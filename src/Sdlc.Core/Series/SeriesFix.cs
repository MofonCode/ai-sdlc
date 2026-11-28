using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Series;

/// <summary>
/// A fix a series added to itself: a test that passed in one recorded run and failed in another stopped the wave's
/// evidence, so the series fixed it in a cycle of its own, at the head of that wave, before the cycles it stopped
/// (D-232).
/// </summary>
/// <param name="Successor">The fix's successor, which the series wrote and took into the wave first.</param>
/// <param name="Test">The test that comes and goes, as the runner named it.</param>
/// <param name="Wave">The wave it was added to.</param>
/// <param name="Added">When.</param>
/// <param name="PassedIn">The cycle whose recorded evidence shows the test passing.</param>
/// <param name="Detail">The failure's message, as the failing run gave it, when it gave one.</param>
/// <param name="Holds">
/// The cycles of the wave that stopped on it, the first being the one whose run failed: each waits for the fix to pass,
/// and is built again once it has.
/// </param>
/// <param name="Results">The failing run's results file, kept in the series' folder, relative to the root.</param>
public sealed record SeriesFix(string Successor, string Test, int Wave, DateTimeOffset Added, string PassedIn, string? Detail, IReadOnlyList<string> Holds, string? Results = null);

/// <summary>
/// Adds a fix to the series checked out here, and says where each cycle a fix holds stands (D-232).
/// </summary>
/// <remarks>
/// <para><strong>Only what the evidence shows comes and goes.</strong> The failing run must show the test failing, and a
/// cycle's recorded evidence in this working copy must show it passing. The build that hit it judged its own change
/// did not cause it (D-216); the binary checks that the failure is real and that the test has passed before, which is
/// what keeps this from being a way to add work the series' list never held (D-231).</para>
/// <para><strong>Bounded</strong>: one fix a wave, never a second for the same test, and never under
/// <c>approval: required</c>, where a cycle nobody approved is never built.</para>
/// <para><strong>It writes and never commits</strong>, as <c>sdlc series wave</c> does: the playbook commits the
/// successor, the kept results and the record together.</para>
/// </remarks>
public static class AddFix
{
    /// <summary>What a cycle a fix holds says it is while the fix has not passed.</summary>
    public const string WaitsOnFix = "waits on its fix";

    /// <summary>What a cycle a fix holds says it is when it was blocked on the fault and its fix has since passed.</summary>
    public const string BuildAgain = "blocked, built again now its fix passed";

    /// <summary>What a cycle a fix holds says it is when the fix stopped without passing.</summary>
    public const string FixStopped = "held: its fix did not pass";

    /// <summary>What a fix's successor is named with, before the test's own name.</summary>
    public const string SlugPrefix = "steady-";

    private static readonly Regex _notSlug = new("[^A-Z0-9]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAddFixRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for a fix added to the series checked out here.</summary>
    /// <param name="Test">The test that failed, as the runner named it.</param>
    /// <param name="Results">The failing run's results file, or a directory holding the run's results files.</param>
    /// <param name="Holds">The cycles of the open wave that stopped on it, the one whose run failed first.</param>
    public sealed record Command(string Test, string Results, IReadOnlyList<string> Holds) : IRequest<Response>;

    /// <summary>The fix added.</summary>
    /// <param name="Record">The record, as written.</param>
    /// <param name="Fix">The fix.</param>
    public sealed record Response(SeriesRecord Record, SeriesFix Fix);

    /// <summary>The successor a fix for a test is named.</summary>
    /// <param name="test">The test, as the runner named it.</param>
    /// <returns>A slug: <see cref="SlugPrefix"/> and the test's last segment, lower-cased and hyphenated.</returns>
    public static string SlugOf(string test)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(test);

        string last = test.Trim();
        int paren = last.IndexOf('(', StringComparison.Ordinal);
        last = paren > 0 ? last[..paren] : last;
        last = last[(last.LastIndexOf('.') + 1)..];

        // Upper-cased to match, then lowered a letter at a time: what is left is ASCII letters, digits and hyphens.
        string words = _notSlug.Replace(last.ToUpperInvariant(), "-").Trim('-');
        string slug = SlugPrefix + string.Concat(words.Select(char.ToLowerInvariant));
        return slug.Length <= 64 ? slug.TrimEnd('-') : slug[..64].TrimEnd('-');
    }

    /// <summary>Where a cycle of a wave stands once the fixes that hold it are read.</summary>
    /// <param name="record">The series' record.</param>
    /// <param name="cycle">The cycle's id.</param>
    /// <param name="state">Where its own files say it stands.</param>
    /// <param name="root">The working copy's root.</param>
    /// <param name="repository">How to ask whether its evidence was recorded on top of its fix.</param>
    /// <returns>The state, the same one when no fix holds it or the fix changes nothing.</returns>
    public static string StateOf(SeriesRecord record, string cycle, string state, string root, IRepository repository)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(repository);

        if (record.Fixes?.LastOrDefault(fix => fix.Holds.Contains(cycle, StringComparer.Ordinal)) is not { } holding
            || state is "built: pass" or "abandoned" or "not here")
        {
            return state;
        }

        string? fixCycle = record.Waves.SelectMany(wave => wave.Items)
            .FirstOrDefault(item => item.Kind == SeriesItemKind.Successor && item.Ref == holding.Successor)?.Cycle;
        Cycle? fix = fixCycle is not null && CycleId.TryParse(fixCycle, out CycleId? id) ? new Cycle(id.Value, root) : null;
        string fixState = fix is null ? NextWave.NotStarted : SeriesStatus.StateOf(fix);

        if (NextWave.IsOpen(fixState))
        {
            return WaitsOnFix;
        }

        if (fixState != "built: pass")
        {
            return NextWave.IsOpen(state) ? FixStopped : state;
        }

        // Blocked on the fault, and not recorded again since the fix: built again on top of it. Recorded on top of it
        // and blocked still, the verdict stands, so the wave can end.
        if (state == "blocked" && CycleId.TryParse(cycle, out CycleId? held))
        {
            string? fixHead = HeadOf(fix!);
            string? heldHead = HeadOf(new Cycle(held.Value, root));

            return fixHead is not null && heldHead is not null && repository.IsAncestor(root, fixHead, heldHead) ? state : BuildAgain;
        }

        return state;
    }

    /// <summary>The fix added, as a person reads it.</summary>
    /// <param name="added">What was added.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response added)
    {
        ArgumentNullException.ThrowIfNull(added);

        SeriesFix fix = added.Fix;
        StringBuilder text = new();

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Series {added.Record.Id} added a fix to wave {fix.Wave}: successor:{fix.Successor}, written to {Successor.RelativePath(fix.Successor)}, first in the wave."));
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  {fix.Test} failed in {fix.Holds[0]}'s run and passed in {fix.PassedIn}'s recorded evidence."));
        text.AppendLine("  Held until it passes: " + string.Join(", ", fix.Holds) + ".");
        text.AppendLine("Commit the successor, the kept results and the series' record, then 'sdlc series wave' hands the wave back with the fix first.");

        return text.ToString();
    }

    /// <summary>One line per fix a series added, for its status and its report; empty when it added none.</summary>
    /// <param name="record">The series' record.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Lines(SeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return
        [
            .. (record.Fixes ?? []).Select(fix => string.Create(
                CultureInfo.InvariantCulture,
                $"wave {fix.Wave}: successor:{fix.Successor} -- {fix.Test} passed in {fix.PassedIn}'s recorded run and failed in {fix.Holds[0]}'s, so the series fixed it first and held {string.Join(", ", fix.Holds)} until it passed{(fix.Detail is { Length: > 0 } detail ? ". The failure: " + detail : string.Empty)}")),
        ];
    }

    /// <summary>The successor of a fix added to the last wave that is still to plan or build, or null when there is none.</summary>
    /// <param name="record">The series' record, its items resolved.</param>
    /// <param name="root">The working copy's root.</param>
    /// <returns>The fix's successor slug, or null.</returns>
    internal static string? Unbuilt(SeriesRecord record, string root)
    {
        if (record.Waves.Count == 0 || record.Fixes?.LastOrDefault(fix => fix.Wave == record.Waves[^1].Number) is not { } fix)
        {
            return null;
        }

        string? cycle = record.Waves[^1].Items.FirstOrDefault(item => item.Kind == SeriesItemKind.Successor && item.Ref == fix.Successor)?.Cycle;
        string state = cycle is not null && CycleId.TryParse(cycle, out CycleId? id) ? SeriesStatus.StateOf(new Cycle(id.Value, root)) : NextWave.NotStarted;

        return NextWave.IsOpen(state) ? fix.Successor : null;
    }

    private static string? HeadOf(Cycle cycle)
    {
        string path = Path.Combine(cycle.EvidenceDirectory, RecordEvidence.HeadFileName);
        return File.Exists(path) ? File.ReadAllText(path).Trim() is { Length: > 0 } head ? head : null : null;
    }

    // Whether two runner names are one test: the same, or one the other's fully qualified form.
    private static bool SameTest(string recorded, string asked) =>
        string.Equals(recorded, asked, StringComparison.Ordinal)
        || recorded.EndsWith("." + asked, StringComparison.Ordinal)
        || asked.EndsWith("." + recorded, StringComparison.Ordinal);

    private sealed class Handler(SolutionRoot solution, IRepository repository, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Result<string> here = SeriesScope.SeriesHere(repository, root);

            if (here.IsFailed)
            {
                return here.ToResult<Response>();
            }

            string id = here.Value;
            Result<SeriesRecord> read = await SeriesRecord.Read(root, id, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SeriesRecord record = read.Value.Resolved(await ReadSuccessors.Starts(root, cancellationToken).ConfigureAwait(false));
            string test = request.Test.Trim();

            if (record.Approval == ApprovalRequirement.Required)
            {
                return Fail($"Series {id} runs under approval: required, so it adds no cycle nobody approved. It stops on {test}: a person has it fixed in a cycle of its own and approved, and continues the series once that is on its branch.");
            }

            if (record.Waves.Count == 0 || record.Waves[^1].Done is not null)
            {
                return Fail($"Series {id} has no wave open, so there is nothing for a fix to go first in. A fix is added while the wave it stopped is being built.");
            }

            SeriesWave wave = record.Waves[^1];
            SeriesFix[] fixes = [.. record.Fixes ?? []];

            if (fixes.FirstOrDefault(fix => SameTest(fix.Test, test)) is { } before)
            {
                return Fail($"Series {id} already added a fix for {test}, in wave {before.Wave} (successor:{before.Successor}), and it fails again. That is a person's to look at: the series adds one fix a test.");
            }

            if (fixes.FirstOrDefault(fix => fix.Wave == wave.Number) is { } other)
            {
                return Fail($"Wave {wave.Number} of series {id} already added a fix, for {other.Test}. A second fault in one wave is a person's to look at: the series adds one fix a wave.");
            }

            List<string> holds = [.. request.Holds.Select(hold => hold.Trim()).Where(hold => hold.Length > 0).Distinct(StringComparer.Ordinal)];
            HashSet<string> inWave = new(wave.Items.Select(item => item.Cycle).OfType<string>(), StringComparer.Ordinal);

            if (holds.Count == 0)
            {
                return Fail("Which cycles did it stop? Name them with --holds, the one whose run failed first: a fix holds what stopped on it.");
            }

            if (holds.FirstOrDefault(hold => !inWave.Contains(hold)) is { } stray)
            {
                return Fail($"{stray} is not a cycle of wave {wave.Number} of series {id}. A fix holds only cycles of the wave it is added to.");
            }

            Result<(string File, RecordedTest Test)> failed = await Failing(request.Results, test, cancellationToken).ConfigureAwait(false);

            if (failed.IsFailed)
            {
                return failed.ToResult<Response>();
            }

            string? passedIn = PassedIn(root, failed.Value.Test.Name, test, holds);

            if (passedIn is null)
            {
                return Fail($"No cycle's recorded evidence here shows {test} passing, so nothing shows it comes and goes. A failure the change caused is the cycle's own to fix (D-216); one that has never passed is a person's to look at.");
            }

            string slug = SlugOf(failed.Value.Test.Name);

            if (File.Exists(Path.Combine(Successor.DirectoryOf(root), slug + ".md"))
                || record.Waves.Any(taken => taken.Items.Any(item => item.Kind == SeriesItemKind.Successor && item.Ref == slug)))
            {
                return Fail($"successor:{slug} already exists. A fix for {test} is already waiting or was taken; a person looks at why it fails again.");
            }

            string? detail = failed.Value.Test.Detail is { } message ? FirstLine(message) : null;
            string kept = SeriesRecord.RelativeDirectory(id) + "/" + slug + Path.GetExtension(failed.Value.File);
            SeriesFix added = new(slug, failed.Value.Test.Name, wave.Number, clock.GetUtcNow(), passedIn, detail, holds, kept);

            Directory.CreateDirectory(Successor.DirectoryOf(root));
            await File.WriteAllTextAsync(Path.Combine(Successor.DirectoryOf(root), slug + ".md"), SuccessorText(added, id), cancellationToken).ConfigureAwait(false);
            File.Copy(failed.Value.File, Path.Combine(root, kept.Replace('/', Path.DirectorySeparatorChar)), overwrite: true);

            record = record.WithFix(added);
            await File.WriteAllTextAsync(SeriesRecord.PathIn(root, id), record.Write(), cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(record, added));
        }

        private static Result<Response> Fail(string message) => Result.Fail<Response>(message);

        // The results file in which the test failed, and the test as it was recorded there.
        private static async Task<Result<(string File, RecordedTest Test)>> Failing(string results, string test, CancellationToken cancellationToken)
        {
            string[] files = Directory.Exists(results)
                ? [.. Directory.EnumerateFiles(results, "*.trx", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
                : File.Exists(results) ? [results] : [];

            if (files.Length == 0)
            {
                return Result.Fail($"There is no results file at {results}. Name the failing run's .trx, or the directory holding its results files.");
            }

            bool seen = false;

            foreach (string file in files)
            {
                Result<IReadOnlyList<RecordedTest>> read = TestSummaryReader.ReadTrx(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), file);

                if (read.IsFailed)
                {
                    return read.ToResult<(string, RecordedTest)>();
                }

                foreach (RecordedTest recorded in read.Value.Where(recorded => SameTest(recorded.Name, test)))
                {
                    seen = true;

                    if (recorded.Outcome == TestOutcome.Failed)
                    {
                        return Result.Ok((file, recorded));
                    }
                }
            }

            return Result.Fail(seen
                ? $"{test} did not fail in the run at {results}. A fix is added for a test the failing run shows failing."
                : $"{test} is not in the run at {results}. Name it as the runner did.");
        }

        // The latest cycle whose recorded evidence shows the test passing, a cycle it holds only when no other does.
        private static string? PassedIn(string root, string recorded, string asked, IReadOnlyList<string> holds)
        {
            List<string> passed = [];

            foreach (Cycle cycle in CycleIndex.All(root))
            {
                string path = Path.Combine(cycle.EvidenceDirectory, RecordEvidence.SummaryFileName);

                if (File.Exists(path)
                    && TestSummaryReader.Read(File.ReadAllText(path), path) is { IsSuccess: true } summary
                    && summary.Value.Tests.Any(test => test.Outcome == TestOutcome.Passed && (SameTest(test.Name, recorded) || SameTest(test.Name, asked))))
                {
                    passed.Add(cycle.Id.ToString());
                }
            }

            return passed.Where(cycle => !holds.Contains(cycle, StringComparer.Ordinal)).Order(StringComparer.Ordinal).LastOrDefault()
                ?? passed.Order(StringComparer.Ordinal).LastOrDefault();
        }

        private static string FirstLine(string message)
        {
            string line = message.Trim().Split('\n')[0].Trim();
            return line.Length <= 300 ? line : line[..300] + "...";
        }

        // The fix's successor: an intent's substance, written from the evidence alone.
        private static string SuccessorText(SeriesFix fix, string series) => string.Create(
            CultureInfo.InvariantCulture,
            $"""
            ---
            succeeds: {fix.PassedIn}
            ---

            # {fix.Test} gives the same result on every run

            ## Outcome

            `{fix.Test}` passes or fails the same way on every run of the same code, so no cycle's evidence is stopped by it again.

            ## Why

            It passed in cycle {fix.PassedIn}'s recorded run and failed in cycle {fix.Holds[0]}'s, whose build judged its own change did not cause it. A test that fails only sometimes is fixed in a cycle of its own, never run again until green (D-216), so series {series} added this fix to wave {fix.Wave} and held {string.Join(", ", fix.Holds)} until it passes (D-232). {(fix.Detail is { Length: > 0 } detail ? "The failure: \"" + detail + "\". " : string.Empty)}The failing run's results are kept at `{fix.Results}`.

            """);
    }
}

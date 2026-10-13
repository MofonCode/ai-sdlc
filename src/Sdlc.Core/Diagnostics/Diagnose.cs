using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Hooks;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Diagnostics;

/// <summary>What one check of an installation found.</summary>
public enum CheckOutcome
{
    /// <summary>It is there and it is right.</summary>
    Sound,

    /// <summary>Something is wrong, and the check says what to do about it.</summary>
    Problem,

    /// <summary>
    /// Not the binary's to answer, and the check names who to ask instead.
    /// </summary>
    /// <remarks>
    /// <strong>The category that keeps this command honest.</strong> Half of what a person wants to
    /// know about an install is on the other side of a boundary the binary cannot see across -- whether
    /// the host loaded the plugin, whether a git hook is installed in this clone, whether a pipeline
    /// step is a <em>required</em> check. Reporting those as sound would make the panel a thing that
    /// says an install works when nobody has established that, which is the failure this whole
    /// framework is written against. Reporting them as problems would make the panel cry wolf. So they
    /// are a third thing, and they do not count against the exit code.
    /// </remarks>
    Elsewhere,
}

/// <summary>One thing that was looked at.</summary>
/// <param name="Subject">What was checked, in two or three words.</param>
/// <param name="Outcome">What was found.</param>
/// <param name="Detail">What is true, stated so a person can act on it.</param>
/// <param name="Remedy">The command or the step that fixes it, where there is one.</param>
public sealed record Check(string Subject, CheckOutcome Outcome, string Detail, string? Remedy = null);

/// <summary>
/// Checks an installation and a repository, and says which half of a two-half install is missing.
/// </summary>
/// <remarks>
/// <strong>The two halves fail differently and the failures look the same from inside a session.</strong>
/// Without the binary, a skill's first command is not found. Without the plugin, nothing ever asks for a
/// cycle -- and that one is silent, because an agent with no skills behaves like an agent that was not
/// asked. The install guide has always said so in prose and left the reader to walk it; this is the walk,
/// in one command, which is the only part of it a tool can do.
/// <para>
/// <strong>It reports and never repairs.</strong> Every remedy it prints is a command a person runs --
/// adoption especially, which the pre-tool hook denies to an agent. A doctor that fixed things would be
/// a second path into the files adoption writes, and the one rule that keeps the hook configuration out
/// of the hands of the thing it constrains is that there is exactly one.
/// </para>
/// </remarks>
public static class Diagnose
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddDiagnoseRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what state this installation and this repository are in.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>What was found.</summary>
    /// <param name="Checks">Every check, in the order a person reads them.</param>
    public sealed record Response(IReadOnlyList<Check> Checks)
    {
        /// <summary>
        /// Whether anything is wrong.
        /// </summary>
        /// <remarks>
        /// <see cref="CheckOutcome.Elsewhere"/> does not count. It is not an unknown that somebody
        /// should resolve before trusting the install -- it is a question with a different owner, and
        /// a command that exited non-zero for one would be non-zero on every correct installation.
        /// </remarks>
        public bool Sound => !Checks.Any(check => check.Outcome == CheckOutcome.Problem);
    }

    /// <summary>
    /// Looks at everything about an install that can be looked at from here.
    /// </summary>
    /// <param name="solution">The repository being checked.</param>
    /// <param name="repository">How to ask git what it knows.</param>
    /// <param name="mediator">To ask for the constraints the way every other caller does.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository, Mediator mediator)
        : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<Check> checks =
            [
                Binary(),
                await Rules(cancellationToken).ConfigureAwait(false),
                Git(),
            ];

            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);

            if (!Directory.Exists(sdlc))
            {
                // Everything below reads what adoption wrote, so there is nothing to say about any of
                // it. Reporting six missing files would bury the one fact that matters.
                checks.Add(new Check(
                    "adoption",
                    CheckOutcome.Problem,
                    "this repository has no " + Cycle.SdlcDirectoryName + " directory, so it is not governed",
                    "a person runs 'sdlc init' here. An agent cannot: the hook denies it"));

                return Result.Ok(new Response(checks));
            }

            checks.Add(await Adoption(cancellationToken).ConfigureAwait(false));
            checks.Add(await DeclaredStack(cancellationToken).ConfigureAwait(false));

            foreach (SupportedHost host in SupportedHosts.All)
            {
                checks.Add(HookConfig(host));
            }

            checks.Add(Pipeline(await RecordedPlatform(cancellationToken).ConfigureAwait(false)));
            checks.Add(PrePush());
            checks.Add(Plugin());

            return Result.Ok(new Response(checks));
        }

        private static Check Binary() => new(
            "binary",
            CheckOutcome.Sound,
            string.Create(CultureInfo.InvariantCulture, $"sdlc {Release.Version}, on the path"));

        /// <summary>
        /// Whether the rules resolve, asked the way every other caller asks.
        /// </summary>
        /// <remarks>
        /// Dispatched rather than read off the disk, so this checks the path a stage skill actually
        /// takes. The binary carries the library (D-41), so the normal answer names the embedded copy;
        /// the one real failure is <c>SDLC_CONSTRAINTS</c> naming a file that is not there, which is a
        /// person's explicit instruction about which rules bind.
        /// </remarks>
        private async Task<Check> Rules(CancellationToken cancellationToken)
        {
            Result<EffectiveConstraints.Response> rules = await mediator
                .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(
                    new EffectiveConstraints.Query(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (rules.IsFailed)
            {
                return new Check(
                    "rules",
                    CheckOutcome.Problem,
                    string.Join("; ", rules.Errors.Select(error => error.Message)),
                    "unset SDLC_CONSTRAINTS to use the copy the binary carries, or point it at a file that exists");
            }

            int count = rules.Value.Constraints.Count;

            return new Check(
                "rules",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{count} in force. 'sdlc constraints' renders them"));
        }

        /// <summary>
        /// Whether git is here, which everything else rests on.
        /// </summary>
        /// <remarks>
        /// The same refusal adoption gives, as a check: without git a build cannot record the diff a
        /// verification derives from and the gates key on a branch that cannot exist, so every hook
        /// would permit everything while the directory still looked governed.
        /// </remarks>
        private Check Git() => repository.IsRepository(solution.Path)
            ? new Check("git", CheckOutcome.Sound, "a repository")
            : new Check(
                "git",
                CheckOutcome.Problem,
                solution.Path + " is not a git repository, so nothing here can be governed",
                "run 'git init' first");

        private async Task<Check> Adoption(CancellationToken cancellationToken)
        {
            Result<SolutionDocument> document = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            if (document.IsFailed)
            {
                return new Check(
                    "adoption",
                    CheckOutcome.Problem,
                    string.Join("; ", document.Errors.Select(error => error.Message)),
                    "only a person writes " + Cycle.SolutionFileName + ". 'sdlc draft solution' writes what belongs in it beside it");
            }

            return new Check(
                "adoption",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"origin {SolutionDocument.NameOf(document.Value.Origin)}, standard {SolutionDocument.NameOf(document.Value.Standard)}, platform {SolutionDocument.NameOf(document.Value.Platform)}"));
        }

        /// <summary>
        /// Whether a declared stack is in place, and what it pins and switches on.
        /// </summary>
        /// <remarks>
        /// <para><strong>Missing is the fault; empty is an answer.</strong> A person places this file,
        /// and no cycle opens without it, so its absence means nobody has yet said what the solution is
        /// built from. A file with nothing in it is different: that is a person declaring that nothing
        /// is pinned and each plan chooses freely, which is theirs to decide. This reported an empty
        /// table as a problem while adoption wrote the file itself, because then empty meant nobody had
        /// got round to it; now the rename is the statement (D-76).</para>
        ///
        /// <para><strong>The quiet failures that are left each leave a working gate.</strong> A row
        /// with no answer pins nothing while looking like a decision; the options menu pasted in offers
        /// a plan answers the solution never chose; a rule row naming an id this build does not ship
        /// switches on nothing while reading as if it did. Every stage passes in all three.</para>
        ///
        /// <para><strong>A shipped rule the file does not list is information, never fault.</strong>
        /// What is listed is what binds, so a release that adds a rule changes nothing for a solution
        /// already adopted until somebody adds its row. That is the right way round -- an upgrade of
        /// the binary should not change what a plan is judged against -- and it is worth saying so the
        /// new rule is declined rather than merely unseen.</para>
        ///
        /// <para>Reported and never repaired, like everything else here. Rewriting a person's file
        /// would be the gate deciding what a team is built from.</para>
        /// </remarks>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>What the declared stack is in, as one check.</returns>
        private async Task<Check> DeclaredStack(CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                solution.Path,
                Cycle.SdlcDirectoryName,
                Cycle.TechStackFileName);

            if (!File.Exists(path))
            {
                return new Check(
                    "declared stack",
                    CheckOutcome.Problem,
                    Cycle.TechStackFileName + " is not there, so nothing says what this solution is built from and no cycle opens",
                    "'sdlc draft techstack' writes a draft of the framework's default, or of nothing with --empty; a person renames it into place");
            }

            string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            // The menu, pasted into the real file. It is written beside the draft and never over it for
            // exactly this reason: a declared stack listing answers the solution has not chosen lets a
            // plan reach for one and truthfully not be departing from anything, which is the deadlock
            // protecting this file closed.
            if (text.Contains(StackOptions.Marker, StringComparison.Ordinal))
            {
                return new Check(
                    "declared stack",
                    CheckOutcome.Problem,
                    "it carries the options menu, which lists answers this solution has not chosen",
                    "delete everything from the options marker down. The menu is a report under '.sdlc/reports/', which is never renamed into place");
            }

            IReadOnlyList<StackTable.Row> rows = StackTable.Rows(text);
            IReadOnlyList<StackRules.Row> rules = StackRules.Rows(text);

            string[] shipped = [.. Constraints.ShippedProfileLibrary.Rules()
                .Select(Constraints.ShippedProfileLibrary.RowId)];

            // A row that switches on a rule nothing ships reads as a solution being held to something
            // and holds it to nothing. A typo, or a rule a later release retired.
            string[] unknown = [.. rules
                .Select(rule => rule.Id)
                .Where(id => !shipped.Contains(id, StringComparer.OrdinalIgnoreCase))];

            if (unknown.Length > 0)
            {
                return new Check(
                    "declared stack",
                    CheckOutcome.Problem,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"it lists {string.Join(", ", unknown)}, which this release does not ship, so that row switches on nothing"),
                    "'sdlc draft techstack' writes the menu with every stack rule this release ships. Correct the id in the draft or delete the row, and rename it");
            }

            string[] undeclared = [.. rows
                .Where(row => string.IsNullOrWhiteSpace(row.Choice))
                .Select(row => row.Concern)];

            if (undeclared.Length > 0)
            {
                return new Check(
                    "declared stack",
                    CheckOutcome.Problem,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{undeclared.Length} of {rows.Count} rows declare nothing: {string.Join(", ", undeclared)}"),
                    "'sdlc draft techstack' writes the menu beside the draft, with what each profile declares for every concern. A row this solution has no use for is better deleted than left empty");
            }

            int on = rules.Count(rule => rule.On);

            // Nothing in it at all: a person placed an empty file, which declares that nothing is
            // pinned. Said in full, because "0 rows" reads as a fault and this is not one.
            if (rows.Count == 0 && rules.Count == 0)
            {
                return new Check(
                    "declared stack",
                    CheckOutcome.Sound,
                    "it pins nothing and switches on no stack rule, so every plan chooses freely -- which is a solution's to decide");
            }

            string[] unlisted = [.. shipped.Where(id => !rules.Any(rule =>
                string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase)))];

            // A solution adopted before the rules were rows lists none of them and is still held to the
            // ones its solution.md names, so it is told where they come from rather than that it is
            // missing every one.
            string tail = rules.Count == 0
                ? await Legacy(shipped.Length, cancellationToken).ConfigureAwait(false)
                : unlisted.Length == 0
                    ? string.Empty
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $". This release also ships {string.Join(", ", unlisted)}, which it does not list and so is not held to");

            return new Check(
                "declared stack",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rows.Count} rows declared, {on} of {rules.Count} listed stack rules on{tail}"));
        }

        /// <summary>
        /// What to say about a declared stack that lists no rule at all.
        /// </summary>
        /// <remarks>
        /// Two solutions look like that. One was adopted before the rules were rows: its
        /// <c>solution.md</c> still names a profile, and it is still held to that profile's rules, so
        /// saying it lists none would read as having none. The other chose its rows and no rules. They
        /// are told apart by the line that used to select them, which is the only thing still reading
        /// it.
        /// </remarks>
        /// <param name="shipped">How many stack rules this release ships.</param>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>A clause to append to the detail.</returns>
        private async Task<string> Legacy(int shipped, CancellationToken cancellationToken)
        {
            Result<SolutionDocument> document = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            return document.IsSuccess && StackProfiles.Find(document.Value.LegacyProfile) is { } profile
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $". It lists none, so the rules still come from the '{profile.Name}' line in {Cycle.SolutionFileName}, where they used to be selected. 'sdlc draft techstack' writes the menu with the rows to copy in, which is what makes them visible here")
                : shipped == 0
                    ? string.Empty
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $". This release ships {shipped}, none of which it is held to");
        }

        /// <summary>
        /// Which platform this solution records, or GitHub when it is too broken to say.
        /// </summary>
        /// <remarks>
        /// A solution document that will not read is already reported by <see cref="Adoption"/>, and the
        /// pipeline check must not go quiet because of it: falling back to the strictest reading means an
        /// unreadable document never buys an exemption from the guarantee.
        /// </remarks>
        private async Task<Platform> RecordedPlatform(CancellationToken cancellationToken)
        {
            Result<SolutionDocument> document = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            return document.IsSuccess ? document.Value.Platform : Platform.GitHub;
        }

        /// <summary>
        /// Whether one host's hook configuration is there and calls this binary.
        /// </summary>
        /// <remarks>
        /// <strong>Checked by what it runs rather than by existing.</strong> A file that parses and
        /// names no gate is the shape an upgrade leaves when a merge went wrong, and it is
        /// indistinguishable from a working install by a listing. This is the dangerous direction --
        /// a hook that is not loaded denies nothing and every cycle still appears to work -- so the
        /// check reads the file.
        /// </remarks>
        private Check HookConfig(SupportedHost host)
        {
            string relative = host.ConfigPath;
            string path = Path.Combine(solution.Path, Path.Combine([.. relative.Split('/')]));
            string subject = host.Name;

            if (!File.Exists(path))
            {
                return new Check(
                    subject,
                    CheckOutcome.Problem,
                    relative + " is missing, so nothing is denied on that host",
                    "a person runs 'sdlc init' here, which writes it");
            }

            string command = "sdlc hook --host " + host.Spelling;

            if (!File.ReadAllText(path).Contains(command, StringComparison.Ordinal))
            {
                return new Check(
                    subject,
                    CheckOutcome.Problem,
                    relative + " exists and does not call the gate",
                    "a person runs 'sdlc init' here, which puts the entry back");
            }

            return new Check(
                subject,
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{relative}, {(host.GovernsTurnEnd ? "tool calls and the turn end" : "tool calls only")}"));
        }

        /// <summary>
        /// Whether a pipeline anywhere here runs the one check that is the guarantee.
        /// </summary>
        /// <remarks>
        /// Every file is searched rather than the one adoption writes, because a team may have moved
        /// the step into a pipeline of their own and that is a correct install. What this cannot see is
        /// whether the check is <em>required</em> on the branch, which is the half that makes it a
        /// promise -- so that part is reported as somebody else's to confirm.
        /// </remarks>
        private Check Pipeline(Platform platform)
        {
            // Local records that there is no pipeline, so the absence of one is the install working. It
            // is still reported rather than passed silently: what is missing is the only non-bypassable
            // layer, and a solution that said nothing about that would let somebody believe the pre-push
            // hook was the guarantee. The hook is local and '--no-verify' walks past it.
            if (platform == Platform.Local)
            {
                return new Check(
                    "the guarantee",
                    CheckOutcome.Elsewhere,
                    "this solution is local, so no pipeline check governs a merge. The pre-push hook is "
                    + "the only mechanical layer and it is bypassable",
                    "when there is a remote, 'sdlc init --platform github' or '--platform azure-devops' "
                    + "writes the pipeline, and a person makes the check required in branch policy");
            }

            string[] candidates =
            [
                .. Directory.Exists(Path.Combine(solution.Path, ".github", "workflows"))
                    ? Directory.EnumerateFiles(Path.Combine(solution.Path, ".github", "workflows"))
                    : [],
                .. File.Exists(Path.Combine(solution.Path, "azure-pipelines.yml"))
                    ? [Path.Combine(solution.Path, "azure-pipelines.yml")]
                    : Array.Empty<string>(),
            ];

            string? found = Array.Find(
                candidates,
                file => File.ReadAllText(file).Contains("sdlc verify-chain", StringComparison.Ordinal));

            return found is null
                ? new Check(
                    "the guarantee",
                    CheckOutcome.Problem,
                    "no pipeline here runs 'sdlc verify-chain', so nothing stops an ungoverned change merging",
                    "a person runs 'sdlc init' here, and then makes the check required in branch policy")
                : new Check(
                    "the guarantee",
                    CheckOutcome.Sound,
                    Path.GetRelativePath(solution.Path, found).Replace('\\', '/') + " runs it");
        }

        /// <summary>
        /// The pre-push hook, which is written and never installed.
        /// </summary>
        /// <remarks>
        /// Whether it is installed is <c>core.hooksPath</c>, which is per clone and not tracked -- so
        /// the file being there says nothing about whether it runs. Reporting the file as sound would
        /// be the dishonest half of that.
        /// </remarks>
        private Check PrePush()
        {
            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, "hooks", "pre-push");

            return File.Exists(path)
                ? new Check(
                    "the pre-push hook",
                    CheckOutcome.Elsewhere,
                    "written. Whether it runs is core.hooksPath, which is per clone and untracked",
                    "git config core.hooksPath " + Cycle.SdlcDirectoryName + "/hooks")
                : new Check(
                    "the pre-push hook",
                    CheckOutcome.Problem,
                    Cycle.SdlcDirectoryName + "/hooks/pre-push is missing",
                    "a person runs 'sdlc init' here, which writes it");
        }

        /// <summary>
        /// The other half of the install, which this cannot see.
        /// </summary>
        /// <remarks>
        /// A binary cannot ask a host what it loaded. Saying so is the point: without the plugin
        /// nothing ever asks for a cycle, and that failure is silent, because an agent with no skills
        /// behaves exactly like an agent nobody asked.
        /// </remarks>
        private static Check Plugin() => new(
            "the skills",
            CheckOutcome.Elsewhere,
            "a host loads these, and a binary cannot ask it what it loaded",
            "on Claude Code: 'claude plugin details sdlc@ai-sdlc'. Expect Skills (17) and Hooks (2)");
    }
}

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
    /// the host loaded the plugin, and whether a pipeline step is a <em>required</em> check when the
    /// platform's CLI cannot be asked (D-106). Reporting
    /// those as sound would make the panel a thing that
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
    /// <param name="Setup">
    /// What a person still has to place before the first cycle, or null where the repository is not
    /// adopted. Each step ends in a rename, which runs nothing, so this is where "what now" is
    /// answered after the first one (D-79).
    /// </param>
    public sealed record Response(IReadOnlyList<Check> Checks, SetupState? Setup = null)
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
    /// <param name="policies">To ask the platform what its branch policy requires.</param>
    private sealed class Handler(SolutionRoot solution, IRepository repository, Mediator mediator, IBranchPolicies policies)
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
            checks.Add(await IntegrationBranch(cancellationToken).ConfigureAwait(false));
            checks.Add(await Stamp(cancellationToken).ConfigureAwait(false));
            checks.Add(await DeclaredStack(cancellationToken).ConfigureAwait(false));

            if (await BuildSettingsDeclared(cancellationToken).ConfigureAwait(false) is { } settings)
            {
                checks.Add(settings);
            }

            checks.Add(await LineEndingsCheck(cancellationToken).ConfigureAwait(false));

            foreach (SupportedHost host in SupportedHosts.All)
            {
                checks.Add(HookConfig(host));
            }

            Platform platform = await RecordedPlatform(cancellationToken).ConfigureAwait(false);
            Check pipeline = Pipeline(platform);

            checks.Add(pipeline);

            if (pipeline.Outcome == CheckOutcome.Sound && PipelineFile() is { } file)
            {
                string integration = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);

                checks.Add(BranchPolicy(
                    platform,
                    integration,
                    await policies.Ask(platform, integration, file, solution.Path, cancellationToken).ConfigureAwait(false)));
            }

            checks.Add(Plugin());

            return Result.Ok(new Response(
                checks,
                await Setup.Read(solution.Path, cancellationToken).ConfigureAwait(false)));
        }

        /// <summary>
        /// Whether every text file is stored with one line ending, whoever committed it.
        /// </summary>
        /// <remarks>
        /// A problem rather than a note, because the cost lands on review: once a solution is shared, a
        /// commit from another platform shows every line of a file as changed. Adoption writes the rule
        /// where there is no file, so this fires for a solution adopted before it did, or one whose own
        /// file leaves it out.
        /// </remarks>
        private async Task<Check> LineEndingsCheck(CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, LineEndings.FileName);
            bool exists = File.Exists(path);

            if (exists
                && LineEndings.Normalises(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)))
            {
                return new Check("line endings", CheckOutcome.Sound, "normalised by " + LineEndings.FileName);
            }

            return exists
                ? new Check(
                    "line endings",
                    CheckOutcome.Problem,
                    LineEndings.FileName + " does not normalise every text file, so a commit from another platform can show a whole file changed",
                    "add '" + LineEndings.Rule + "' to " + LineEndings.FileName + ", then commit 'git add --renormalize .'")
                : new Check(
                    "line endings",
                    CheckOutcome.Problem,
                    "there is no " + LineEndings.FileName + ", so a commit from another platform can show a whole file changed",
                    "a person runs 'sdlc init', which writes it; then commit 'git add --renormalize .'");
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
                    File.Exists(SolutionDocument.DraftPathIn(solution.Path))
                        ? "only a person writes " + Cycle.SolutionFileName + ": fill in the paragraph in " + WriteSolutionDraft.DraftFileName + ", then rename it over " + Cycle.SolutionFileName
                        : "only a person writes " + Cycle.SolutionFileName + ". 'sdlc draft solution' writes what belongs in it beside it");
            }

            // A problem rather than a step left, which D-79 had it as. Every plan reads this paragraph for
            // what good looks like here, and a placeholder tells it nothing -- so a solution that carries
            // one is not ready for a cycle, and saying so with the exit code is accurate (D-94).
            if (WriteSolutionDraft.IsPlaceholder(
                await File.ReadAllTextAsync(SolutionDocument.PathIn(solution.Path), cancellationToken).ConfigureAwait(false)))
            {
                return new Check(
                    "adoption",
                    CheckOutcome.Problem,
                    Cycle.SolutionFileName + " still carries the template's text where the paragraph saying what this solution is goes, so every plan is told nothing about what good looks like here",
                    "write the paragraph in " + Cycle.SolutionFileName + " -- only a person can");
            }

            return new Check(
                "adoption",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"origin {SolutionDocument.NameOf(document.Value.Origin)}, standard {SolutionDocument.NameOf(document.Value.Standard)}, platform {SolutionDocument.NameOf(document.Value.Platform)}"));
        }

        /// <summary>
        /// Whether the framework's own files were written by this release.
        /// </summary>
        /// <remarks>
        /// <para><strong>A problem in either direction.</strong> Older means the hook configuration and the
        /// pipeline may be a release behind the binary judging the cycles -- an upgrade on this machine that
        /// never reached this repository. Newer means somebody refreshed it with a later binary than this
        /// one, which then judges with rules the files were not written for. Neither is visible any other
        /// way, and both are fixed by one command (D-93).</para>
        ///
        /// <para><strong>No stamp is the same problem as an old one.</strong> The files were written before
        /// stamps were, which is older than any release that writes one.</para>
        /// </remarks>
        private async Task<Check> Stamp(CancellationToken cancellationToken)
        {
            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, "README.md");

            string? stamped = File.Exists(path)
                ? FrameworkStamp.Read(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false))
                : null;

            const string refresh = "a person runs 'sdlc init' here, which refreshes what the framework owns and touches nothing a person writes";

            if (stamped is null)
            {
                return new Check(
                    "framework files",
                    CheckOutcome.Problem,
                    Cycle.SdlcDirectoryName + "/README.md carries no release stamp, so the hook configuration and the pipeline were written before stamps were, by an older release than this one",
                    refresh);
            }

            int compared = FrameworkStamp.Compare(stamped, Release.Version);

            return compared switch
            {
                0 => new Check(
                    "framework files",
                    CheckOutcome.Sound,
                    string.Create(CultureInfo.InvariantCulture, $"written by sdlc {stamped}, this release")),
                < 0 => new Check(
                    "framework files",
                    CheckOutcome.Problem,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"written by sdlc {stamped}, and this binary is {Release.Version}, so the hook configuration and the pipeline may be a release behind"),
                    refresh),
                _ => new Check(
                    "framework files",
                    CheckOutcome.Problem,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"written by sdlc {stamped}, which is newer than this binary ({Release.Version})"),
                    "upgrade the binary on this machine -- the install guide's Update section -- rather than re-running init with this one"),
            };
        }

        /// <summary>
        /// Whether the integration branch the solution names exists, here or on the remote.
        /// </summary>
        /// <remarks>
        /// Without it a cycle's plan branch starts from whatever happens to be checked out, and the plan's
        /// pull request has nothing to merge into. Adoption creates it after its commit, so this fires on a
        /// solution adopted before that, or one whose key was changed and the branch never made (D-94).
        /// </remarks>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>The check.</returns>
        private async Task<Check> IntegrationBranch(CancellationToken cancellationToken)
        {
            string branch = await SolutionDocument.IntegrationBranchOf(solution.Path, cancellationToken).ConfigureAwait(false);

            return repository.RefExists(solution.Path, branch) || repository.RefExists(solution.Path, "origin/" + branch)
                ? new Check("integration branch", CheckOutcome.Sound, branch + ", where approved plans wait and built cycles land")
                : new Check(
                    "integration branch",
                    CheckOutcome.Problem,
                    branch + " does not exist, so a cycle's plan branch starts from whatever is checked out and its pull request has nowhere to go",
                    "'git switch -c " + branch + "' from the commit adoption made. Running adoption again creates it as well");
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
        /// <para><strong>A shipped rule or default the file does not mention is information, never
        /// fault.</strong> What is listed is what binds, so a release that adds one changes nothing for a
        /// solution already adopted until somebody adds its row. That is the right way round -- an
        /// upgrade of the binary should not change what a plan is judged against -- and it is worth
        /// saying so the new row is answered rather than merely unseen. A row saying <c>none</c> or
        /// <c>false</c> is an answer, so only what was never asked is named (D-93).</para>
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
                    File.Exists(Path.Combine(solution.Path, Cycle.SdlcDirectoryName, WriteTechStackDraft.DraftFileName))
                        ? "fill in " + WriteTechStackDraft.DraftFileName + " -- 'none' in each row not true here -- then rename it over " + Cycle.TechStackFileName + ". Only a person can"
                        : "'sdlc draft techstack' writes a draft of the framework's default, or of nothing with --empty; a person renames it into place");
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
                    "'sdlc draft techstack' writes the menu beside the draft, with what each profile declares for every concern. A row this solution has no use for says 'none' rather than being left empty or deleted, so a later release knows not to ask");
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

            // Everything the stack has no row for, defaults and rules alike. A row that says no -- 'none',
            // or false -- is an answer and is not listed; only what was never asked is (D-93).
            StackCoverage.Unmentioned unmentioned = StackCoverage.Of(text);

            string tail = unmentioned.IsEmpty
                ? string.Empty
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $". It does not mention {string.Join(", ", unmentioned.Names)}, which this release ships -- 'sdlc stack' lists them, and a row answering each, 'none' or false included, means nobody is asked again");

            return new Check(
                "declared stack",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rows.Count} rows declared, {on} of {rules.Count} listed stack rules on{tail}"));
        }

        /// <summary>
        /// Where the build settings a declared stack takes stand against what is on disk.
        /// </summary>
        /// <remarks>
        /// <para><strong>A declared file that is not there is information, never fault.</strong> A row
        /// writes nothing: the file arrives through a cycle whose approved plan declares it, so the
        /// interval between switching a row on and that cycle landing is the design working. Calling
        /// it a problem would fail this command on every solution that had just decided to take one,
        /// which is the moment somebody is most likely to run it (D-77).</para>
        ///
        /// <para><strong>A row naming a file nothing ships is the fault</strong>, for the reason an
        /// unknown rule id is: it reads as a solution having taken something and takes nothing.</para>
        ///
        /// <para>Null where there is nothing to say -- no declared stack, which the check above has
        /// already reported, or one with no build-settings rows at all.</para>
        /// </remarks>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>The check, or null when the declared stack says nothing about build settings.</returns>
        private async Task<Check?> BuildSettingsDeclared(CancellationToken cancellationToken)
        {
            Result<ReadStackDeclaration.Response> read = await mediator
                .Send<ReadStackDeclaration.Query, ReadStackDeclaration.Response>(new ReadStackDeclaration.Query(), cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed || !read.Value.Placed)
            {
                return null;
            }

            ReadStackDeclaration.Response stack = read.Value;

            if (stack.Unknown.Count > 0)
            {
                return new Check(
                    "build settings",
                    CheckOutcome.Problem,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the declared stack lists {string.Join(", ", stack.Unknown)}, which this release does not ship, so that row takes nothing"),
                    "'sdlc draft techstack' writes the menu with every build settings file this release ships. Correct the name in the draft or delete the row, and rename it");
            }

            int declared = stack.Settings.Count(state => state.Declaration == ReadStackDeclaration.Declaration.Declared);

            if (stack.Settings.All(state => state.Declaration == ReadStackDeclaration.Declaration.NoRow))
            {
                return null;
            }

            string tail = stack.Gap.Count == 0
                ? declared == 0 ? string.Empty : ", all of them here"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $". Not here yet: {string.Join(", ", stack.Gap.Select(setting => setting.Path))} -- a cycle whose approved plan declares one brings it, and 'sdlc stack' shows the comparison");

            return new Check(
                "build settings",
                CheckOutcome.Sound,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{declared} of {stack.Settings.Count} taken by the declared stack{tail}"));
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
            // layer, and a solution that said nothing about that would let somebody believe a local cycle
            // was governed to the same standard. Nothing on this machine stands in for it (D-78).
            if (platform == Platform.Local)
            {
                return new Check(
                    "the guarantee",
                    CheckOutcome.Elsewhere,
                    "this solution is local, so no pipeline check governs a merge and nothing "
                    + "mechanical decides what reaches the default branch",
                    "when there is a remote, 'sdlc init --platform github' or '--platform azure-devops' "
                    + "writes the pipeline, and a person makes the check required in branch policy");
            }

            string? found = PipelineFile();

            return found is null
                ? new Check(
                    "the guarantee",
                    CheckOutcome.Problem,
                    "no pipeline here runs 'sdlc verify-chain', so nothing stops an ungoverned change merging",
                    "a person runs 'sdlc init' here, and then makes the check required in branch policy")
                : new Check(
                    "the guarantee",
                    CheckOutcome.Sound,
                    found + " runs it");
        }

        /// <summary>The pipeline file that runs the chain, relative and with forward slashes, or null.</summary>
        private string? PipelineFile()
        {
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

            return found is null ? null : Path.GetRelativePath(solution.Path, found).Replace('\\', '/');
        }

        /// <summary>
        /// The half of the guarantee that lives on the platform, asked of the platform where it can be.
        /// </summary>
        /// <remarks>
        /// <para>A pipeline that runs <c>sdlc verify-chain</c> is a promise only while branch policy requires
        /// it, and the policy is set on the platform rather than written into the repository -- so a policy
        /// switched off, or never set, changes nothing here. The doctor asks the platform through its own
        /// CLI, signed in as the person running it. Only asked once the pipeline itself is sound: with no
        /// pipeline, the problem already names the policy.</para>
        ///
        /// <para><strong>Three answers.</strong> Required is sound. A platform that answered and requires
        /// nothing is a problem, because the guarantee is not in force. A platform that could not be asked --
        /// no CLI, not signed in, no permission -- is a warning, marked elsewhere as it always was, and says
        /// how to let the doctor ask: the person running it not having signed in is not a broken install.</para>
        ///
        /// <para><strong>On Azure DevOps the policy is also what runs it.</strong> Azure Repos ignores a
        /// <c>pr:</c> trigger in YAML, so the pipeline runs on a pull request only when a build
        /// validation policy names it. Without one it is not an optional check -- it is no check, so the
        /// remedy says so rather than reading the same as GitHub's.</para>
        ///
        /// <para>The reviewer is named too, as a warning rather than a requirement (D-84): who must approve
        /// a pull request is the organisation's, but once a factory writes code overnight, a branch with no
        /// required reviewer takes that code read by nobody but whoever clicks merge.</para>
        /// </remarks>
        /// <param name="platform">The recorded platform. Never local: a local solution has no pipeline.</param>
        /// <param name="integration">The branch cycles merge into.</param>
        /// <param name="answer">What the platform said, or why it was not asked.</param>
        /// <returns>The check.</returns>
        private static Check BranchPolicy(Platform platform, string integration, PolicyAnswer answer)
        {
            string remedy = (platform == Platform.AzureDevOps
                    ? "on Azure DevOps the pipeline runs only from a build validation policy, so without one "
                      + $"nothing runs: Repos > Branches > {integration} and the default branch > Branch policies > Build "
                      + "validation, add the pipeline and make it Required"
                    : $"on GitHub: a branch protection rule or ruleset on {integration} and the default branch that requires the "
                      + $"'{CliBranchPolicies.GitHubCheck}' status check")
                + $". Who must review is yours to set; with no required reviewer on {integration}, code a factory "
                + "wrote reaches it read by nobody but whoever merges it";

            string signIn = platform == Platform.AzureDevOps ? "az login" : "gh auth login";

            return answer.Finding switch
            {
                PolicyFinding.Required => new Check(
                    "branch policy",
                    CheckOutcome.Sound,
                    answer.Detail + ". The default branch and who must review are not asked; both are yours to set"),
                PolicyFinding.NotRequired => new Check("branch policy", CheckOutcome.Problem, answer.Detail, remedy),
                _ => new Check(
                    "branch policy",
                    CheckOutcome.Elsewhere,
                    "the platform holds this, and it could not be asked whether the check is required: " + answer.Detail,
                    remedy + $". Sign in with '{signIn}' and run the doctor again, and it asks for itself"),
            };
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
            "on Claude Code: 'claude plugin details sdlc@ai-sdlc'. Expect Skills (18) and Hooks (2)");
    }
}

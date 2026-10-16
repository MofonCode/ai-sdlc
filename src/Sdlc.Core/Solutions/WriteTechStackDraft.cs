using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Reports;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Copies <c>techstack.md</c> to a draft beside it, for an edit a person renames into place.
/// </summary>
/// <remarks>
/// <para><strong>Why the stack file is a person's to place.</strong> It is one of the two rule sources a
/// plan is read against -- <see cref="Evidence.DeclaredFiles"/> says so in as many words -- and the
/// other one, the solution's own constraints, has always been protected. The reason given there applies
/// here unchanged: a cycle judged by the rule it is changing is the same deadlock as the framework
/// governing itself. <c>CON_15</c> requires a plan to say why it departs from a declared default, and
/// while an agent could write this file a plan could satisfy that rule by adding the row it wanted and
/// then truthfully not departing from anything.</para>
///
/// <para><strong>What protecting it makes <c>CON_15</c> mean.</strong> The rule never asked a plan to
/// change the stack file; it asked a plan to argue. So the only path inside a cycle is now to depart and
/// say what the default would have cost, and declaring a new default becomes a separate deliberate act
/// with a person's hand on it. That is the rule working rather than being satisfied by moving what it
/// reads.</para>
///
/// <para><strong>The draft starts as a byte copy, which is the whole guarantee.</strong> Renaming it can
/// only apply what somebody actually changed -- there is no merge, no regeneration, and nothing this can
/// silently drop. An agent may edit the draft, because only <c>techstack.md</c> is denied and
/// <c>techstack.draft.md</c> is an ordinary file; what an agent cannot do is place it.</para>
///
/// <para><strong>Where there is nothing to copy, the draft is the framework's default.</strong> Adoption
/// does not create the declared stack any more, so the first run of this is where it comes from: the
/// default profile's rows with every shipped stack rule switched on, for a person to cut down to what
/// is true of their solution. That is the one time this proposes rows, and it is safe for the reason a
/// file written straight into place was not -- it is a draft, read before it is placed (D-76).</para>
///
/// <para><strong>And it can be empty on request.</strong> A declared stack with nothing in it is a
/// legitimate answer: nothing pinned, no stack rule in force, every plan choosing freely. Getting
/// there by deleting two tables by hand is how somebody leaves half of one behind, so <c>--empty</c>
/// writes the draft with nothing in it.</para>
///
/// <para><strong>After that it proposes no rows.</strong> Which packages and patterns a solution
/// declares is a judgement about how a team works, and a tool that filled them in would be making that
/// judgement while looking like bookkeeping. What it does instead, on a copy, is name what the stack
/// does not mention -- the rows and rules a later release added -- under the rename command, and add
/// none of it: the draft stays a byte copy, and whoever edits it answers each one (D-93).</para>
/// </remarks>
public static class WriteTechStackDraft
{
    /// <summary>The name the draft is written under, beside the file it is for.</summary>
    public const string DraftFileName = "techstack.draft.md";

    /// <summary>What the menu is filed as under <c>.sdlc/reports/</c>.</summary>
    /// <remarks>
    /// <para>A second file rather than a section of the draft, because the draft is a byte copy and the
    /// rename is what places it: candidates inside it would survive into the one file every plan is read
    /// against, where a plan could reach for one and truthfully not be departing from anything.</para>
    ///
    /// <para><strong>And a report rather than a file beside the draft, which is where this first put
    /// it.</strong> The menu outlives the rename — nothing deletes it — so an untracked copy would sit
    /// in <c>.sdlc/</c> indefinitely, and <c>evidence</c> stages untracked paths with
    /// <c>--intent-to-add</c> before diffing. It would therefore reach every later cycle's diff as a
    /// file no plan declared, which is what <c>CON_07.2</c> fires on. <c>ReportFile</c> already carries
    /// that argument and the directory that ignores itself, and the menu matches its definition exactly:
    /// a copy of what a command printed, worked through rather than read once, and read back by nothing.
    /// </para>
    /// </remarks>
    public const string OptionsReportKind = "stack-options";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteTechStackDraftRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, DraftResult>, Handler>();
    }

    /// <summary>Asks for the stack document to be drafted to disk.</summary>
    /// <param name="Empty">
    /// Whether the draft is written with nothing in it, for a solution that pins nothing. Otherwise it
    /// is a copy of the declared stack, or the framework's default where there is none yet.
    /// </param>
    public sealed record Command(bool Empty = false) : IRequest<DraftResult>;

    private sealed class Handler(SolutionRoot solution, TimeProvider clock)
        : IRequestHandler<Command, DraftResult>
    {
        /// <inheritdoc />
        public async Task<Result<DraftResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string directory = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            string path = Path.Combine(directory, Cycle.TechStackFileName);

            // Adoption is what makes a repository a governed solution, and it no longer writes this
            // file -- so the directory is the test rather than the file. Without it there is no solution
            // for a stack to be declared in.
            if (!File.Exists(SolutionDocument.PathIn(solution.Path)))
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no governed solution at {solution.Path} to declare a stack for. A person runs 'sdlc init' first; this is the step after it.")));
            }

            Start start = request.Empty
                ? Start.Empty
                : File.Exists(path) ? Start.Copy : Start.Default;

            // Read only where the default is being written, because that is the one draft whose
            // contents depend on it: the build-settings rows start on where nothing predates them and
            // off where code does (D-77). A copy is a copy, and an empty draft has no rows to start.
            SolutionOrigin origin = SolutionOrigin.Scaffolded;

            if (start == Start.Default)
            {
                Result<SolutionDocument> document = await SolutionDocument
                    .ReadFile(solution.Path, cancellationToken)
                    .ConfigureAwait(false);

                if (document.IsFailed)
                {
                    return document.ToResult<DraftResult>();
                }

                origin = document.Value.Origin;
            }

            string current = start switch
            {
                Start.Copy => await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                Start.Default => Init.Templates.TechStack(origin),
                _ => string.Empty,
            };

            string draftPath = Path.Combine(directory, DraftFileName);

            await File.WriteAllTextAsync(draftPath, current, cancellationToken).ConfigureAwait(false);

            // The menu, as a report and never over the real file. Written on every run rather than
            // created once: it is derived from the profiles this build ships, so a kept copy would
            // offer answers a later release had changed its mind about -- which is the whole of what a
            // report is, and why the directory it lands in ignores itself.
            string optionsPath = await ReportFile.Write(
                solution.Path,
                OptionsReportKind,
                StackOptions.Render(),
                clock.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);

            return Result.Ok(DraftResult.Drafted(
                current,
                Instructions(
                    path,
                    draftPath,
                    optionsPath,
                    start,
                    origin,
                    start == Start.Copy ? StackCoverage.Of(current) : StackCoverage.Unmentioned.Nothing)));
        }

        /// <summary>What a draft was written from.</summary>
        private enum Start
        {
            /// <summary>A byte copy of the declared stack already in place.</summary>
            Copy,

            /// <summary>The framework's default, because no declared stack is in place yet.</summary>
            Default,

            /// <summary>Nothing, because the person asked for a stack that pins nothing.</summary>
            Empty,
        }

        /// <summary>What the draft starts as, in the words the rename depends on.</summary>
        /// <param name="start">What it was written from.</param>
        /// <param name="origin">Where the solution's code came from.</param>
        /// <returns>The lines that follow the rename command.</returns>
        private static string[] Describe(Start start, SolutionOrigin origin) => start switch
        {
            Start.Copy =>
            [
                "The draft starts as a byte copy of the file, so the rename applies what you changed and",
                "nothing else. Nothing is merged and nothing is regenerated.",
            ],
            Start.Default =>
            [
                "No stack is declared here yet, so the draft is the framework's default: its rows, and",
                "every stack rule it ships switched on. Write 'none' as the choice in a row that is not",
                "true of this solution rather than deleting it, and set a rule to false to decline it --",
                "a row that says no is how a later release knows not to ask again. Renaming it untouched",
                "declares that default.",
                "Until it is renamed into place, no cycle opens.",
                string.Empty,
                .. origin == SolutionOrigin.Adopted
                    ? (string[])
                    [
                        "The build settings rows start false, because this repository held code before the",
                        "framework did: Directory.Build.props makes warnings errors, so switching it on can",
                        "fail a build over files nobody has touched. Set a row to true to take that file,",
                        "and a cycle whose plan declares it brings it. The menu says what each one costs.",
                    ]
                    :
                    [
                        "The build settings rows start true: nothing here predates them, and the solution",
                        "skeleton does not compile without them. They arrive with it, on the first cycle.",
                    ],
            ],
            _ =>
            [
                "The draft is empty, which is what was asked for. Renamed into place it declares that",
                "nothing is pinned: no default for a plan to depart from, no stack rule in force, and",
                "every plan choosing freely. Rows can be added later, the same way.",
            ],
        };

        private static string Instructions(
            string path,
            string draftPath,
            string optionsPath,
            Start start,
            SolutionOrigin origin,
            StackCoverage.Unmentioned unmentioned) => string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"Written to {draftPath}"),
                string.Empty,
                "Make the change in the draft, then place it by renaming it over the real file:",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"    mv -f \"{draftPath}\" \"{path}\""),
                string.Empty,
                .. Describe(start, origin),
                string.Empty,
                .. unmentioned.IsEmpty ? [] : (string[])[.. StackCoverage.Describe(unmentioned, "    "), string.Empty],
                string.Create(CultureInfo.InvariantCulture, $"The menu is at {optionsPath}:"),
                "every concern the shipped profiles know about, what each declares for it, the",
                "alternatives this framework has argued, the stack rules it ships, each build settings",
                "file with what it does and what it costs, and an empty table for a solution starting",
                "over. Open it beside the draft and copy across what you want.",
                string.Empty,
                "It is a report rather than a file in '.sdlc/' itself -- written fresh every run, read back",
                "by nothing, safe to delete, and in a directory that ignores itself so a copy left behind",
                "never reaches a later cycle's diff as a file no plan declared. It is never renamed over",
                "the real file: a declared stack carrying answers this solution has not chosen would let a",
                "plan reach for one and truthfully not be departing from anything.",
                string.Empty,
                "What belongs in a change here is a default this solution is adopting or dropping -- not a",
                "package one cycle happened to need. A plan that reaches for something undeclared satisfies",
                "CON_15 by saying what the declared default would have cost, which is an argument rather",
                "than an edit to this file.",
                string.Empty,
                "Nothing here places it. Every agent write to that file is denied, and the rename is the act.",
            ]);
    }
}

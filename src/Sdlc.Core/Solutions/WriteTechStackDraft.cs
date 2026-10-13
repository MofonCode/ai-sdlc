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
/// <para><strong>It proposes no rows.</strong> Which packages and patterns a solution declares is a
/// judgement about how a team works, and a tool that filled them in would be making that judgement while
/// looking like bookkeeping. The profile's template is where defaults come from, once at creation.</para>
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
    public sealed record Command : IRequest<DraftResult>;

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

            if (!File.Exists(path))
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {path} to draft from. This solution has not been adopted yet, and adoption is what creates it -- from the profile's template, which is the one moment anything but a person writes it.")));
            }

            string current = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            string draftPath = Path.Combine(directory, DraftFileName);

            await File.WriteAllTextAsync(draftPath, current, cancellationToken).ConfigureAwait(false);

            // The menu, as a report and never over the real file. Written on every run rather than
            // created once: it is derived from the profiles this build ships, so a kept copy would
            // offer answers a later release had changed its mind about -- which is the whole of what a
            // report is, and why the directory it lands in ignores itself.
            string optionsPath = await ReportFile.Write(
                solution.Path,
                OptionsReportKind,
                StackOptions.Render(await ProfileOf(cancellationToken).ConfigureAwait(false)),
                clock.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);

            return Result.Ok(DraftResult.Drafted(current, Instructions(path, draftPath, optionsPath)));
        }

        /// <summary>
        /// The profile this solution records, for the menu's empty table.
        /// </summary>
        /// <remarks>
        /// Null when the solution document cannot be read or names a profile nothing answers to. The
        /// draft itself does not depend on it — a person amending one row should not be refused because
        /// another file is wrong — so the menu leaves that section out and names the command that
        /// reports it.
        /// </remarks>
        /// <param name="cancellationToken">Token observed while reading.</param>
        /// <returns>The profile, or null.</returns>
        private async Task<StackProfile?> ProfileOf(CancellationToken cancellationToken)
        {
            Result<SolutionDocument> read = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            return read.IsSuccess ? StackProfiles.Find(read.Value.Profile) : null;
        }

        private static string Instructions(string path, string draftPath, string optionsPath) => string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"Written to {draftPath}"),
                string.Empty,
                "Make the change in the draft, then place it by renaming it over the real file:",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"    mv -f \"{draftPath}\" \"{path}\""),
                string.Empty,
                "The draft starts as a byte copy of the file, so the rename applies what you changed and",
                "nothing else. Nothing is merged and nothing is regenerated.",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"The menu is at {optionsPath}:"),
                "every concern the shipped profiles know about, what each declares for it, the",
                "alternatives this framework has argued, and an empty table for a solution starting over.",
                "Open it beside the draft and copy across what you want.",
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

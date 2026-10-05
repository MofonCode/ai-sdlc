using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Drafts;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Writes a complete <c>solution.md</c> beside the real one, for a person to rename onto it.
/// </summary>
/// <remarks>
/// <para><strong>Why this exists: a block to paste is a block that does not get pasted.</strong>
/// Adoption prints the solution document for a person to place by hand, and in a real adoption it was
/// never placed — the file stayed on its shipped placeholder, "Replace this with a paragraph a
/// newcomer could read", and every plan in that solution was written against a document that said
/// nothing. Nothing failed, because nothing reads the prose; the cost lands on whoever needed to know
/// what the solution was for. A rename is one command and leaves no step to forget.</para>
///
/// <para><strong>The prose already there is carried through verbatim.</strong> That is what makes the
/// rename safe rather than destructive: a person who has written their paragraph gets it back
/// unchanged, so renaming the draft over the real file cannot cost them anything they wrote. Only a
/// placeholder is replaced, and then by a prompt rather than by invented prose — this knows what the
/// keys are and has nothing to say about what the solution is for.</para>
///
/// <para><strong>The four keys are carried through as recorded, never recomputed.</strong> The origin
/// is a historical fact, the standard decides which conventions new work follows, and the platform
/// decides whether a pull-request check governs the merge (D-53). A draft that defaulted any of them
/// would reclassify a solution by being renamed. Changing one is a different act with its own
/// drafter: <c>sdlc draft profile</c>.</para>
///
/// <para><strong>The binary writes the draft, never a skill</strong> — the same rule
/// <see cref="Approvals.WriteApprovalDraft"/> is written under. Every agent write to
/// <c>solution.md</c> stays denied, and nothing here knows how to reach it; the rename is the act,
/// and an <c>sdlc write-solution</c> would be the exception D-09 refuses to create.</para>
/// </remarks>
public static class WriteSolutionDraft
{
    /// <summary>What the shipped placeholder starts with, so a draft can tell it from real prose.</summary>
    public const string Placeholder = "Replace this with a paragraph";

    /// <summary>The name the draft is written under, beside the file it is for.</summary>
    public const string DraftFileName = "solution.draft.md";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteSolutionDraftRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, DraftResult>, Handler>();
    }

    /// <summary>Asks for the solution document to be drafted to disk.</summary>
    public sealed record Command : IRequest<DraftResult>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, DraftResult>
    {
        /// <inheritdoc />
        public async Task<Result<DraftResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string path = SolutionDocument.PathIn(solution.Path);

            if (!File.Exists(path))
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {path} to draft from. This solution has not been adopted yet, and adoption is what creates it.")));
            }

            Result<SolutionDocument> read = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<DraftResult>();
            }

            SolutionDocument document = read.Value;
            string current = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            string body = BodyOf(current);
            bool untouched = body.Contains(Placeholder, StringComparison.Ordinal);

            string draft = string.Join(
                Environment.NewLine,
                [
                    "---",
                    .. SolutionDocument.Keys(
                        document.Profile,
                        document.Origin,
                        document.Standard,
                        document.Platform),
                    "---",
                    string.Empty,
                    untouched ? Prompt : body.TrimEnd(),
                    string.Empty,
                ]);

            string draftPath = Path.Combine(
                Path.GetDirectoryName(path) ?? solution.Path,
                DraftFileName);

            await File.WriteAllTextAsync(draftPath, draft, cancellationToken).ConfigureAwait(false);

            return Result.Ok(DraftResult.Drafted(draft, Instructions(path, draftPath, untouched)));
        }

        /// <summary>
        /// What a draft says where the prose has never been written.
        /// </summary>
        /// <remarks>
        /// A prompt rather than invented prose, and rather than the shipped placeholder again. This
        /// knows the four keys and knows nothing about what the solution is for, so writing a paragraph
        /// here would be the tool asserting something only a person can say — and a plausible-looking
        /// sentence nobody wrote is worse than a blank, because it reads as settled.
        /// </remarks>
        private static string Prompt => string.Join(
            Environment.NewLine,
            [
                "# What this solution is",
                string.Empty,
                "<!-- Replace this comment, and delete the rest of it once you have. Two or three",
                "     sentences a newcomer could read:",
                string.Empty,
                "       what the solution does, and who for;",
                "       what somebody would judge it on that no test checks -- the standard a reviewer",
                "       would send work back for;",
                "       anything every screen or every endpoint is expected to do.",
                string.Empty,
                "     Every plan written in this solution reads this. Nothing else says what good looks",
                "     like here, so a plan that finds a placeholder has been told nothing. -->",
            ]);

        /// <summary>Everything after the frontmatter, which is the person's half of the file.</summary>
        /// <remarks>
        /// Found by the closing fence rather than by parsing, because the whole point is to carry the
        /// body through without understanding it. A file with no frontmatter is returned whole, which
        /// errs toward keeping what somebody wrote.
        /// </remarks>
        private static string BodyOf(string text)
        {
            string normalised = text.ReplaceLineEndings();
            string fence = "---" + Environment.NewLine;

            if (!normalised.StartsWith(fence, StringComparison.Ordinal))
            {
                return normalised;
            }

            int closing = normalised.IndexOf(
                Environment.NewLine + fence,
                StringComparison.Ordinal);

            return closing < 0
                ? normalised
                : normalised[(closing + Environment.NewLine.Length + fence.Length)..].TrimStart();
        }

        private static string Instructions(string path, string draftPath, bool untouched) => string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"Written to {draftPath}"),
                string.Empty,
                "Place it by renaming it over the real file:",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"    mv -f \"{draftPath}\" \"{path}\""),
                string.Empty,
                untouched
                    ? "The prose has never been written, so the draft carries a prompt where it goes. Fill"
                    : "The prose already there is carried through unchanged, so renaming costs you nothing",
                untouched
                    ? "that in before or after you rename -- either way, a plan that reads a placeholder has"
                    : "you wrote. Edit it in the draft or after the rename, whichever you prefer.",
                untouched ? "been told nothing about what good looks like here." : string.Empty,
                string.Empty,
                "The four keys are carried through as recorded and not recomputed, so this cannot",
                "reclassify the solution by being renamed. To change the profile, use 'sdlc draft profile'.",
                string.Empty,
                "Nothing here places it. Every agent write to that file is denied, and the rename is the act.",
            ]);
    }
}

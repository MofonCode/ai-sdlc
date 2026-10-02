using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Drafts;

namespace Sdlc.Core.Solutions;

/// <summary>Asks for the line a person would change to move a solution to another profile.</summary>
/// <param name="Profile">The profile to move to.</param>
public sealed record ProfileTextQuery(string Profile) : IRequest<DraftResult>;

/// <summary>
/// Prints the one line a profile migration changes. Writes nothing.
/// </summary>
/// <remarks>
/// <c>solution.md</c> is protected, so no agent writes it. Protecting the whole file rather than the
/// one key it is safe to change is what keeps every hook rule absolute: per-key protection would be
/// the framework's first conditional rule, and the cost of the absolute version is this one manual
/// line per migration.
/// </remarks>
/// <param name="solutionRoot">The root of the solution being governed.</param>
public sealed class ProfileTextHandler(string solutionRoot)
    : IRequestHandler<ProfileTextQuery, DraftResult>
{
    /// <inheritdoc />
    public async Task<Result<DraftResult>> Handle(
        ProfileTextQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Profile))
        {
            return Result.Fail<DraftResult>(
                "Which profile? Give its package name, as in 'sdlc draft profile dotnet-default'.");
        }

        string path = SolutionDocument.PathIn(solutionRoot);
        Result<SolutionDocument> read = await SolutionDocument
            .ReadFile(solutionRoot, cancellationToken)
            .ConfigureAwait(false);

        if (read.IsFailed)
        {
            return read.ToResult<DraftResult>();
        }

        SolutionDocument solution = read.Value;
        string wanted = request.Profile.Trim();

        if (string.Equals(solution.Profile, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Ok(DraftResult.Refused(string.Create(
                CultureInfo.InvariantCulture,
                $"This solution is already on '{solution.Profile}'. Nothing to change.")));
        }

        // The origin is printed alongside, unchanged, because it is the thing a hand-edit is most
        // likely to disturb and the one field that must never move: it is a historical fact, and it
        // decides whether a profile's defaults bind at all.
        string entry = string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"profile: {wanted}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"origin: {SolutionDocument.NameOf(solution.Origin)}"),
            ]) + Environment.NewLine;

        string instructions = string.Join(
            Environment.NewLine,
            [
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Open {path} and replace its two frontmatter keys with the lines above."),
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The profile moves from '{solution.Profile}' to '{wanted}'. The origin does not change,"),
                "and must not: it says where this code came from, which decides whether a profile's",
                "defaults bind or the existing standard wins. It is printed above so a hand-edit has the",
                "whole block rather than one line of it.",
                string.Empty,
                "A different profile resolves a different set of PRO_ constraints and templates, so run",
                "'sdlc constraints' afterwards to see what now applies.",
                string.Empty,
                "Nothing here writes that file. The hook denies every agent write to it.",
            ]);

        return Result.Ok(DraftResult.Drafted(entry, instructions));
    }
}

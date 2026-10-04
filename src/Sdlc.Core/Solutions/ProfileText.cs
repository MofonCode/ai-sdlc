using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Drafts;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Prints the one line a profile migration changes. Writes nothing.
/// </summary>
/// <remarks>
/// <para><c>solution.md</c> is protected, so no agent writes it. Protecting the whole file rather
/// than the one key it is safe to change is what keeps every hook rule absolute: per-key protection
/// would be the framework's first conditional rule, and the cost of the absolute version is this one
/// manual line per migration.</para>
///
/// <para><strong>The shape, which every operation follows.</strong> One static class per operation,
/// named for what it does. The request nests inside it, so the type reads as
/// <c>ProfileText.Query</c> at every call site and the operation has one name rather than two. The
/// handler is <c>private</c>: nothing in the program can reach it, so the mediator is the only way
/// in and no caller can skip the pipeline. Its registration lives in here with it, which is what
/// makes that possible -- a registration written anywhere else could not name a private type.</para>
///
/// <para>The response is <see cref="DraftResult"/> rather than a nested <c>Response</c>, because the
/// three drafters genuinely produce the same thing and one renderer reads all three.</para>
/// </remarks>
public static class ProfileText
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddProfileTextRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, DraftResult>, Handler>();
    }

    /// <summary>Asks for the line a person would change to move a solution to another profile.</summary>
    /// <param name="Profile">The profile to move to.</param>
    public sealed record Query(string Profile) : IRequest<DraftResult>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, DraftResult>
    {
        public async Task<Result<DraftResult>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Profile))
            {
                return Result.Fail<DraftResult>(
                    "Which profile? Give its package name, as in 'sdlc draft profile dotnet-default'.");
            }

            string path = SolutionDocument.PathIn(solution.Path);
            Result<SolutionDocument> read = await SolutionDocument
                .ReadFile(solution.Path, cancellationToken)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<DraftResult>();
            }

            SolutionDocument document = read.Value;
            string wanted = request.Profile.Trim();

            if (string.Equals(document.Profile, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Ok(DraftResult.Refused(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This solution is already on '{document.Profile}'. Nothing to change.")));
            }

            // Every key is printed, not only the one that changes. The origin is a historical fact that
            // must never move, and the platform decides whether a pull-request check governs the merge --
            // both are things a hand-edit over a partial block would drop, and dropping the platform would
            // silently reclassify a local solution as GitHub and have doctor report a broken install.
            string entry = string.Join(
                Environment.NewLine,
                [
                    string.Create(CultureInfo.InvariantCulture, $"profile: {wanted}"),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"origin: {SolutionDocument.NameOf(document.Origin)}"),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"platform: {SolutionDocument.NameOf(document.Platform)}"),
                ]) + Environment.NewLine;

            string instructions = string.Join(
                Environment.NewLine,
                [
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Open {path} and replace its three frontmatter keys with the lines above."),
                    string.Empty,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The profile moves from '{document.Profile}' to '{wanted}'. The other two do not change,"),
                    "and the origin must not: it says where this code came from, which decides whether a",
                    "profile's defaults bind or the existing standard wins. The platform decides whether a",
                    "pull-request check governs a merge at all. Both are printed above so a hand-edit has the",
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
}

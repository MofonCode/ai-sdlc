using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Writes <c>verify.md</c> from the adjudicator's answer, byte for byte (D-117, D-109).
/// </summary>
/// <remarks>
/// The session recorded the verdict by copying it out of the reader's output, and a session that copies
/// can also repair — which is this session writing the verdict after all. An answer that is not a verify
/// document is refused rather than tidied, so the only way past it is a new adjudication.
/// </remarks>
public static class WriteVerdict
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddWriteVerdictRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>The verdict written.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Path">Where it was written.</param>
    /// <param name="Outcome">The outcome it declares.</param>
    public sealed record Response(Cycle Cycle, string Path, string Outcome);

    /// <summary>Writes the verdict an adjudicator returned.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="From">The adjudicator's answer: the host's result object, or its plain text.</param>
    public sealed record Command(CycleId Id, string From) : IRequest<Response>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no adjudicator's answer at {request.From}."));
            }

            Result<ReaderAnswer> answer = ReaderAnswers.Parse(
                ReaderAnswers.Adjudicate,
                await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false),
                null,
                request.From);

            if (answer.IsFailed)
            {
                return answer.ToResult<Response>();
            }

            string text = answer.Value.Text;
            const string Again = " Start a new adjudication; do not repair this one, because the repair is this session writing the verdict.";

            if (!text.StartsWith("---", StringComparison.Ordinal))
            {
                return Result.Fail<Response>("The adjudicator's answer does not open with a verify document's frontmatter: it has something before it, or is not a verify document." + Again);
            }

            Result<FrontmatterDocument> split = Frontmatter.Read(text, "verify.md");

            if (split.IsFailed || Frontmatter.Key(split.Value.Yaml, "outcome") is not { Length: > 0 } outcome)
            {
                return Result.Fail<Response>("The adjudicator's answer has no `outcome` in its frontmatter." + Again);
            }

            if (ArtifactSections.NumberedItems(text, "Criteria").Count == 0)
            {
                return Result.Fail<Response>("The adjudicator's answer adjudicates no criterion under `## Criteria`." + Again);
            }

            string path = cycle.PathOf(ArtifactKind.Verify);
            await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(cycle, path, outcome));
        }
    }
}

using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Perspectives;

/// <summary>
/// Which perspectives a cycle gets, and why each (D-90).
/// </summary>
/// <remarks>
/// <para><strong>Three sources, in the order a cycle meets them.</strong> Security, operability and
/// maintainability always. Then the ones the intent names in its <c>perspectives</c> key, seen in the interview
/// and covered by the approval's hash — the spec is where perspectives add the most, and declared files first
/// exist in the plan. Then the ones the plan's declared files imply, which the intent missed: those are added,
/// and named as missed, so an interrogation of the plan raises them.</para>
/// <para><strong>Only perspectives that have been written apply.</strong> One that is always on and has no
/// document yet is listed as not yet written, so the gap is visible rather than silently skipped.</para>
/// </remarks>
public static class ActivePerspectives
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddActivePerspectivesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>One perspective a cycle gets, and why.</summary>
    /// <param name="Name">Its name.</param>
    /// <param name="Why">Always on, named by the intent, or which declared file brought it.</param>
    /// <param name="Written">Whether a document for it exists yet.</param>
    /// <param name="MissedByIntent">Whether the plan's files brought it and the intent did not name it.</param>
    public sealed record Active(string Name, string Why, bool Written, bool MissedByIntent);

    /// <summary>Asks which perspectives a cycle gets.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    /// <summary>The perspectives, in the order a reader runs them.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Perspectives">Each one and why.</param>
    public sealed record Response(CycleId Id, IReadOnlyList<Active> Perspectives);

    /// <summary>Reads the names an intent's <c>perspectives</c> key lists.</summary>
    /// <param name="intent">The intent's text.</param>
    /// <returns>The names as written, or none.</returns>
    public static IReadOnlyList<string> NamedBy(string intent)
    {
        Result<FrontmatterDocument> split = Frontmatter.Read(intent, "intent.md");

        if (split.IsFailed || Frontmatter.Key(split.Value.Yaml, "perspectives") is not { } value || string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value.Trim().TrimStart('[').TrimEnd(']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.Trim('\'', '"'))
            .Where(name => name.Length > 0)];
    }

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<IReadOnlyList<Perspective>> library = PerspectiveLibrary.All();

            if (library.IsFailed)
            {
                return library.ToResult<Response>();
            }

            Cycle cycle = new(request.Id, solution.Path);
            string intentPath = cycle.PathOf(ArtifactKind.Intent);
            string planPath = cycle.PathOf(ArtifactKind.Plan);

            IReadOnlyList<string> named = File.Exists(intentPath)
                ? NamedBy(await File.ReadAllTextAsync(intentPath, cancellationToken).ConfigureAwait(false))
                : [];

            IReadOnlyList<string> declared = File.Exists(planPath)
                ? DeclaredFiles.Read(await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false))
                : [];

            HashSet<string> written = new(library.Value.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            List<Active> active = [];

            foreach (string name in Perspective.AlwaysOn)
            {
                active.Add(new Active(name, "always on", written.Contains(name), false));
            }

            foreach (string name in named.Where(n => !active.Any(a => string.Equals(a.Name, n, StringComparison.OrdinalIgnoreCase))))
            {
                active.Add(new Active(name, "named by the intent", written.Contains(name), false));
            }

            foreach (Perspective perspective in library.Value.Where(p => !active.Any(a => a.Name == p.Name)))
            {
                if (declared.FirstOrDefault(perspective.Matches) is { } file)
                {
                    active.Add(new Active(
                        perspective.Name,
                        string.Create(CultureInfo.InvariantCulture, $"the plan declares {file}"),
                        true,
                        true));
                }
            }

            return Result.Ok(new Response(request.Id, active));
        }
    }

    /// <summary>Renders the list for a person and for the interrogate skill.</summary>
    /// <param name="response">The list.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Perspectives for {response.Id}:"));

        foreach (Active active in response.Perspectives)
        {
            string state = active.Written ? string.Empty : "  (not written yet: nothing to run)";
            string missed = active.MissedByIntent ? "  -- the intent did not name it; raise that at plan" : string.Empty;
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {active.Name,-18} {active.Why}{state}{missed}"));
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders one perspective for one stage: the full method and questions for a reader, or the short list for
    /// an author.
    /// </summary>
    /// <param name="perspective">The perspective.</param>
    /// <param name="stage">The stage: intent, spec, plan, build or verify.</param>
    /// <param name="cover">The author's short list rather than the reader's full method.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string RenderStage(Perspective perspective, string stage, bool cover)
    {
        ArgumentNullException.ThrowIfNull(perspective);

        StringBuilder output = new();

        if (cover)
        {
            output.AppendLine(perspective.Title + " -- " + CoverHeading(stage) + ":");
            output.AppendLine(perspective.Cover[stage]);
            return output.ToString();
        }

        output.AppendLine("# " + perspective.Title + " -- " + stage);
        output.AppendLine();
        output.AppendLine(perspective.Purpose);
        output.AppendLine();
        output.AppendLine("## Method");
        output.AppendLine();
        output.AppendLine(perspective.Method);
        output.AppendLine();
        output.AppendLine("## Ask of this " + stage);
        output.AppendLine();
        output.AppendLine(perspective.Questions[stage]);
        output.AppendLine();
        output.AppendLine("## Rules it may block on");
        output.AppendLine();
        output.AppendLine(perspective.Rules);
        output.AppendLine();
        output.AppendLine("## Evidence it needs");
        output.AppendLine();
        output.AppendLine(perspective.Evidence);
        return output.ToString();
    }

    // A cover list steers within what the intent asked for and never widens it (D-90): the intent asks the
    // person rather than writing the list in, and the spec writes only what the intent supports, because a
    // list that arrives with every cycle is not the person asking for it (CON_05).
    private static string CoverHeading(string stage) => stage switch
    {
        "intent" => "ask the person about this in the intent",
        "spec" => "cover this in the spec where the intent asks for it, and name the rest in Out of scope",
        _ => "cover this in the " + stage,
    };
}

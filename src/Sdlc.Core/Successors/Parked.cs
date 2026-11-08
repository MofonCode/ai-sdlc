using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Successors;

/// <summary>
/// A cycle whose plan turned out to need another's unmerged code, waiting with its spec and plan kept (D-145).
/// </summary>
/// <remarks>
/// <para><strong>The backlog's second depth.</strong> A successor is a piece never planned: an outcome and a why.
/// A parked cycle was planned, and the plan-time claim check found it needs a file only another plan creates. Its
/// spec and plan are paid-for work, so it keeps them and its id, and waits.</para>
/// <para><strong>Its file records only what cannot be worked out.</strong> What it waits on, the commit its plan was
/// written against, and why. Whether it is ready is worked out from the record on every read, as a successor's is.</para>
/// </remarks>
/// <param name="WaitsOn">What it waits for, in the order written: cycle ids, or <c>successor:&lt;slug&gt;</c>.</param>
/// <param name="Base">The commit its plan was written against, when it was recorded.</param>
/// <param name="Why">Why it was parked, in a sentence.</param>
public sealed record Parked(IReadOnlyList<SuccessorReference> WaitsOn, string? Base, string Why)
{
    /// <summary>The file in a cycle's folder whose presence parks it.</summary>
    public const string FileName = "parked.md";

    /// <summary>Where a cycle's parked file lives.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The path.</returns>
    public static string PathOf(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        return Path.Combine(cycle.Directory, FileName);
    }

    /// <summary>Reads a parked file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>It, or every problem with it.</returns>
    public static Result<Parked> Parse(string text)
    {
        Result<FrontmatterDocument> split = Frontmatter.Read(text, FileName);

        if (split.IsFailed)
        {
            return split.ToResult<Parked>();
        }

        List<SuccessorReference> waits = [];
        List<string> problems = [];

        foreach (string entry in (Frontmatter.Key(split.Value.Yaml, "waits_on") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (SuccessorReference.TryParse(entry, out SuccessorReference? reference))
            {
                waits.Add(reference);
            }
            else
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"'{entry}' in waits_on: is neither a cycle id nor successor:<slug>."));
            }
        }

        if (waits.Count == 0 && problems.Count == 0)
        {
            problems.Add("waits_on: is missing or empty, so nothing says what it waits for.");
        }

        return problems.Count > 0
            ? Result.Fail<Parked>(problems)
            : Result.Ok(new Parked(waits, Frontmatter.Key(split.Value.Yaml, "base"), split.Value.Body.Trim()));
    }

    /// <summary>The file's text.</summary>
    /// <returns>Frontmatter and the reason, ending in a newline.</returns>
    public string Text()
    {
        StringBuilder text = new();
        text.Append("---\nwaits_on: ").Append(string.Join(", ", WaitsOn)).Append('\n');

        if (Base is not null)
        {
            text.Append("base: ").Append(Base).Append('\n');
        }

        text.Append("---\n\n").Append(Why.Trim()).Append('\n');
        return text.ToString();
    }
}

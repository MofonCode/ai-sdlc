using System.Globalization;
using FluentResults;

namespace Sdlc.Core.Perspectives;

/// <summary>
/// The perspectives the binary carries, one embedded document each (D-90).
/// </summary>
/// <remarks>
/// Giving the framework a perspective is adding a file under <c>Perspectives/</c>, as giving a profile its
/// first rule is: the resource names are the list, so there is no second list to keep in step.
/// </remarks>
public static class PerspectiveLibrary
{
    private const string Prefix = "Sdlc.Core.Perspectives.";

    /// <summary>Every perspective written so far, by name.</summary>
    /// <returns>The perspectives, or what is wrong with a document.</returns>
    public static Result<IReadOnlyList<Perspective>> All()
    {
        List<Perspective> all = [];
        List<IError> errors = [];

        foreach (string resource in typeof(PerspectiveLibrary).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal))
        {
            using Stream stream = typeof(PerspectiveLibrary).Assembly.GetManifestResourceStream(resource)!;
            using StreamReader reader = new(stream);
            Result<Perspective> read = Perspective.Read(reader.ReadToEnd(), resource[Prefix.Length..]);

            if (read.IsFailed)
            {
                errors.AddRange(read.Errors);
                continue;
            }

            all.Add(read.Value);
        }

        return errors.Count > 0 ? Result.Fail<IReadOnlyList<Perspective>>(errors) : Result.Ok<IReadOnlyList<Perspective>>(all);
    }

    /// <summary>One perspective by name.</summary>
    /// <param name="name">Its name.</param>
    /// <returns>The perspective, or a failure naming the ones there are.</returns>
    public static Result<Perspective> Find(string name)
    {
        Result<IReadOnlyList<Perspective>> all = All();

        if (all.IsFailed)
        {
            return all.ToResult<Perspective>();
        }

        return all.Value.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found
            ? Result.Ok(found)
            : Result.Fail<Perspective>(string.Create(
                CultureInfo.InvariantCulture,
                $"There is no perspective called '{name}'. Written so far: {string.Join(", ", all.Value.Select(p => p.Name))}."));
    }
}

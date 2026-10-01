using System.Globalization;

namespace Sdlc.Core.Constraints;

/// <summary>One tier's constraint document, as text with the path it came from.</summary>
/// <param name="Tier">The tier the document supplies.</param>
/// <param name="Path">Where it was read from. Carried so a parse failure can name it.</param>
/// <param name="Text">The document's contents.</param>
public sealed record ConstraintDocument(ConstraintTier Tier, string Path, string Text);

/// <summary>Supplies the constraint documents in force, in resolution order.</summary>
public interface IConstraintDocuments
{
    /// <summary>
    /// Reads every tier's document that exists, global first, then profile, then solution.
    /// </summary>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The documents found. A tier with no document is absent rather than empty.</returns>
    Task<IReadOnlyList<ConstraintDocument>> Enumerate(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the tier documents from disk.
/// </summary>
/// <remarks>
/// The effective set is assembled at run time and never copied into a solution: a local copy goes
/// stale and then disagrees with the live set, with nothing to say which one binds.
/// </remarks>
/// <param name="libraryPath">The framework's own <c>CONSTRAINTS.md</c>.</param>
/// <param name="solutionRoot">
/// The root of the solution being worked on. Its <c>.sdlc/constraints.md</c> supplies the solution
/// tier, and is absent until someone writes the first local rule.
/// </param>
/// <param name="profilePath">
/// The profile package's constraint document, when a profile supplies one. No profile package ships
/// yet, so this is normally null — the tier exists in the model so that adding one later is a new
/// source rather than a redesign.
/// </param>
public sealed class FileConstraintDocuments(
    string libraryPath,
    string solutionRoot,
    string? profilePath = null) : IConstraintDocuments
{
    /// <summary>The file a solution's own constraints are read from, inside its .sdlc directory.</summary>
    public const string SolutionFileName = "constraints.md";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConstraintDocument>> Enumerate(CancellationToken cancellationToken)
    {
        List<ConstraintDocument> documents = [];

        await Add(ConstraintTier.Global, libraryPath, required: true).ConfigureAwait(false);
        await Add(ConstraintTier.Profile, profilePath, required: false).ConfigureAwait(false);
        await Add(
            ConstraintTier.Solution,
            Path.Combine(solutionRoot, Cycles.Cycle.SdlcDirectoryName, SolutionFileName),
            required: false).ConfigureAwait(false);

        return documents;

        async Task Add(ConstraintTier tier, string? path, bool required)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                if (required)
                {
                    throw new FileNotFoundException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"The {tier} constraint library is missing. Expected it at '{path}'."),
                        path);
                }

                return;
            }

            string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            documents.Add(new ConstraintDocument(tier, path, text));
        }
    }
}

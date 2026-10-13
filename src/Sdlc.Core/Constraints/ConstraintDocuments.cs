using System.Globalization;
using FluentResults;

namespace Sdlc.Core.Constraints;

/// <summary>One tier's constraint document, as text with where it came from.</summary>
/// <param name="Tier">The tier the document supplies.</param>
/// <param name="Source">
/// Where it was read from, carried so a parse failure and <c>sdlc constraints</c> can name it. Usually
/// a path, and not always one: the global tier is embedded in the binary unless a file overrides it,
/// and then this reads <c>embedded in sdlc 0.9.0</c>. Called a source rather than a path for that
/// reason -- a member named Path holding something that is not one is how a caller ends up passing it
/// to File.Exists. D-41.
/// </param>
/// <param name="Text">The document's contents.</param>
public sealed record ConstraintDocument(ConstraintTier Tier, string Source, string Text);

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
/// <param name="libraryPath">
/// The framework's own <c>CONSTRAINTS.md</c>, when a file supplies it. Null means the copy the binary
/// carries, which is the normal case for an installed tool -- see <see cref="ShippedLibrary"/>.
/// </param>
/// <param name="solutionRoot">
/// The root of the solution being worked on. Its <c>.sdlc/constraints.md</c> supplies the solution
/// tier, and is absent until someone writes the first local rule.
/// </param>
/// <param name="profilePath">
/// A profile constraint document supplied as a file, which wins over the one the binary carries. Null
/// is the normal case: the profile tier resolves from the profile <c>solution.md</c> names, out of the
/// documents embedded in this build — see <see cref="ShippedProfileLibrary"/>. The parameter stays
/// because an external profile package, if one is ever built, arrives as a path.
/// </param>
public sealed class FileConstraintDocuments(
    string? libraryPath,
    string solutionRoot,
    string? profilePath = null) : IConstraintDocuments
{
    /// <summary>
    /// The file a solution's own constraints are read from, inside its .sdlc directory.
    /// </summary>
    /// <remarks>
    /// Named for the constraints rather than for the solution, because <c>solution.md</c> is a
    /// different protected file in the same directory and the same code path guards both. A constant
    /// called SolutionFileName sitting next to one is how the wrong file ends up guarded.
    /// </remarks>
    public const string SolutionConstraintsFileName = "constraints.md";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConstraintDocument>> Enumerate(CancellationToken cancellationToken)
    {
        List<ConstraintDocument> documents = [];

        // The global tier always resolves: a path when one was found, and otherwise the copy the
        // binary carries. There is no third outcome, which is the whole of D-41 -- the gate used to
        // refuse to open at all when no file was beside it or above it.
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            documents.Add(new ConstraintDocument(
                ConstraintTier.Global,
                ShippedLibrary.Source,
                ShippedLibrary.Text()));
        }
        else
        {
            await Add(ConstraintTier.Global, libraryPath, required: true).ConfigureAwait(false);
        }

        // The profile tier, which resolved to nothing at all until this read the profile's name: the
        // key existed, the parameter existed, and nothing ever passed it -- so `sdlc constraints`
        // printed the same set on every profile while three documents told a person it would change.
        // A path still wins, for the seam an external profile package would use; otherwise the rules
        // come from the profile solution.md names, out of this build.
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            await Add(ConstraintTier.Profile, profilePath, required: false).ConfigureAwait(false);
        }
        else
        {
            await AddShippedProfile(cancellationToken).ConfigureAwait(false);
        }

        await Add(
            ConstraintTier.Solution,
            Path.Combine(solutionRoot, Cycles.Cycle.SdlcDirectoryName, SolutionConstraintsFileName),
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
                    // Reached only when a caller passed a path to a file that is not there. The
                    // command line checks SDLC_CONSTRAINTS before it builds the container and
                    // refuses by name, so this is a programmer's mistake rather than a person's.
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

        async Task AddShippedProfile(CancellationToken token)
        {
            // An unreadable or absent solution document leaves the tier absent rather than failing.
            // Every caller of this is a gate about to judge an artifact, and a solution whose own
            // document cannot be read has a larger problem that `sdlc doctor` reports by name -- while
            // a throw here would turn it into every command failing with a message about constraints.
            Result<Solutions.SolutionDocument> read = await Solutions.SolutionDocument
                .ReadFile(solutionRoot, token)
                .ConfigureAwait(false);

            if (read.IsFailed)
            {
                return;
            }

            if (ShippedProfileLibrary.Text(read.Value.Profile) is not { } text)
            {
                return;
            }

            documents.Add(new ConstraintDocument(
                ConstraintTier.Profile,
                ShippedProfileLibrary.Source(read.Value.Profile),
                text));
        }
    }
}

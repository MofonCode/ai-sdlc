using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Runs;

/// <summary>
/// A cycle a run took out rather than merge: its build stopped, its merge conflicted, or the suite failed on the merged
/// result (D-235).
/// </summary>
/// <remarks>
/// Taken out, not abandoned: its branch is pushed with its plan, code and findings, a harvest still reads it, and a person
/// starts a wave or a series from it later. The run carries on without it.
/// </remarks>
/// <param name="Cycle">The cycle.</param>
/// <param name="Wave">The wave it was in; 1 for a wave that stands alone.</param>
/// <param name="Reason">Why, in a sentence.</param>
/// <param name="Branch">The branch its work is on.</param>
/// <param name="Pushed">Whether that branch reached the remote.</param>
public sealed record TakenOut(string Cycle, int Wave, string Reason, string Branch, bool Pushed);

/// <summary>
/// A wave's own record, <c>.sdlc/waves/&lt;id&gt;/wave.json</c>, for a wave that stands alone: how many of its cycles
/// build at once, and what it took out (D-235). A wave inside a series is recorded in the series' record instead.
/// </summary>
/// <param name="Id">The wave's id.</param>
/// <param name="Parallel">How many of its cycles build at once.</param>
/// <param name="TakenOut">What it took out, in the order it did.</param>
public sealed record WaveRecord(string Id, int Parallel, IReadOnlyList<TakenOut> TakenOut)
{
    /// <summary>How many cycles build at once when nobody says (D-236).</summary>
    public const int DefaultParallel = 2;

    /// <summary>The waves' folder's name, under <c>.sdlc/</c>.</summary>
    public const string DirectoryName = "waves";

    /// <summary>The record's file name, in the wave's folder.</summary>
    public const string FileName = "wave.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>A wave's folder, relative to the root.</summary>
    /// <param name="id">The wave's id.</param>
    /// <returns>The path, with forward slashes.</returns>
    public static string RelativeDirectory(string id) => Cycle.SdlcDirectoryName + "/" + DirectoryName + "/" + id;

    /// <summary>Where a wave's record lives.</summary>
    /// <param name="solutionRoot">The working copy.</param>
    /// <param name="id">The wave's id.</param>
    /// <returns>The full path.</returns>
    public static string PathIn(string solutionRoot, string id) =>
        Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName, id, FileName);

    /// <summary>Reads a wave's record, or a new one when it has none yet.</summary>
    /// <param name="solutionRoot">The working copy.</param>
    /// <param name="id">The wave's id.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The record, or why what is there cannot be read.</returns>
    public static async Task<Result<WaveRecord>> Read(string solutionRoot, string id, CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot, id);

        if (!File.Exists(path))
        {
            return Result.Ok(new WaveRecord(id, DefaultParallel, []));
        }

        try
        {
            WaveRecord? read = JsonSerializer.Deserialize<WaveRecord>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), _options);

            return read is null
                ? Result.Fail<WaveRecord>(path + ": the wave record is empty.")
                : Result.Ok(read with { Parallel = read.Parallel < 1 ? DefaultParallel : read.Parallel, TakenOut = read.TakenOut ?? [] });
        }
        catch (JsonException error)
        {
            return Result.Fail<WaveRecord>(path + ": the wave record does not parse: " + error.Message);
        }
    }

    /// <summary>Writes the record into its folder.</summary>
    /// <param name="solutionRoot">The working copy.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>A task.</returns>
    public async Task Write(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot, Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(this, _options) + "\n", cancellationToken).ConfigureAwait(false);
    }
}

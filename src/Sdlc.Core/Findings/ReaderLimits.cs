using System.Globalization;

namespace Sdlc.Core.Findings;

/// <summary>How the readers run on this machine: how many at once, and whether each keeps a trace (D-214).</summary>
/// <param name="Parallel">The most readers running at once.</param>
/// <param name="Trace">Whether each reader's host writes a debug log beside its prompt.</param>
/// <remarks>
/// Both are the machine's, never the solution's: what a run can start at once is bounded by the account it runs on,
/// and a trace is for the person looking into one run.
/// </remarks>
public sealed record ReaderLimits(int Parallel, bool Trace)
{
    /// <summary>The variable that sets how many readers run at once.</summary>
    public const string ParallelVariable = "SDLC_READERS";

    /// <summary>The variable that, set to anything but empty or <c>0</c>, has every reader keep a trace.</summary>
    public const string TraceVariable = "SDLC_TRACE";

    /// <summary>How many run at once when nothing says otherwise: every reader of a round, never all of a harvest.</summary>
    public const int DefaultParallel = 8;

    /// <summary>The limits when nothing is set.</summary>
    public static ReaderLimits Default { get; } = new(DefaultParallel, Trace: false);

    /// <summary>Reads the limits from the environment.</summary>
    /// <param name="environment">Reads one variable, or null when it is unset.</param>
    /// <returns>The limits; a count that is not a whole number above zero is the default.</returns>
    public static ReaderLimits From(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        int parallel = int.TryParse(environment(ParallelVariable), NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0
            ? count
            : DefaultParallel;
        bool trace = environment(TraceVariable) is { Length: > 0 } set && set != "0";

        return new ReaderLimits(parallel, trace);
    }
}

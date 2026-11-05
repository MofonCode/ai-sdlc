namespace Sdlc.Cli;

/// <summary>
/// What the gate's exit status means.
/// </summary>
/// <remarks>
/// A hook, a git hook and a pipeline all read these, so they are a contract rather than an
/// implementation detail. The distinction that earns its keep is <see cref="Refused"/> against
/// <see cref="Failed"/>: a refusal is the gate working, and a failure is the gate broken. A caller
/// that cannot tell them apart treats its own bug as a governed stop.
/// </remarks>
internal static class ExitCode
{
    /// <summary>The command did what was asked.</summary>
    public const int Ok = 0;

    /// <summary>A rule refused what was asked. The gate working as intended.</summary>
    public const int Refused = 1;

    /// <summary>The command line could not be understood.</summary>
    public const int Usage = 2;

    /// <summary>The gate itself failed. Never a governed stop.</summary>
    public const int Failed = 3;
}

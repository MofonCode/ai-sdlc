using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Cli;
using Sdlc.Core;
using Sdlc.Core.Constraints;

// Constraint text is passed through verbatim, so the stream has to carry what the library holds.
// On Windows the default is the console codepage, which turns an em dash into a question mark - and
// a session judging against mangled text would cite mangled text. Setting this throws on a handle
// that cannot take it, and a failure here is cosmetic rather than a reason not to run.
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // A redirected or closed handle. The gate still works; the dashes may not look right.
}

var line = CommandLine.Parse(args);

if (line.Verb is "" or "help")
{
    await Console.Out.WriteAsync(Usage.Text).ConfigureAwait(false);
    return line.Verb.Length == 0 ? ExitCode.Usage : ExitCode.Ok;
}

try
{
    return line.Verb switch
    {
        "constraints" => await Constraints(line).ConfigureAwait(false),
        _ => await Unknown(line.Verb).ConfigureAwait(false),
    };
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    // The gate could not read what it needed. That is the gate failing rather than a rule refusing,
    // and the exit code says which, so a hook does not report a broken tool as a governed stop.
    await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
    return ExitCode.Failed;
}

static async Task<int> Constraints(CommandLine line)
{
    string solutionRoot = line.TryOption("solution", out string? root)
        ? root
        : Directory.GetCurrentDirectory();

    if (!Locate.Library(out string? libraryPath, out string? why))
    {
        await Console.Error.WriteLineAsync(why).ConfigureAwait(false);
        return ExitCode.Failed;
    }

    ServiceProvider provider = new ServiceCollection()
        .AddSdlc(libraryPath, solutionRoot)
        .BuildServiceProvider();

    await using (provider.ConfigureAwait(false))
    {
        Mediator mediator = provider.GetRequiredService<Mediator>();
        line.TryOption("for", out string? artifact);

        Result<ConstraintSet> result = await mediator
            .Send<EffectiveConstraintsQuery, ConstraintSet>(new EffectiveConstraintsQuery(artifact))
            .ConfigureAwait(false);

        if (result.IsFailed)
        {
            foreach (IError error in result.Errors)
            {
                await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
            }

            return ExitCode.Refused;
        }

        ConstraintDetail detail = line.HasOption("brief")
            ? ConstraintDetail.Brief
            : ConstraintDetail.Full;

        await Console.Out.WriteAsync(ConstraintRenderer.Render(result.Value, detail, artifact))
            .ConfigureAwait(false);

        return ExitCode.Ok;
    }
}

static async Task<int> Unknown(string verb)
{
    await Console.Error.WriteLineAsync($"Unknown command '{verb}'.").ConfigureAwait(false);
    await Console.Error.WriteAsync(Usage.Text).ConfigureAwait(false);
    return ExitCode.Usage;
}

using FluentResults;
using Sdlc.Core.Harvest;
using Sdlc.FrameworkHarvest;

// Reads the exports solutions handed the framework -- 'sdlc harvest framework --out <file>' -- and prints them read
// together: the notes grouped by the part of the framework they are about, and the closed cycles' numbers by the
// release they ran on. Read-only, and it writes nowhere but stdout or --out.
//
// Why this exists: what a solution's harvest found about the framework was read by hand, from one solution's cycles,
// before they were closed. Exports outlive the cycles and come from any solution that chooses to send one (D-204).
List<string> inputs = [];
string? output = null;

for (int at = 0; at < args.Length; at++)
{
    if (args[at] == "--out" && at + 1 < args.Length)
    {
        output = Path.GetFullPath(args[++at]);
    }
    else
    {
        inputs.Add(args[at]);
    }
}

if (inputs.Count == 0)
{
    await Console.Error.WriteLineAsync("framework-harvest <export.json | directory>... [--out <file>]: the files 'sdlc harvest framework --out' wrote, or directories holding them.").ConfigureAwait(false);
    return 2;
}

List<(string Source, FrameworkExport Export)> exports = [];

foreach (string path in inputs.SelectMany(input => Directory.Exists(input) ? Directory.EnumerateFiles(input, "*.json", SearchOption.AllDirectories) : [input]).Order(StringComparer.Ordinal))
{
    Result<FrameworkExport> read = FrameworkExport.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false), path);

    if (read.IsFailed)
    {
        await Console.Error.WriteLineAsync(read.Errors[0].Message).ConfigureAwait(false);
        return 1;
    }

    exports.Add((Path.GetFileName(path), read.Value));
}

string report = Reading.Render(exports);

if (output is null)
{
    await Console.Out.WriteAsync(report).ConfigureAwait(false);
}
else
{
    await File.WriteAllTextAsync(output, report).ConfigureAwait(false);
    await Console.Out.WriteLineAsync("Written to " + output).ConfigureAwait(false);
}

return 0;

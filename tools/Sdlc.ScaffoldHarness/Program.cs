using System.Globalization;
using Sdlc.Core.Cycles;
using Sdlc.Core.Init;

// Writes what a governed solution looks like on its first cycle, so that `dotnet build` and
// `dotnet test` can be run against it.
//
// Why this exists: every test in Sdlc.Tests asserts STRINGS in the templates, so a template that
// generates code which does not compile passes the whole suite. That is not hypothetical -- the
// change that split the contracts project out shipped a `--` inside an XML comment, which is illegal
// and kills the generated solution's first build, and then a CA1859 that warnings-as-errors turns
// into an error. Neither was visible until a solution was generated and built by hand.
//
// It writes both halves, because neither compiles without the other: the stack files adoption writes
// (which carry the analyzer policy and the package versions) and the skeleton `sdlc scaffold` writes
// behind an approved plan.
if (args.Length is 0 or > 2 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine(
        "usage: scaffold-harness <directory> [solution-name]\n\n"
        + "Writes a scaffolded solution into <directory>, which must not already hold one.\n"
        + "Then build it:  dotnet build <directory>/<name>.slnx\n"
        + "          and:  dotnet test  <directory>/<name>.slnx");

    return 2;
}

string target = Path.GetFullPath(args[0]);
string name = args.Length == 2 ? Scaffold.NameFrom(args[1]) : "Demo";

// Refuse rather than merge into whatever is there. A harness that wrote half a solution over an
// existing one would produce a build failure nobody could attribute.
if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
{
    Console.Error.WriteLine(
        $"{target} is not empty. Point this at a directory that does not exist, or delete that one: a "
        + "scaffold written over existing files fails for a reason nobody can read.");

    return 2;
}

Directory.CreateDirectory(target);

// The architecture rules the skeleton ships anchor the repository root on a .sdlc/ directory beside
// src/, so a generated solution needs one or RepoFiles throws rather than scanning a partial tree.
Directory.CreateDirectory(Path.Combine(target, Cycle.SdlcDirectoryName));

int written = 0;

// What `init` writes into a greenfield solution: the files that constrain how code is written. They
// carry warnings-as-errors, the analyzer level and the central package versions, so the skeleton does
// not compile -- or does not compile under the policy a consumer gets -- without them.
foreach ((string path, string contents) in Stack())
{
    Write(path, contents);
}

// And the skeleton itself, which `sdlc scaffold <id>` writes once a plan declaring it is approved.
foreach ((string path, string contents) in Scaffold.Files(name))
{
    Write(path, contents);
}

Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"Wrote {written} files for solution '{name}' to {target}"));

Console.WriteLine($"  dotnet build \"{Path.Combine(target, name + ".slnx")}\"");
Console.WriteLine($"  dotnet test  \"{Path.Combine(target, name + ".slnx")}\"");

return 0;

void Write(string relative, string contents)
{
    string full = Path.Combine(target, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    File.WriteAllText(full, contents);
    written++;
}

// Asked for rather than listed. This file used to carry its own copy of the five, with nothing
// asserting the two agreed -- so a sixth file added to adoption would have left this harness building
// a solution that no longer matched what `sdlc init` produces: the harness passing and the real thing
// broken, which is the failure it was built to prevent, one level up. F-22.
static IEnumerable<(string Path, string Contents)> Stack() => Initialise.StackFiles();

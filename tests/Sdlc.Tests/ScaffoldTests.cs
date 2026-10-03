using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The solution a scaffolded repository starts as.
/// </summary>
/// <remarks>
/// The templates are embedded resources, which is a class of thing that goes missing quietly. It
/// already did: MSBuild infers a resource's culture from the second-to-last extension and <c>cs</c> is
/// the code for Czech, so <c>RepoFiles.cs.template</c> was taken for a Czech satellite and seven of
/// eleven templates vanished into a <c>cs/</c> assembly. The only symptom was a scaffold that threw
/// partway through writing files. <see cref="Every_template_the_scaffold_names_is_embedded"/> is what
/// turns that into a failing test.
/// </remarks>
[TestClass]
public sealed class ScaffoldTests
{
    private const string Name = "Demo";

    [TestMethod]
    public void Every_template_the_scaffold_names_is_embedded()
    {
        // Reading every file is the assertion: Scaffold.Files throws by name for a template that is
        // not in the build, so this passing means all eleven resolved.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        Assert.AreEqual(13, files.Count, "A template added or removed changes this number deliberately.");

        foreach ((string path, string contents) in files)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(contents),
                path + " came out empty, which means its resource resolved to nothing.");
        }
    }

    [TestMethod]
    public void No_file_the_scaffold_writes_still_carries_the_token()
    {
        // A surviving token is a solution that does not compile, and it would survive silently: the
        // token is legal in a comment and in a string.
        foreach ((string path, string contents) in Scaffold.Files(Name))
        {
            Assert.IsFalse(
                contents.Contains("__SOLUTION__", StringComparison.Ordinal),
                path + " still carries the name token, so it was never substituted.");
        }
    }

    [TestMethod]
    public void The_scaffold_writes_a_solution_file_at_the_root_and_a_project_under_each_scanned_directory()
    {
        // The solution file at the ROOT is the hard-won part. An earlier repository put it under src/,
        // which is what made its architecture tests resolve the root to src/, scan two directories that
        // did not exist, inspect zero files and pass forever.
        string[] paths = [.. Scaffold.Files(Name).Select(file => file.Key.Replace('\\', '/'))];

        CollectionAssert.Contains(paths, "Demo.slnx");

        Assert.IsTrue(paths.Any(path => path.StartsWith("src/", StringComparison.Ordinal)));
        Assert.IsTrue(paths.Any(path => path.StartsWith("tests/Demo.Tests/", StringComparison.Ordinal)));
        Assert.IsTrue(paths.Any(path => path.StartsWith("tests/Demo.Architecture.Tests/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void The_architecture_tests_ship_with_something_to_police()
    {
        // Their own guard throws when a scan finds nothing, so shipping them into an empty repository
        // would fail on the first run. That is why the reference slice and the rules are one scaffold
        // rather than two features.
        string[] paths = [.. Scaffold.Files(Name).Select(file => file.Key.Replace('\\', '/'))];

        Assert.IsTrue(
            paths.Contains("src/Demo.Core/Demo.Core.csproj"),
            "The rules need a project to read, and RepoFiles throws rather than passing on an empty scan.");

        Assert.IsTrue(
            paths.Contains("src/Demo.Core/Greetings/Greet.cs"),
            "The reference slice is the most effective thing a profile ships: an agent copies the code "
            + "beside it far more reliably than it follows an instruction.");
    }

    [TestMethod]
    public void The_reference_slice_is_written_in_the_operation_shape()
    {
        string slice = Scaffold.Files(Name).Single(file => file.Key.EndsWith("Greet.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(slice, "public static class Greet");
        StringAssert.Contains(slice, "public sealed record Query(");
        StringAssert.Contains(slice, "public sealed record Response(");
        StringAssert.Contains(
            slice,
            "private sealed class Handler",
            "A private handler is what makes the mediator the only way in, and the registration can "
            + "only live inside the holder because of it.");
        StringAssert.Contains(slice, "AddGreetRequestHandler");
    }

    [TestMethod]
    public void The_scaffold_is_pinned_to_the_kernel_version_the_slice_is_written_against()
    {
        // The slice uses ResultEnvelope, ToUserMessage and ToLogString. Pinned below the version that
        // introduced them, the scaffold does not compile; pinned above what is published, it does not
        // restore. Both are silent until somebody adopts.
        string packages = Templates.PackageProps();

        StringAssert.Contains(packages, "<PackageVersion Include=\"MofonCode.Core\" Version=\"" + Templates.KernelVersion + "\" />");

        string tests = Scaffold.Files(Name)
            .Single(file => file.Key.EndsWith("GreetTests.cs", StringComparison.Ordinal)).Value;

        foreach (string member in (string[])["ToLogString", "ToUserMessage", "ToEnvelope"])
        {
            StringAssert.Contains(
                tests,
                member,
                member + " arrived in MofonCode.Core 0.2.0, so the pin above has to be at least that.");
        }
    }

    [TestMethod]
    public void Validation_runs_as_a_behaviour_rather_than_as_a_guard_in_a_handler()
    {
        // In the pipeline it runs for every operation that has a validator, so no handler has to
        // remember it and no caller can reach a handler without it. Proven in the scaffolded solution
        // by unregistering the behaviour and watching two of its own tests fail - the handler does not
        // check the name, so a failure there can only have come from the validator.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        string slice = files.Single(file => file.Key.EndsWith("Greet.cs", StringComparison.Ordinal)).Value;
        StringAssert.Contains(slice, "private sealed class Validator : AbstractValidator<Query>");
        StringAssert.Contains(
            slice,
            "AddTransient<IValidator<Query>, Validator>",
            "The holder registers its validator beside its handler, which is what lets the validator "
            + "be private too.");

        string services = files.Single(file => file.Key.EndsWith("Services.cs", StringComparison.Ordinal)).Value;
        StringAssert.Contains(
            services,
            "typeof(IRequestBehavior<,>), typeof(ValidationBehavior<,>)",
            "An open generic, so an operation gets validation by declaring a validator and nothing else.");

        string behaviour = files
            .Single(file => file.Key.EndsWith("ValidationBehavior.cs", StringComparison.Ordinal)).Value;
        StringAssert.Contains(
            behaviour,
            "new Error(failure.ErrorMessage)",
            "An ordinary error rather than an exceptional one: a validation message was written for "
            + "whoever typed the input, so the envelope has to pass it through rather than replace it.");
    }

    [TestMethod]
    public void This_repository_bans_everything_the_profile_it_ships_bans()
    {
        // The framework held consumers to a rule it did not run on itself: the profile shipped the
        // clock ban and ai-sdlc had no banned list at all, so both drafters that stamp a person's
        // record read the clock from a static. Nothing could see it, in a repository whose whole
        // argument is that a rule nobody enforces is one somebody reaches for when it suits them.
        //
        // So this is the rule about the rules. A ban added to the template from here on is a ban this
        // repository takes too, or this fails and says which one -- and it compares the symbols rather
        // than the files, because the reasons are addressed to different readers and the framework may
        // ban more of its own than it imposes on anybody else.
        string shipped = Scaffold.Files(Name).Single(file => file.Key == "BannedSymbols.txt").Value;
        string own = File.ReadAllText(RepoPaths.BannedSymbols);

        foreach (string symbol in Symbols(shipped))
        {
            CollectionAssert.Contains(
                Symbols(own).ToList(),
                symbol,
                symbol + " is banned in every solution this scaffolds and not in this one. Either take "
                + "the ban, or stop shipping it.");
        }
    }

    /// <summary>The symbol each banned-API line names, without its reason.</summary>
    private static IEnumerable<string> Symbols(string file) => file
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Select(line => line.Split(';')[0]);

    [TestMethod]
    public void The_scaffold_bans_reading_the_clock_from_a_static()
    {
        // The one ban worth shipping before there is an HTTP boundary to ban Results.Problem at. A
        // clock read from a static cannot be substituted, so anything depending on the time becomes
        // untestable - and it is wired in Directory.Build.props rather than per project, so a project
        // added later is covered without anybody remembering.
        string banned = Scaffold.Files(Name)
            .Single(file => file.Key == "BannedSymbols.txt").Value;

        foreach (string symbol in (string[])
        [
            "P:System.DateTime.Now",
            "P:System.DateTime.UtcNow",
            "P:System.DateTime.Today",
            "P:System.DateTimeOffset.Now",
            "P:System.DateTimeOffset.UtcNow",
        ])
        {
            StringAssert.Contains(banned, symbol);
        }

        foreach (string line in banned.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            StringAssert.Contains(
                line,
                ";",
                "Every ban carries its reason after a semicolon. A ban with no reason is a rule somebody "
                + "deletes at the moment it is inconvenient: " + line);
        }

        string props = Templates.BuildProps();
        StringAssert.Contains(props, "Microsoft.CodeAnalysis.BannedApiAnalyzers");
        StringAssert.Contains(props, "$(MSBuildThisFileDirectory)BannedSymbols.txt");
    }

    [TestMethod]
    [DataRow("dice-trial", "DiceTrial")]
    [DataRow("my_app", "MyApp")]
    [DataRow("MarketSense", "MarketSense")]
    [DataRow("some.thing here", "SomeThingHere")]
    [DataRow("2fast", "Solution")]
    [DataRow("---", "Solution")]
    public void A_directory_name_becomes_something_that_can_be_a_namespace(string directory, string expected)
    {
        // A repository directory is often not a legal C# identifier, and a scaffold that used one
        // verbatim would write a solution that cannot compile for a reason nobody would look for in
        // the directory name.
        Assert.AreEqual(expected, Scaffold.NameFrom(directory));
    }
}

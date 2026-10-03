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

        Assert.AreEqual(11, files.Count, "A template added or removed changes this number deliberately.");

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

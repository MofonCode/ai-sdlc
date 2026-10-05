using System.Xml;
using System.Xml.Linq;
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
        // not in the build, so this passing means every one resolved.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        Assert.AreEqual(21, files.Count, "A template added or removed changes this number deliberately.");

        foreach ((string path, string contents) in files)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(contents),
                path + " came out empty, which means its resource resolved to nothing.");
        }
    }

    [TestMethod]
    public void Every_project_file_the_scaffold_writes_is_well_formed_xml()
    {
        // The cheapest layer that can hold the rule, and it exists because of a real defect: a comment
        // in two .csproj templates carried `--`, which is illegal inside an XML comment, and MSBuild
        // refuses the project with MSB4025 before it compiles a line. Nothing in this suite noticed,
        // because every other test here asserts strings.
        //
        // The trap is specific and will recur: this repository's prose style uses `--` as a dash, the
        // C# templates are full of it legitimately, and an XML comment is the one place it is fatal.
        List<string> malformed = [];
        int inspected = 0;

        foreach ((string path, string contents) in Scaffold.Files(Name))
        {
            if (!path.EndsWith(".csproj", StringComparison.Ordinal)
                && !path.EndsWith(".slnx", StringComparison.Ordinal))
            {
                continue;
            }

            inspected++;

            try
            {
                XDocument.Parse(contents);
            }
            catch (XmlException error)
            {
                malformed.Add(path.Replace('\\', '/') + " -- " + error.Message);
            }
        }

        Assert.IsTrue(
            inspected > 0,
            "This rule parsed no project files, so it would pass however they were written. Either the "
            + "scaffold stopped writing a .csproj and a .slnx, or their extensions changed.");

        Assert.AreEqual(
            0,
            malformed.Count,
            "These project files the scaffold writes are not well-formed XML:\n  "
            + string.Join("\n  ", malformed)
            + "\n\nMSBuild refuses the project before compiling anything, so the generated solution "
            + "fails on its first build. If the message mentions '--', that is the usual cause: a "
            + "double hyphen is illegal inside an XML comment, and a single one reads the same. This "
            + "catches the shape of the file and nothing about whether the code in it compiles -- "
            + "tools/Sdlc.ScaffoldHarness is what does that, and CI runs it.");
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
            paths.Contains("src/Demo.Application/Demo.Application.csproj"),
            "The rules need a project to read, and RepoFiles throws rather than passing on an empty scan.");

        Assert.IsTrue(
            paths.Contains("src/Demo.Application/Greetings/Greet.cs"),
            "The reference slice is the most effective thing a profile ships: an agent copies the code "
            + "beside it far more reliably than it follows an instruction.");
    }

    [TestMethod]
    public void The_slice_spans_the_split_it_is_teaching()
    {
        // D-38 puts a response in the contracts project and leaves its query nested in the holder. That
        // is the one place the scaffold departs from "read one file to see an operation", so the slice
        // has to demonstrate the pair rather than describe it -- and the folders have to mirror, because
        // a reader looking for a response finds it by the path or by searching.
        string[] paths = [.. Scaffold.Files(Name).Select(file => file.Key.Replace('\\', '/'))];

        CollectionAssert.Contains(paths, "src/Demo.Contracts/Demo.Contracts.csproj");

        Assert.IsTrue(
            paths.Contains("src/Demo.Contracts/Greetings/GreetResponse.cs"),
            "The response belongs in the contracts project, in the folder mirroring the holder's. "
            + "Without the pair shipped, the first operation written from habit nests its response and "
            + "the WebAssembly client has no type for it.");

        string contracts = Scaffold.Files(Name)
            .Single(file => file.Key.EndsWith("Demo.Contracts.csproj", StringComparison.Ordinal)).Value;

        Assert.IsFalse(
            contracts.Contains("<PackageReference", StringComparison.Ordinal)
            || contracts.Contains("<ProjectReference", StringComparison.Ordinal),
            "The contracts project references nothing, and that is the rule rather than the starting "
            + "state: a Blazor WebAssembly client ships every assembly it references to the browser, so "
            + "a dependency here is one every visitor downloads.");
    }

    [TestMethod]
    public void The_scaffold_ships_the_rules_that_hold_the_layout()
    {
        // The layout is only a convention until something fails on it, and D-38's whole argument is
        // that a rule nobody can enforce is one somebody reaches for when it is inconvenient. These are
        // the three the entry names, by the test name a reader would meet them under.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        string structure = files
            .Single(file => file.Key.EndsWith("StructureTests.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(
            structure,
            "Every_project_under_src_references_only_what_the_direction_rule_allows",
            "The direction rule is what keeps the dependencies pointing one way, and what stops a web "
            + "project reaching past the wire into the application.");

        StringAssert.Contains(
            structure,
            "Only_the_application_project_may_take_a_data_or_vendor_package",
            "internal stops another project using the DbContext; this is what stops it adding EF and "
            + "declaring a second one.");

        StringAssert.Contains(
            structure,
            "Every_project_under_src_is_named_in_both_rules",
            "Guard the guard: a project neither table names is a project both rules skip silently.");

        // D-57. techstack.md declares MudBlazor for ui components and a Razor class library for shared
        // UI, and the tables here used to forbid every way of doing it: MudBlazor failed the package
        // rule in Web, a Ui project failed the guard above on arrival, and Web -> Ui failed the
        // direction rule. A profile whose own test contradicts its own stack file is worse than either
        // alone, because the plan that follows the file is the one that fails.
        StringAssert.Contains(
            structure,
            "[\"Ui\"] = [\"Contracts\"]",
            "The Razor class library needs a direction row, or the first solution to add one meets a "
            + "failing build asking for it.");

        StringAssert.Contains(
            structure,
            "[\"Web\"] = [\"Contracts\", \"Ui\"]",
            "A host that cannot reference the component library cannot render anything from it.");

        StringAssert.Contains(
            structure,
            "[\"Ui\"] = [\"MudBlazor\"",
            "MudBlazor is declared by techstack.md and admitted nowhere until this row exists.");

        string convention = files
            .Single(file => file.Key.EndsWith("ConventionTests.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(
            convention,
            "Every_response_that_crosses_the_wire_lives_in_the_contracts_project",
            "Without it the split is a habit, and the failure arrives in the UI as a type the client "
            + "does not have - a long way from the file that caused it.");
    }

    [TestMethod]
    public void The_scaffold_ships_a_liveness_route_and_the_rule_that_keeps_it()
    {
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        string host = files
            .Single(file => file.Key.EndsWith("Program.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(
            host,
            "AddHealthChecks",
            "MapHealthChecks without the registration throws at startup rather than at build, so the "
            + "scaffold ships both or neither.");

        StringAssert.Contains(host, "MapHealthChecks(\"/health\")");

        // Not through an Endpoints holder, deliberately: Every_endpoint_returns_the_envelope requires
        // one of those to return ApiResult<T>, and a load balancer reading this route understands a
        // status code and the framework's payload. Routing it through the envelope would make the one
        // caller nobody can change parse something it has never heard of.
        Assert.IsFalse(
            files.Any(file => file.Key.EndsWith("HealthEndpoints.cs", StringComparison.Ordinal)),
            "Liveness goes through the framework's own route rather than this solution's envelope, so "
            + "there is no holder for it and no exception carved into the envelope rule.");

        string endpoints = files
            .Single(file => file.Key.EndsWith("EndpointTests.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(
            endpoints,
            "The_host_still_answers_whether_it_is_up",
            "A mapped route nothing checks is a line somebody deletes in the same commit as the change "
            + "that made it inconvenient. A process with no liveness route is indistinguishable from one "
            + "that is up and broken, and the traffic keeps arriving.");
    }

    [TestMethod]
    public void The_reference_slice_is_written_in_the_operation_shape()
    {
        string slice = Scaffold.Files(Name).Single(file => file.Key.EndsWith("Greet.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(slice, "public static class Greet");
        StringAssert.Contains(slice, "public sealed record Query(");

        // The response is NOT here -- D-38 puts it in the contracts project, because it is the shape a
        // WebAssembly client deserialises and that client cannot reference this project. Asserted as an
        // absence as well as a presence, because restoring the nesting is the natural thing to do and
        // it compiles perfectly.
        Assert.IsFalse(
            slice.Contains("record Response(", StringComparison.Ordinal),
            "The response is nested in the holder again. It belongs in the contracts project, and the "
            + "architecture rule the scaffold ships says so - but the slice is what people copy, so a "
            + "nested one here teaches the shape the rule then refuses.");

        StringAssert.Contains(
            slice,
            "IRequest<GreetResponse>",
            "The query names its response from the contracts project, which is what the architecture "
            + "rule checks for every request the application declares.");

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
    public void The_scaffold_ships_a_boundary_for_its_response_bans_to_bite_on()
    {
        // The finding this encodes: a ban naming a symbol the compilation cannot resolve is reported by
        // NOTHING. No warning, no diagnostic - 'T:Totally.Made.Up.Type' in a library builds clean. So
        // banning the framework's response factories in a solution with no HTTP project was a rule that
        // existed only in a file, which is worse than no rule because the file makes it look enforced.
        //
        // The two therefore ship together or not at all. If the Api project is ever dropped from the
        // scaffold, these bans have to go with it, and this test is what says so.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);
        string[] paths = [.. files.Select(file => file.Key.Replace('\\', '/'))];

        CollectionAssert.Contains(paths, "src/Demo.Api/Demo.Api.csproj");
        CollectionAssert.Contains(paths, "src/Demo.Api/Boundary.cs");
        CollectionAssert.Contains(paths, "src/Demo.Api/Greetings/GreetEndpoints.cs");

        string banned = files.Single(file => file.Key == "BannedSymbols.txt").Value;

        foreach (string symbol in (string[])
        [
            "T:Microsoft.AspNetCore.Http.Results",
            "T:Microsoft.AspNetCore.Http.TypedResults",
        ])
        {
            StringAssert.Contains(
                banned,
                symbol,
                symbol + " is what makes the one conversion the only way out. Both are banned rather "
                + "than Results.Problem alone: Problem has three overloads, a ban naming the wrong one "
                + "is silently useless, and TypedResults.Problem does the same thing under another name.");
        }
    }

    [TestMethod]
    public void The_slice_leaves_through_the_one_conversion()
    {
        // The reference slice is the strongest thing the profile ships, so the endpoint has to show the
        // shape rather than describe it: dispatch, convert, return. An architecture test in the
        // scaffolded solution requires the return type, and this requires the slice to demonstrate it.
        IReadOnlyList<KeyValuePair<string, string>> files = Scaffold.Files(Name);

        string endpoints = files
            .Single(file => file.Key.EndsWith("GreetEndpoints.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(endpoints, "Task<ApiResult<GreetResponse>>");
        StringAssert.Contains(endpoints, "result.ToApiResult(http)");

        string boundary = files
            .Single(file => file.Key.EndsWith("Boundary.cs", StringComparison.Ordinal)).Value;

        StringAssert.Contains(
            boundary,
            "result.ToEnvelope(traceId: http.TraceIdentifier)",
            "The conversion goes through the kernel's envelope, which is what drops an exceptional "
            + "error and everything it caused. A boundary that shaped the response itself would be a "
            + "second implementation of the one rule that must not have two.");

        StringAssert.Contains(
            boundary,
            "WriteAsJsonAsync",
            "It writes the JSON itself rather than calling a framework factory, which is what lets the "
            + "ban be absolute. A sanctioned helper calling the banned API would need an exemption, and "
            + "the exemption is the hole.");
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

        foreach (string symbol in Symbols(shipped).Except(NoSuchSurfaceHere))
        {
            CollectionAssert.Contains(
                Symbols(own).ToList(),
                symbol,
                symbol + " is banned in every solution this scaffolds and not in this one. Either take "
                + "the ban, or stop shipping it.");
        }
    }

    /// <summary>
    /// Bans the profile ships that this repository does not take, each because the surface they
    /// govern does not exist here.
    /// </summary>
    /// <remarks>
    /// <para>Not a weakening of the rule but the rule stated correctly: what would be wrong is holding
    /// consumers to something the framework declines to follow <em>where it applies</em>. The clock ban
    /// applies here, because this reads clocks. These two govern an HTTP boundary, and <c>sdlc</c> is a
    /// command-line binary with none — so taking them would add lines naming symbols this compilation
    /// cannot resolve, and an unresolvable ban is reported by nothing at all. A list of inert lines is
    /// exactly what this file exists to prevent.</para>
    ///
    /// <para><strong>Add an entry only with that reason, in the change that declines the ban.</strong>
    /// "This repository has no such surface" is the only argument this list accepts. "We do not want to
    /// follow it here" is the thing it is for catching.</para>
    /// </remarks>
    private static readonly string[] _noSuchSurfaceHere =
    [
        "T:Microsoft.AspNetCore.Http.Results",
        "T:Microsoft.AspNetCore.Http.TypedResults",
    ];

    private static IReadOnlyList<string> NoSuchSurfaceHere => _noSuchSurfaceHere;

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

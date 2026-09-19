using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AiSdlc.Tests;

/// <summary>
/// The real pack manifest, executed into an empty directory, built, and run - per kind.
/// </summary>
/// <remarks>
/// <para>
/// Everything else in this suite proves machinery. This class is the evidence: a machine
/// takes the manifest the pack actually ships, produces a solution from nothing, and watches
/// it compile and pass its own tests. A green row here means that happened in this run.
/// </para>
/// <para>
/// Slow by construction - real generators, a real restore, a real build - so the expensive
/// part happens once per kind in <see cref="Prepare"/> and every row asserts against what it
/// recorded. One row per requirement rather than one row per kind, so a failure names the
/// thing that broke instead of "the web kind".
/// </para>
/// <para>
/// <b>Nothing here skips.</b> A missing SDK, a missing template, a missing asset fails the
/// row with what the executor said. A skipped MSTest leaves the whole command at exit 0
/// showing neither a pass nor a failure, which is the silent success this feature exists to
/// eliminate. The single exception is the container runtime: where there is none, the
/// integration suite must still compile, is not run, and the row says so in its output.
/// <see cref="NoRowInThisClassCanSkip"/> is the instrument that holds that rule.
/// </para>
/// </remarks>
[TestClass]
public sealed partial class ScaffoldEmitsBuildableSolutionTests
{
    /// <summary>
    /// The identifier handed to the executor, so the emitted names are the same every run and
    /// do not depend on what a temp directory happened to be called.
    /// </summary>
    private const string Solution = "Probe";

    /// <summary>
    /// The role, in the frozen layout, whose suite needs a container runtime to exercise the
    /// composed system. It is the one suite R64 allows to be compiled and not run.
    /// </summary>
    private const string IntegrationRole = "IntegrationTests";

    /// <summary>Generous on purpose: a first restore on a cold package cache is not fast.</summary>
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(20);

    /// <summary>
    /// A markdown code fence. R68's check for one lives in
    /// <see cref="StarterNotebookIsHeadingsAndASolutionReferenceOnly"/>; it is a constant here
    /// so the search does not have to appear inside a string that a later reader mistakes for
    /// a fence of this file's own.
    /// </summary>
    private const string Fence = "```";

    /// <summary>The profile R67 says the competition kind selects. Named, not read.</summary>
    private const string MachineLearningProfile = "dotnet-ml";

    /// <summary>
    /// How many headings R68's "section headings" has to mean before the notebook counts as
    /// carrying any. Deliberately below what the asset ships, so adding or merging a section is
    /// a pack revision rather than a test failure; removing most of them is the failure.
    /// </summary>
    private const int MinimumNotebookHeadings = 5;

    /// <summary>
    /// What the web kind must emit - **R21**, stated here and deliberately not read out of
    /// <c>scaffold.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expectation derived from the manifest moves with the thing it is supposed to hold:
    /// delete the action that generates and registers the front end and the expectation for a
    /// front end disappears along with it, every row stays green, and the hole this list exists
    /// to close is rebuilt one layer up where it is harder to see. So the contract is written
    /// out a second time, independently, and compared against what is actually on disk.
    /// </para>
    /// <para>
    /// The layout these paths spell is what charter v3 **froze**: the <c>src/</c> + <c>tests/</c>
    /// + <c>terraform/</c> + <c>notebooks/</c> top-level split, the <c>&lt;Solution&gt;.&lt;Role&gt;</c>
    /// project-name pattern, the role suffixes, and the solution file's extension. Changing any
    /// of them is a migration across every downstream repository, so a failure here is a signal
    /// to amend the spec rather than to edit this list.
    /// </para>
    /// </remarks>
    private static readonly FrozenLayout WebLayout = new(
        SolutionFile: Solution + ".slnx",
        Projects:
        [
            "src/" + Solution + ".AppHost/" + Solution + ".AppHost.csproj",
            "src/" + Solution + ".ServiceDefaults/" + Solution + ".ServiceDefaults.csproj",
            "src/" + Solution + ".Web/" + Solution + ".Web.csproj",
            "src/" + Solution + ".Api/" + Solution + ".Api.csproj",
            "tests/" + Solution + ".UnitTests/" + Solution + ".UnitTests.csproj",
            "tests/" + Solution + ".IntegrationTests/" + Solution + ".IntegrationTests.csproj",
        ],
        OtherFiles:
        [
            "README.md",
            ".gitignore",
            ".editorconfig",
            "global.json",
            "Directory.Build.props",
            "Directory.Packages.props",
            "coverage.runsettings",
            "terraform/versions.tf",
        ]);

    /// <summary>
    /// What the competition kind must emit - **R22**, on the same terms as
    /// <see cref="WebLayout"/>: the same top-level split, different roles, plus the starter
    /// notebook that is the fifth of its five artefacts.
    /// </summary>
    private static readonly FrozenLayout CompetitionLayout = new(
        SolutionFile: Solution + ".slnx",
        Projects:
        [
            "src/" + Solution + ".Features/" + Solution + ".Features.csproj",
            "src/" + Solution + ".Training/" + Solution + ".Training.csproj",
            "tests/" + Solution + ".UnitTests/" + Solution + ".UnitTests.csproj",
        ],
        OtherFiles:
        [
            "README.md",
            ".gitignore",
            ".editorconfig",
            "global.json",
            "Directory.Build.props",
            "Directory.Packages.props",
            "coverage.runsettings",
            "terraform/versions.tf",
            "notebooks/" + Solution + ".ipynb",
        ]);

    /// <summary>
    /// The five things R24 requires of the web kind's emitted README, as the text an owner
    /// opening the repository would actually find.
    /// </summary>
    private static readonly IReadOnlyList<Expectation> WebReadme =
    [
        new("the build command", "dotnet build"),
        new("the test command", "dotnet test"),
        new("the local-run command", "dotnet run --project src/" + Solution + ".AppHost"),
        new("where the artifact chain lives", ".sdlc/"),
        new("what the charter in it is for", "CHARTER.md"),
        new(
            "the invariant's first half - the orchestration host describes local development",
            "AppHost resource graph describes local development"),
        new(
            "the invariant's second half - the infrastructure definition is authoritative",
            "Terraform is the single source of truth"),
    ];

    /// <summary>
    /// The same five things for the competition kind, whose local-run command is its training
    /// runner and which states only the half of the invariant that means anything in a
    /// repository with no orchestration host - see <see cref="AssertReadmeCarries"/>.
    /// </summary>
    private static readonly IReadOnlyList<Expectation> CompetitionReadme =
    [
        new("the build command", "dotnet build"),
        new("the test command", "dotnet test"),
        new("the local-run command", "dotnet run --project src/" + Solution + ".Training"),
        new("where the artifact chain lives", ".sdlc/"),
        new("what the charter in it is for", "CHARTER.md"),
        new(
            "the infrastructure definition is authoritative for anything deployed",
            "Terraform is the single source of truth"),
    ];

    /// <summary>
    /// The stack's own ignore entries - R25's "in addition to". One per category the base file
    /// covers, so a check that passes says the whole file arrived rather than one line of it.
    /// </summary>
    private static readonly IReadOnlyList<Expectation> BaseIgnoreEntries =
    [
        new("build output", "[Oo]bj/"),
        new("test and coverage output", "[Tt]est[Rr]esults/"),
        new("per-user IDE state", "*.user"),
        new("notebook kernel scratch state", ".ipynb_checkpoints/"),
    ];

    /// <summary>
    /// The Terraform entries R25 says the base file is emitted *alongside*. Already appended
    /// before this change, and asserted here so that replacing one append with the other cannot
    /// read as satisfying the requirement.
    /// </summary>
    private static readonly IReadOnlyList<Expectation> TerraformIgnoreEntries =
    [
        new("the provider cache", "**/.terraform/*"),
        new("state, which can hold secret values in plain text", "*.tfstate"),
        new("the local override convention", "override.tf"),
    ];

    /// <summary>
    /// The packages the machine-learning profile commits to, which is what R67 means by the
    /// competition kind requiring that profile's pins.
    /// </summary>
    private static readonly IReadOnlyList<Expectation> MachineLearningPins =
    [
        new("the ML framework itself", "Microsoft.ML"),
        new("gradient boosting", "Microsoft.ML.LightGbm"),
        new("automated model selection", "Microsoft.ML.AutoML"),
        new("dataframes", "Microsoft.Data.Analysis"),
    ];

    /// <summary>
    /// The sample artefacts the web kind's generators produce and R26 requires gone, named
    /// independently of the globs the manifest aims at them.
    /// </summary>
    /// <remarks>
    /// This list is the second opinion, not the main instrument - see
    /// <see cref="AssertSampleContentRemoved"/>. On its own it would go stale in exactly the
    /// way R26 is about: the day the SDK renames one of these, a check that only looks for the
    /// old name passes while the newly named sample ships.
    /// </remarks>
    private static readonly IReadOnlyList<string> WebSampleContent =
    [
        "src/" + Solution + ".Api/Controllers/WeatherForecastController.cs",
        "src/" + Solution + ".Api/WeatherForecast.cs",
        "src/" + Solution + ".Api/" + Solution + ".Api.http",
        "tests/" + Solution + ".UnitTests/Test1.cs",
        "tests/" + Solution + ".IntegrationTests/IntegrationTest1.cs",
    ];

    /// <summary>The same for the competition kind, whose generators produce two.</summary>
    private static readonly IReadOnlyList<string> CompetitionSampleContent =
    [
        "src/" + Solution + ".Features/Class1.cs",
        "tests/" + Solution + ".UnitTests/Test1.cs",
    ];

    private static Scaffolded? webScaffold;
    private static Scaffolded? competitionScaffold;

    /// <summary>Set by MSTest; every row writes what it found here so a pass is readable.</summary>
    public TestContext TestContext { get; set; } = null!;

    private static string Executor => Path.Combine(
        Repo.Root, "plugins", "sdlc-core", "scripts", "Invoke-ScaffoldManifest.ps1");

    private static string Manifest => Path.Combine(
        Repo.Root, "plugins", "stack-dotnet", "skills", "stack-dotnet", "scaffold.json");

    private static Scaffolded Web => webScaffold
        ?? throw new InvalidOperationException("class initialisation did not run");

    private static Scaffolded Competition => competitionScaffold
        ?? throw new InvalidOperationException("class initialisation did not run");

    [ClassInitialize]
    public static void Prepare(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        webScaffold = Scaffolded.For("web");
        competitionScaffold = Scaffolded.For("competition");
    }

    [ClassCleanup]
    public static void Release()
    {
        webScaffold?.Dispose();
        competitionScaffold?.Dispose();
        webScaffold = null;
        competitionScaffold = null;
    }

    // --- R31: the solution builds ---------------------------------------------------------

    [TestMethod]
    public void ScaffoldEmitsBuildableSolution_Web() => AssertBuilds(Web);

    [TestMethod]
    public void ScaffoldEmitsBuildableSolution_Competition() => AssertBuilds(Competition);

    private void AssertBuilds(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);
        TestContext.WriteLine(scaffold.Describe());

        Assert.AreEqual(
            0,
            scaffold.Build.ExitCode,
            "the '" + scaffold.Kind + "' kind scaffolded and did not build. This is R31, and a "
                + "scaffold that emits a solution nobody can compile is the whole complaint "
                + "this feature answers." + Environment.NewLine + Errors(scaffold.Build));
    }

    // --- R66: restore under central package management ------------------------------------

    [TestMethod]
    public void RestoreSucceedsUnderCentralPackageManagement_Web() => AssertRestores(Web);

    [TestMethod]
    public void RestoreSucceedsUnderCentralPackageManagement_Competition() => AssertRestores(Competition);

    private void AssertRestores(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        string packages = Path.Combine(scaffold.Target, "Directory.Packages.props");
        Assert.IsTrue(
            File.Exists(packages),
            "the '" + scaffold.Kind + "' kind emitted no central package file, so there is "
                + "nothing for R66 to hold.");

        string packagesText = File.ReadAllText(packages);
        Assert.IsTrue(
            packagesText.Contains(
                "<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>",
                StringComparison.Ordinal),
            "R66 is about restore succeeding with transitive pinning ON. With it off, the "
                + "restore below proves a smaller surface than the requirement claims.");

        List<string> offenders = [];
        foreach (string project in scaffold.Projects)
        {
            foreach (Match match in VersionedReference().Matches(File.ReadAllText(project)))
            {
                offenders.Add(Relative(scaffold, project) + ": " + match.Value.Trim());
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "a reference still carries a Version attribute, which central package management "
                + "rejects outright (NU1008). The manifest's rewrite is what is supposed to "
                + "have taken these off:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));

        TestContext.WriteLine(
            scaffold.Kind + ": " + scaffold.Projects.Count
                + " emitted project(s), none carrying a version attribute");

        Assert.AreEqual(
            0,
            scaffold.Restore.ExitCode,
            "restore failed for the '" + scaffold.Kind + "' kind with transitive pinning on. "
                + "Nothing else makes R28's package list self-enforcing - this restore is its "
                + "only instrument." + Environment.NewLine + Errors(scaffold.Restore));
    }

    // --- R32, R64: the suites run and report a count ----------------------------------------

    [TestMethod]
    public void EmittedSuitesReportATestCount_Web() => AssertSuitesReportACount(Web);

    [TestMethod]
    public void EmittedSuitesReportATestCount_Competition() => AssertSuitesReportACount(Competition);

    private void AssertSuitesReportACount(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        Assert.AreEqual(
            0,
            scaffold.Build.ExitCode,
            "the '" + scaffold.Kind + "' kind did not build, so no suite could be run and none "
                + "could be compiled either. R64's compile-only downgrade is for an absent "
                + "container runtime, not for a solution that does not compile."
                + Environment.NewLine + Errors(scaffold.Build));

        Assert.AreNotEqual(
            0,
            scaffold.Suites.Count,
            "the '" + scaffold.Kind + "' kind emitted no test project at all. A solution with "
                + "no suite reports green by having nothing to report.");

        TestContext.WriteLine(
            scaffold.Kind + ": container runtime "
                + (Scaffolded.ContainerRuntime is null
                    ? "NOT DETECTED - integration tests are compiled and NOT RUN"
                    : "detected (" + Scaffolded.ContainerRuntime + ")"));

        foreach (Suite suite in scaffold.Suites)
        {
            if (suite.NotRun is not null)
            {
                // The one permitted downgrade, and it is named rather than implied. The suite
                // compiled - the build above is what proves that - and did not run.
                TestContext.WriteLine(
                    scaffold.Kind + " / " + suite.Project + ": COMPILED, NOT RUN - " + suite.NotRun);
                Console.WriteLine(
                    scaffold.Kind + " / " + suite.Project + ": COMPILED, NOT RUN - " + suite.NotRun);
                continue;
            }

            Assert.IsNotNull(
                suite.Run,
                "the '" + suite.Project + "' suite was neither run nor recorded as not run. "
                    + "That is the shape R65 forbids: an outcome nobody can read.");

            Assert.IsTrue(
                suite.Total > 0,
                "the '" + suite.Project + "' suite reported " + suite.Total.ToString(CultureInfo.InvariantCulture)
                    + " tests. A suite that collects nothing exits clean and reads as green, "
                    + "which is why R32 asks for the count and not the exit code."
                    + Environment.NewLine + Errors(suite.Run));

            Assert.AreEqual(
                0,
                suite.Failed,
                "the '" + suite.Project + "' suite reported "
                    + suite.Failed.ToString(CultureInfo.InvariantCulture) + " failure(s) of "
                    + suite.Total.ToString(CultureInfo.InvariantCulture) + "."
                    + Environment.NewLine + Errors(suite.Run));

            TestContext.WriteLine(
                scaffold.Kind + " / " + suite.Project + ": ran "
                    + suite.Total.ToString(CultureInfo.InvariantCulture) + " test(s), "
                    + suite.Failed.ToString(CultureInfo.InvariantCulture) + " failed");
        }
    }

    // --- R23: emitted names match a coverage exclusion ---------------------------------------

    [TestMethod]
    public void EmittedNamesMatchACoverageExclusion_Web() => AssertNamesMatchCoverageExclusions(Web);

    [TestMethod]
    public void EmittedNamesMatchACoverageExclusion_Competition() =>
        AssertNamesMatchCoverageExclusions(Competition);

    /// <summary>
    /// Every emitted test and host assembly matches an exclusion parsed out of the emitted
    /// <c>coverage.runsettings</c> itself.
    /// </summary>
    /// <remarks>
    /// The patterns are read from the file rather than copied into this test on purpose. A
    /// copy is how an instrument quietly stops covering what it claims to: change the asset,
    /// and a test holding its own copy of the old patterns still passes.
    /// </remarks>
    private void AssertNamesMatchCoverageExclusions(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        string runsettings = Path.Combine(scaffold.Target, "coverage.runsettings");
        Assert.IsTrue(
            File.Exists(runsettings),
            "the '" + scaffold.Kind + "' kind emitted no coverage settings, so R23's contract "
                + "has no other side to it.");

        IReadOnlyList<string> globs = ExclusionGlobs(runsettings);
        Assert.AreNotEqual(
            0,
            globs.Count,
            "no assembly exclusion was parsed out of " + runsettings
                + ". Either the file stopped excluding test code, or this test stopped being "
                + "able to read it - both are failures.");

        // The role names the exclusions actually spell, so "AppHost" reaches this test from
        // the asset rather than from a constant somebody has to remember to update.
        HashSet<string> roles = new(StringComparer.OrdinalIgnoreCase);
        foreach (string glob in globs)
        {
            string tail = glob.Contains('.', StringComparison.Ordinal)
                ? glob[(glob.LastIndexOf('.') + 1)..]
                : glob;
            if (!tail.Contains('*', StringComparison.Ordinal) && tail.Length > 0)
            {
                roles.Add(tail);
            }
        }

        TestContext.WriteLine(
            scaffold.Kind + ": exclusions [" + string.Join(", ", globs)
                + "], roles they name [" + string.Join(", ", roles.Order(StringComparer.Ordinal)) + "]");

        List<string> unmatched = [];
        List<string> checkedNames = [];
        foreach (string project in scaffold.Projects)
        {
            string assembly = AssemblyNameOf(project);
            bool isTestProject = Relative(scaffold, project).StartsWith("tests/", StringComparison.Ordinal);
            bool carriesAnExcludedRole = roles.Any(
                role => assembly.EndsWith("." + role, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(assembly, role, StringComparison.OrdinalIgnoreCase));

            if (!isTestProject && !carriesAnExcludedRole)
            {
                continue;
            }

            checkedNames.Add(assembly);
            if (!globs.Any(glob => MatchesGlob(assembly, glob)))
            {
                unmatched.Add(assembly + " (" + Relative(scaffold, project) + ")");
            }
        }

        Assert.AreNotEqual(
            0,
            checkedNames.Count,
            "the '" + scaffold.Kind + "' kind emitted nothing this check could hold - no test "
                + "project and no host. A vacuous pass is not a pass.");

        TestContext.WriteLine(scaffold.Kind + ": held " + string.Join(", ", checkedNames));

        Assert.AreEqual(
            0,
            unmatched.Count,
            "an emitted test or host assembly no longer matches any exclusion in "
                + "coverage.runsettings, so its own code is being counted as covered product "
                + "code and nothing else would say a word:" + Environment.NewLine
                + string.Join(Environment.NewLine, unmatched));
    }

    // --- R21, R22: what the scaffold actually emits, against the frozen layout ----------------

    [TestMethod]
    public void EmittedLayoutIsTheFrozenOne_Web() => AssertLayoutIsFrozenOne(Web, WebLayout);

    [TestMethod]
    public void EmittedLayoutIsTheFrozenOne_Competition() =>
        AssertLayoutIsFrozenOne(Competition, CompetitionLayout);

    /// <summary>
    /// Every path the frozen layout requires of this kind is on disk after a real execution,
    /// every project it requires is registered in the solution, and nothing else is a project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The expectation is a second, independent statement of the contract - see
    /// <see cref="WebLayout"/> for why it is not read out of the manifest.
    /// </para>
    /// <para>
    /// Registration is checked as well as existence because the two failures look different and
    /// only one of them is loud. A project directory that exists but is not in the solution file
    /// is not compiled, not analysed and not run; the build still exits 0 and every other row in
    /// this class still passes.
    /// </para>
    /// <para>
    /// The emitted project set is compared for equality rather than containment. Which roles
    /// exist is part of what charter v3 froze, so an unexpected project is as much a change to
    /// the contract as a missing one - a failure here is a signal to amend the spec, not to
    /// widen this list.
    /// </para>
    /// </remarks>
    private void AssertLayoutIsFrozenOne(Scaffolded scaffold, FrozenLayout expected)
    {
        AssertScaffolded(scaffold);

        string solutionPath = Path.Combine(scaffold.Target, expected.SolutionFile);
        Assert.IsTrue(
            File.Exists(solutionPath),
            "the '" + scaffold.Kind + "' kind emitted no " + expected.SolutionFile
                + ". The solution file and its extension are both frozen by charter v3, so this "
                + "is either a scaffold that emitted nothing to open or a migration nobody "
                + "declared.");

        List<string> otherSolutionFiles = Directory
            .EnumerateFiles(scaffold.Target, "*.sln*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Where(name => !string.Equals(name, expected.SolutionFile, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.AreEqual(
            0,
            otherSolutionFiles.Count,
            "the '" + scaffold.Kind + "' kind emitted a second solution file alongside "
                + expected.SolutionFile + ". R53 refuses to guess between two of them on a "
                + "re-run, so emitting two makes the repository unscaffoldable a second time: "
                + string.Join(", ", otherSolutionFiles));

        HashSet<string> emitted = scaffold.Projects
            .Select(project => Relative(scaffold, project))
            .ToHashSet(StringComparer.Ordinal);

        List<string> missing = expected.Projects
            .Where(project => !emitted.Contains(project))
            .ToList();
        Assert.AreEqual(
            0,
            missing.Count,
            "the '" + scaffold.Kind + "' kind did not emit every project the frozen layout "
                + "requires. A solution missing one of its roles still restores, still builds "
                + "and still runs whichever suites survived, so nothing else in this class would "
                + "say a word:" + Environment.NewLine + string.Join(Environment.NewLine, missing));

        List<string> unexpected = emitted
            .Where(project => !expected.Projects.Contains(project, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.AreEqual(
            0,
            unexpected.Count,
            "the '" + scaffold.Kind + "' kind emitted a project the frozen layout does not name. "
                + "Which roles exist is frozen, so adding one is a migration across every "
                + "downstream repository and belongs in the spec before it belongs here:"
                + Environment.NewLine + string.Join(Environment.NewLine, unexpected));

        string solutionText = File.ReadAllText(solutionPath);
        List<string> unregistered = expected.Projects
            .Where(project => !solutionText.Contains(project, StringComparison.OrdinalIgnoreCase)
                && !solutionText.Contains(
                    project.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.AreEqual(
            0,
            unregistered.Count,
            "a project the frozen layout requires is on disk but not registered in "
                + expected.SolutionFile + ", so it is never compiled, never analysed and never "
                + "run - and the build exits 0 without it:" + Environment.NewLine
                + string.Join(Environment.NewLine, unregistered));

        List<string> offPattern = emitted
            .Where(project => !FrozenProjectPath().IsMatch(project))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.AreEqual(
            0,
            offPattern.Count,
            "an emitted project breaks the frozen naming pattern - a directory directly under "
                + "src/ or tests/, named '" + Solution + ".<Role>', holding a project file of "
                + "the same name. coverage.runsettings excludes assemblies by that pattern and "
                + "the gate hook matches edited paths against it, so this is the shape both of "
                + "them stop recognising:" + Environment.NewLine
                + string.Join(Environment.NewLine, offPattern));

        List<string> absent = expected.OtherFiles
            .Where(file => !File.Exists(
                Path.Combine(scaffold.Target, file.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();
        Assert.AreEqual(
            0,
            absent.Count,
            "the '" + scaffold.Kind + "' kind did not emit every supporting file the frozen "
                + "layout requires. Each of these is load-bearing for something - the build "
                + "gate, the package pins, the coverage numbers, the ignore rules - and each is "
                + "silently absent rather than loudly so:" + Environment.NewLine
                + string.Join(Environment.NewLine, absent));

        TestContext.WriteLine(
            scaffold.Kind + ": " + expected.SolutionFile + " with "
                + expected.Projects.Count.ToString(CultureInfo.InvariantCulture)
                + " project(s), all registered [" + string.Join(", ", expected.Projects) + "]");
        TestContext.WriteLine(
            scaffold.Kind + ": supporting files [" + string.Join(", ", expected.OtherFiles) + "]");
    }

    // --- R24: what the emitted README carries --------------------------------------------------

    [TestMethod]
    public void EmittedReadmeCarriesWhatTheOwnerNeeds_Web() => AssertReadmeCarries(Web, WebReadme);

    [TestMethod]
    public void EmittedReadmeCarriesWhatTheOwnerNeeds_Competition() =>
        AssertReadmeCarries(Competition, CompetitionReadme);

    /// <summary>
    /// The emitted <c>README.md</c> carries each thing R24 names, read out of the file the
    /// executor actually wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The expected text is written here rather than lifted from the pack's asset, and that
    /// is the whole point of the row.</b> Deriving it from <c>assets/solution.readme.md</c>
    /// would reduce this to "the copy worked": delete the Run locally section from the asset and
    /// the expectation for a local-run command disappears with it. R24 is a statement about what
    /// an owner opening the repository must find, so the test states it.
    /// </para>
    /// <para>
    /// The cost is real and worth naming: this row is coupled to the exact spelling of the
    /// commands, so a pack revision that changes one must change this list too. The spec's
    /// frozen-versus-pinned table puts R24 in the pinned column for exactly that reason - a
    /// revision here is expected to touch both sides, and a change that touches only the asset
    /// is the thing being caught.
    /// </para>
    /// <para>
    /// The competition kind's list omits the orchestration-host clause because that kind emits
    /// no orchestration host. The accepted drift recorded in the plan on 2026-09-18 is exactly
    /// this: one shared README could not honestly serve both kinds. What survives for both is
    /// the half that still means something - the infrastructure definition is authoritative for
    /// anything deployed.
    /// </para>
    /// </remarks>
    private void AssertReadmeCarries(Scaffolded scaffold, IReadOnlyList<Expectation> expected)
    {
        AssertScaffolded(scaffold);

        string readme = Path.Combine(scaffold.Target, "README.md");
        Assert.IsTrue(
            File.Exists(readme),
            "the '" + scaffold.Kind + "' kind emitted no README.md, so R24 has nothing to hold. "
                + "I1 forbids the engine writing one, which makes this the pack's file or "
                + "nobody's.");

        Assert.AreNotEqual(
            0,
            expected.Count,
            "this row was handed an empty expectation list, which would pass on any README at "
                + "all. A vacuous pass is not a pass.");

        string text = Prose(File.ReadAllText(readme));
        List<string> absent = expected
            .Where(item => !text.Contains(item.Text, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.What + " - expected to find: " + item.Text)
            .ToList();

        Assert.AreEqual(
            0,
            absent.Count,
            "the README the '" + scaffold.Kind + "' kind emitted is missing something R24 "
                + "requires it to carry. A README that exists and does not answer 'how do I "
                + "build this' is the stub the spec rejected by name:" + Environment.NewLine
                + string.Join(Environment.NewLine, absent));

        TestContext.WriteLine(
            scaffold.Kind + ": README.md carries "
                + expected.Count.ToString(CultureInfo.InvariantCulture)
                + " required item(s) [" + string.Join("; ", expected.Select(item => item.What)) + "]");
    }

    // --- R25: a base ignore file for the stack, alongside the Terraform entries -------------------

    [TestMethod]
    public void BaseIgnoreEntriesAccompanyTheTerraformOnes_Web() => AssertIgnoreFile(Web);

    [TestMethod]
    public void BaseIgnoreEntriesAccompanyTheTerraformOnes_Competition() =>
        AssertIgnoreFile(Competition);

    /// <summary>
    /// The emitted <c>.gitignore</c> carries the stack's own entries <i>in addition to</i> the
    /// Terraform ones.
    /// </summary>
    /// <remarks>
    /// Both sides are asserted because R25's whole content is the words "in addition to". The
    /// Terraform entries were already being appended before this change; a run that appends only
    /// those produces a repository that ignores state files and commits every build output,
    /// which is R25 satisfied on paper and broken on disk. The entries are matched as whole
    /// lines, so a mention inside a comment does not count as an ignore rule.
    /// </remarks>
    private void AssertIgnoreFile(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        string ignore = Path.Combine(scaffold.Target, ".gitignore");
        Assert.IsTrue(
            File.Exists(ignore),
            "the '" + scaffold.Kind + "' kind emitted no .gitignore at all, so every build "
                + "output and every local state file is a candidate for the first commit.");

        HashSet<string> lines = File.ReadAllLines(ignore)
            .Select(line => line.Trim())
            .Where(line => line.Length != 0)
            .ToHashSet(StringComparer.Ordinal);

        List<string> missingBase = BaseIgnoreEntries
            .Where(entry => !lines.Contains(entry.Text))
            .Select(entry => entry.What + " - expected the line: " + entry.Text)
            .ToList();
        Assert.AreEqual(
            0,
            missingBase.Count,
            "the '" + scaffold.Kind + "' kind emitted a .gitignore without the stack's own base "
                + "entries. This is the half of R25 the change adds - the Terraform half was "
                + "already there, and on its own it leaves build and test output uncovered:"
                + Environment.NewLine + string.Join(Environment.NewLine, missingBase));

        List<string> missingTerraform = TerraformIgnoreEntries
            .Where(entry => !lines.Contains(entry.Text))
            .Select(entry => entry.What + " - expected the line: " + entry.Text)
            .ToList();
        Assert.AreEqual(
            0,
            missingTerraform.Count,
            "the base entries arrived and the Terraform ones did not. R25 is 'in addition to', "
                + "so replacing one append with the other satisfies neither - and a committed "
                + "state file can carry secret values in plain text:" + Environment.NewLine
                + string.Join(Environment.NewLine, missingTerraform));

        TestContext.WriteLine(
            scaffold.Kind + ": .gitignore carries "
                + BaseIgnoreEntries.Count.ToString(CultureInfo.InvariantCulture)
                + " base entr(ies) and "
                + TerraformIgnoreEntries.Count.ToString(CultureInfo.InvariantCulture)
                + " Terraform entr(ies) over "
                + lines.Count.ToString(CultureInfo.InvariantCulture) + " line(s)");
    }

    // --- R26: the sample content the generators produce is actually removed ----------------------

    [TestMethod]
    public void EverySampleRemovalFiredAndTheSampleIsGone_Web() =>
        AssertSampleContentRemoved(Web, WebSampleContent);

    [TestMethod]
    public void EverySampleRemovalFiredAndTheSampleIsGone_Competition() =>
        AssertSampleContentRemoved(Competition, CompetitionSampleContent);

    /// <summary>
    /// Every removal the manifest declares actually removed something, and the sample artefacts
    /// this test knows the generators produce are gone from the emitted tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two checks, because neither one holds R26 alone.</b>
    /// </para>
    /// <para>
    /// The first reads the executor's own report and requires every <c>delete</c> the manifest
    /// declares to have reported <c>removed</c>. This is the one that carries the weight, and
    /// the failure it exists for is precise: an SDK release renames a file a glob names, the
    /// glob matches nothing, and from that day every repository scaffolded from this pack ships
    /// demo content behind a completely green build. Nothing else notices - the solution still
    /// restores, still builds, still runs its suites, and the emitted names still match the
    /// coverage exclusions. It couples this row to the report's outcome vocabulary, which is a
    /// declared contract (R56) and versioned (R61), so that coupling is to something that is
    /// not allowed to move silently.
    /// </para>
    /// <para>
    /// What the first check cannot see is a <c>delete</c> that removed <i>something</i>, but not
    /// the thing it was aimed at - a glob widened by accident reports <c>removed</c> just as
    /// happily. So the second check asserts, by name and independently of the manifest, that
    /// each sample artefact is absent from the emitted tree. Held the other way round, the
    /// second check is the one that goes stale under a rename, which is why it is not the
    /// instrument on its own.
    /// </para>
    /// <para>
    /// Neither catches a generator that starts emitting a <i>new</i> sample file no glob aims
    /// at. That needs a list of what the templates produce, which is the coupling the manifest
    /// exists to avoid, and it is recorded here as a known limit rather than solved.
    /// </para>
    /// <para>
    /// <b>The first run's report, specifically.</b> On the gap-filling re-run a <c>delete</c>
    /// that matches nothing is the correct outcome - the sample was removed the first time and
    /// is not supposed to come back - so R26 is a statement about the run that creates the
    /// repository, and that is the report this row reads.
    /// </para>
    /// <para>
    /// R26's other half - at least one executable test survives the removal - is already held,
    /// and is not repeated here: <see cref="EmittedSuitesReportATestCount_Web"/> and its
    /// competition twin assert a reported test count above zero for every emitted suite.
    /// </para>
    /// </remarks>
    private void AssertSampleContentRemoved(Scaffolded scaffold, IReadOnlyList<string> samples)
    {
        AssertScaffolded(scaffold);

        IReadOnlyList<string> declared = DeclaredRemovals(scaffold.Kind);
        Assert.IsTrue(
            declared.Count >= samples.Count,
            "the '" + scaffold.Kind + "' kind declares "
                + declared.Count.ToString(CultureInfo.InvariantCulture)
                + " removal(s) for " + samples.Count.ToString(CultureInfo.InvariantCulture)
                + " known sample artefact(s). This is the vacuity guard: 'every declared "
                + "removal fired' says nothing about a removal somebody deleted from the "
                + "manifest, so the count is held as well as the outcomes.");

        ScaffoldReport report = ParseReport(scaffold);
        Assert.AreEqual(
            "completed",
            report.Outcome,
            "the first execution for the '" + scaffold.Kind + "' kind did not complete, so the "
                + "report below records the actions it reached rather than the ones it "
                + "declares, and an absent removal here means nothing.");

        List<ActionRecord> removals = report.Actions
            .Where(entry => string.Equals(entry.Kind, "delete", StringComparison.Ordinal))
            .ToList();

        List<string> firedOnNothing = removals
            .Where(entry => !string.Equals(entry.Outcome, "removed", StringComparison.Ordinal))
            .Select(entry => entry.Outcome + ": " + entry.Path
                + (entry.Reason.Length == 0 ? string.Empty : " - " + entry.Reason))
            .ToList();

        Assert.AreEqual(
            0,
            firedOnNothing.Count,
            "a delete action the manifest declares removed nothing on the run that created the "
                + "'" + scaffold.Kind + "' repository. R26 is then satisfied on paper and broken "
                + "on disk: the generator's sample content ships in every repository scaffolded "
                + "from here on, behind an exit 0 and a build with no errors. The usual cause is "
                + "an SDK release that renamed or moved the file the glob names, and the fix is "
                + "to re-aim the glob in the pack's manifest:" + Environment.NewLine
                + string.Join(Environment.NewLine, firedOnNothing));

        Assert.IsTrue(
            removals.Count >= declared.Count,
            "the report records " + removals.Count.ToString(CultureInfo.InvariantCulture)
                + " delete outcome(s) for " + declared.Count.ToString(CultureInfo.InvariantCulture)
                + " declared removal(s). Each one either removes at least one file or records "
                + "itself as skipped, so a shortfall means an action left no trace at all - "
                + "which is the shape R56 exists to forbid.");

        List<string> survivors = samples
            .Where(sample => File.Exists(
                Path.Combine(scaffold.Target, sample.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();
        Assert.AreEqual(
            0,
            survivors.Count,
            "a sample artefact the generators produce is still in the emitted tree, even though "
                + "every declared removal reported success. That is a removal that took "
                + "something other than what it was aimed at - the one failure the report alone "
                + "cannot show:" + Environment.NewLine
                + string.Join(Environment.NewLine, survivors));

        TestContext.WriteLine(
            scaffold.Kind + ": " + declared.Count.ToString(CultureInfo.InvariantCulture)
                + " declared removal(s), " + removals.Count.ToString(CultureInfo.InvariantCulture)
                + " recorded as removed, and "
                + samples.Count.ToString(CultureInfo.InvariantCulture)
                + " known sample artefact(s) absent from the tree");
    }

    // --- R67: the competition kind applies the machine-learning profile ---------------------------

    /// <summary>
    /// The competition kind declares the machine-learning profile, and the packages that profile
    /// commits to are actually pinned in what it emits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two halves, because either alone is hollow. The declaration is what <c>init</c> records
    /// as a charter commitment; the pins are what the emitted projects can actually reference.
    /// A kind that declares the profile and emits no pin gives a repository whose first ML
    /// package reference fails to restore under central package management, and a kind that
    /// carries the pins without declaring the profile records the wrong commitment in the
    /// charter.
    /// </para>
    /// <para>
    /// The profile name is written here rather than read from the manifest, and the web kind is
    /// asserted <i>not</i> to declare it - otherwise the check passes on a manifest where every
    /// kind names the same thing, which is the reading R67 exists to rule out.
    /// </para>
    /// <para>
    /// The pins are not asserted absent from the web kind: <c>Directory.Packages.props</c> is
    /// one shared asset, so both kinds receive the whole pinned list. What R67 binds is that the
    /// competition kind's emitted projects can reference the ML packages, not that the web kind
    /// cannot.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void CompetitionKindAppliesTheMachineLearningProfile()
    {
        AssertScaffolded(Competition);

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Manifest));
        JsonElement kinds = manifest.RootElement.GetProperty("kinds");

        Assert.AreEqual(
            MachineLearningProfile,
            ProfileOf(kinds, "competition"),
            "the competition kind no longer declares the machine-learning profile. R67 is what "
                + "makes a competition repository record the right technology commitment in its "
                + "charter - nothing else in this suite reads the profile at all.");

        Assert.AreNotEqual(
            MachineLearningProfile,
            ProfileOf(kinds, "web"),
            "every kind declares the machine-learning profile, so asserting that the competition "
                + "kind does says nothing about the competition kind. R67 is a statement about "
                + "one kind and not about the pack.");

        string packages = Path.Combine(Competition.Target, "Directory.Packages.props");
        Assert.IsTrue(
            File.Exists(packages),
            "the competition kind emitted no central package file, so there is nowhere for the "
                + "profile's pins to be.");

        string packagesText = File.ReadAllText(packages);
        List<string> unpinned = MachineLearningPins
            .Where(pin => !IsPinned(packagesText, pin.Text))
            .Select(pin => pin.What + " - expected a PackageVersion for: " + pin.Text)
            .ToList();

        Assert.AreEqual(
            0,
            unpinned.Count,
            "the competition kind declares the machine-learning profile and did not emit the "
                + "pins that profile commits to. Under central package management an unpinned "
                + "reference does not fall back to a floating version - it fails the restore, "
                + "and the failure names NU1008 rather than the missing profile:"
                + Environment.NewLine + string.Join(Environment.NewLine, unpinned));

        TestContext.WriteLine(
            "competition: profile '" + MachineLearningProfile + "', with "
                + MachineLearningPins.Count.ToString(CultureInfo.InvariantCulture)
                + " pin(s) present ["
                + string.Join(", ", MachineLearningPins.Select(pin => pin.Text)) + "]");
    }

    // --- R68: the starter notebook is headings only -------------------------------------------------

    /// <summary>
    /// The emitted starter notebook carries section headings and a reference to the emitted
    /// solution, and carries no executable logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What "no executable logic" means here</b>, because the requirement needs a definition
    /// this row can fail on. Three things, each checked:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>No cell of type <c>code</c>.</b> A code cell is the only thing the notebook kernel
    /// will execute, so it is the literal reading of the requirement.
    /// </description></item>
    /// <item><description>
    /// <b>No recorded output and no execution count.</b> Either is evidence that something in
    /// the file was run and its result shipped - the state the requirement says must not exist,
    /// surviving a later edit that deleted the source but not the output.
    /// </description></item>
    /// <item><description>
    /// <b>No fenced code block inside a markdown cell.</b> A fence is inert to the kernel, which
    /// is exactly why it is the way working code arrives in a headings-only file: it is a
    /// snippet that becomes a code cell the moment somebody copies it. Inline code spans are
    /// untouched by this - the notebook names project paths that way throughout, and naming a
    /// path is the reference to the solution the requirement asks for.
    /// </description></item>
    /// </list>
    /// <para>
    /// The strictness is deliberate and the spec says why: the notebook is the one emitted file
    /// no compiler and no analyzer ever sees, and the owner settled F3 on 2026-09-18 by making
    /// it headings only. The spec records that a later revision putting working code in it
    /// reopens that concern and needs a verification story first. This row is what makes the
    /// reopening loud instead of silent - a failure here is a signal to go back to F3, not to
    /// relax the check.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void StarterNotebookIsHeadingsAndASolutionReferenceOnly()
    {
        AssertScaffolded(Competition);

        string notebook = Path.Combine(Competition.Target, "notebooks", Solution + ".ipynb");
        Assert.IsTrue(
            File.Exists(notebook),
            "the competition kind emitted no starter notebook at " + notebook
                + ". R22 names it as one of the five things the kind emits.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(notebook));
        Assert.IsTrue(
            document.RootElement.TryGetProperty("cells", out JsonElement cells)
                && cells.ValueKind == JsonValueKind.Array,
            "the emitted notebook has no cells array, so it is not a notebook any frontend will "
                + "open and this row cannot read it.");

        List<string> executable = [];
        List<string> markdownSources = [];
        int index = 0;
        foreach (JsonElement cell in cells.EnumerateArray())
        {
            string where = "cell " + index.ToString(CultureInfo.InvariantCulture);
            index++;

            string type = cell.TryGetProperty("cell_type", out JsonElement declared)
                ? declared.GetString() ?? string.Empty
                : string.Empty;

            if (!string.Equals(type, "markdown", StringComparison.Ordinal))
            {
                executable.Add(
                    where + ": cell_type is '" + type + "', and the kernel runs those");
                continue;
            }

            if (cell.TryGetProperty("outputs", out JsonElement outputs)
                && outputs.ValueKind == JsonValueKind.Array
                && outputs.GetArrayLength() != 0)
            {
                executable.Add(where + ": carries recorded output, so something here was run");
            }

            if (cell.TryGetProperty("execution_count", out JsonElement count)
                && count.ValueKind != JsonValueKind.Null)
            {
                executable.Add(where + ": carries an execution count, so something here was run");
            }

            string source = CellSource(cell);
            if (source.Contains(Fence, StringComparison.Ordinal))
            {
                executable.Add(
                    where + ": carries a fenced code block, which is one copy away from running");
            }

            markdownSources.Add(source);
        }

        Assert.AreNotEqual(
            0,
            markdownSources.Count,
            "the emitted notebook has no markdown cells in it. An empty file passes every check "
                + "that only looks for what must not be there, which is why this row also counts "
                + "what must.");

        Assert.AreEqual(
            0,
            executable.Count,
            "the starter notebook carries executable logic. It is the one emitted file that "
                + "Directory.Build.props never reaches - no analyzers, no warnings-as-errors, "
                + "nothing that would notice it is wrong - and the owner settled F3 by making it "
                + "headings only. Putting code back reopens that concern and needs a "
                + "verification story before it can close again:" + Environment.NewLine
                + string.Join(Environment.NewLine, executable));

        string whole = string.Join("\n", markdownSources);
        int headings = whole
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith('#'));
        Assert.IsTrue(
            headings >= MinimumNotebookHeadings,
            "the starter notebook carries " + headings.ToString(CultureInfo.InvariantCulture)
                + " heading(s), fewer than the "
                + MinimumNotebookHeadings.ToString(CultureInfo.InvariantCulture)
                + " R68 asks for. The headings are the only thing in this file doing any work; "
                + "without them it is the stub half of the requirement rather than the useful "
                + "half.");

        Assert.IsTrue(
            whole.Contains("src/" + Solution + ".Features", StringComparison.Ordinal),
            "the starter notebook does not reference the emitted solution. R68 asks for the "
                + "headings and the reference, and the reference is what stops the notebook "
                + "being a scratchpad that re-implements what the library already holds. Its "
                + "absence would also mean no token was substituted into this file.");

        TestContext.WriteLine(
            "competition: notebook has "
                + markdownSources.Count.ToString(CultureInfo.InvariantCulture)
                + " markdown cell(s), " + headings.ToString(CultureInfo.InvariantCulture)
                + " heading(s), no code cell, no recorded output, no fenced block");
    }

    // --- R7: a second execution changes nothing -----------------------------------------------

    [TestMethod]
    public void SecondExecutionChangesNothing_Web() => AssertSecondRunIsANoOp(Web);

    [TestMethod]
    public void SecondExecutionChangesNothing_Competition() => AssertSecondRunIsANoOp(Competition);

    private void AssertSecondRunIsANoOp(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        Assert.AreEqual(
            0,
            scaffold.SecondRun.ExitCode,
            "a second execution over the executor's own output exited non-zero."
                + Environment.NewLine + Errors(scaffold.SecondRun));

        AssertSameTree(
            scaffold.AfterFirstRun,
            scaffold.AfterSecondRun,
            "a second execution changed the tree. R7 is what makes a partially scaffolded "
                + "repository recoverable by re-running, so a re-run that rewrites files is "
                + "the feature failing rather than a detail.");

        TestContext.WriteLine(
            scaffold.Kind + ": re-executed over "
                + scaffold.AfterFirstRun.Count.ToString(CultureInfo.InvariantCulture)
                + " file(s), none changed");
    }

    // --- R34: a deleted directory is refilled and nothing else moves ---------------------------

    [TestMethod]
    public void RefillsOnlyTheDeletedTestsDirectory_Web() => AssertRefillsWhatWasDeleted(Web);

    [TestMethod]
    public void RefillsOnlyTheDeletedTestsDirectory_Competition() =>
        AssertRefillsWhatWasDeleted(Competition);

    private void AssertRefillsWhatWasDeleted(Scaffolded scaffold)
    {
        AssertScaffolded(scaffold);

        Assert.AreEqual(
            0,
            scaffold.RefillRun.ExitCode,
            "executing into a directory holding part of its own output exited non-zero."
                + Environment.NewLine + Errors(scaffold.RefillRun));

        Dictionary<string, string> expectedElsewhere = scaffold.AfterFirstRun
            .Where(pair => !pair.Key.StartsWith(Scaffolded.DeletedSubtree, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Dictionary<string, string> actualElsewhere = scaffold.AfterRefillRun
            .Where(pair => !pair.Key.StartsWith(Scaffolded.DeletedSubtree, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        AssertSameTree(
            expectedElsewhere,
            actualElsewhere,
            "filling the gap left by a deleted '" + Scaffolded.DeletedSubtree + "' touched a "
                + "file outside it. R34 is the requirement that lets someone re-run this in a "
                + "repository that already has work in it.");

        Dictionary<string, string> expectedInside = scaffold.AfterFirstRun
            .Where(pair => pair.Key.StartsWith(Scaffolded.DeletedSubtree, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Dictionary<string, string> actualInside = scaffold.AfterRefillRun
            .Where(pair => pair.Key.StartsWith(Scaffolded.DeletedSubtree, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        Assert.AreNotEqual(
            0,
            expectedInside.Count,
            "the first run put nothing under '" + Scaffolded.DeletedSubtree + "', so deleting "
                + "it proved nothing.");

        AssertSameTree(
            expectedInside,
            actualInside,
            "the deleted '" + Scaffolded.DeletedSubtree + "' came back different from what the "
                + "first run put there.");

        TestContext.WriteLine(
            scaffold.Kind + ": deleted " + expectedInside.Count.ToString(CultureInfo.InvariantCulture)
                + " file(s) under " + Scaffolded.DeletedSubtree + " and got them all back, with "
                + expectedElsewhere.Count.ToString(CultureInfo.InvariantCulture)
                + " file(s) elsewhere byte-identical");
    }

    // --- R65: this class is not allowed to skip -------------------------------------------------

    /// <summary>
    /// Holds the rule the rest of this class is written to: an absent prerequisite fails a row,
    /// it does not skip one.
    /// </summary>
    /// <remarks>
    /// A skipped MSTest leaves <c>dotnet test</c> at exit 0 showing neither a pass nor a
    /// failure, so a suite that skips its way to green is indistinguishable from one that
    /// proved something. This is the check that notices the day somebody reaches for
    /// <c>[Ignore]</c> or an inconclusive result to get past a missing SDK.
    /// </remarks>
    [TestMethod]
    public void NoRowInThisClassCanSkip()
    {
        List<string> ignored = typeof(ScaffoldEmitsBuildableSolutionTests)
            .GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(IgnoreAttribute), inherit: true).Length != 0)
            .Select(method => method.Name)
            .ToList();

        Assert.AreEqual(
            0,
            ignored.Count,
            "a row in the one class that proves the feature end to end is ignored, so the "
                + "suite is green without having run it: " + string.Join(", ", ignored));

        string source = Path.Combine(
            Repo.Root, "tests", "AiSdlc.Tests", "ScaffoldEmitsBuildableSolutionTests.cs");
        Assert.IsTrue(File.Exists(source), "this test can no longer find its own source at " + source);

        // Composed rather than written out, so the search does not match the line doing the
        // searching.
        string inconclusive = "Assert." + "Inconclusive";
        Assert.IsFalse(
            File.ReadAllText(source).Contains(inconclusive, StringComparison.Ordinal),
            "a row in this class reports an inconclusive result. An absent prerequisite must "
                + "fail; the only downgrade R65 permits is the container runtime, and that one "
                + "keeps the row passing while naming itself in the output.");
    }

    // --- shared assertions --------------------------------------------------------------------

    private void AssertScaffolded(Scaffolded scaffold)
    {
        if (scaffold.SetupError is not null)
        {
            Assert.Fail(
                "preparing the '" + scaffold.Kind + "' kind did not get far enough to assert "
                    + "anything: " + scaffold.SetupError);
        }

        Assert.AreEqual(
            0,
            scaffold.FirstRun.ExitCode,
            "executing the shipped manifest for the '" + scaffold.Kind + "' kind into an empty "
                + "directory exited non-zero. If this is a preflight, the message below is the "
                + "pack's own remediation and the fix is to do what it says - the requirement "
                + "is working." + Environment.NewLine + Errors(scaffold.FirstRun));

        TestContext.WriteLine(scaffold.Kind + ": scaffolded into " + scaffold.Target);
    }

    private static void AssertSameTree(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual,
        string because)
    {
        List<string> differences = [];
        foreach (KeyValuePair<string, string> pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out string? hash))
            {
                differences.Add("gone:    " + pair.Key);
            }
            else if (!string.Equals(hash, pair.Value, StringComparison.Ordinal))
            {
                differences.Add("changed: " + pair.Key);
            }
        }

        foreach (string path in actual.Keys.Where(path => !expected.ContainsKey(path)))
        {
            differences.Add("new:     " + path);
        }

        Assert.AreEqual(
            0,
            differences.Count,
            because + Environment.NewLine + string.Join(Environment.NewLine, differences.Order(StringComparer.Ordinal)));
    }

    // --- reading what the emitted files say ------------------------------------------------------

    /// <summary>
    /// A markdown file as one run of prose: every line break, and the blockquote marker that
    /// often follows one, replaced by a single space.
    /// </summary>
    /// <remarks>
    /// R24 is about what the README says, not about where the asset's author happened to wrap a
    /// line. The invariant the web kind's README carries is one sentence split across three
    /// quoted lines, and a raw substring search for it fails on the wrap - which would make the
    /// row fail for a reason that has nothing to do with the requirement, and would push
    /// whoever hit it towards weakening the needle until it matched.
    /// </remarks>
    private static string Prose(string markdown) => LineJoin().Replace(markdown, " ");

    /// <summary>The assembly-name globs inside the runsettings' exclusion list.</summary>
    private static IReadOnlyList<string> ExclusionGlobs(string runsettingsPath)
    {
        XDocument document = XDocument.Load(runsettingsPath);
        List<string> globs = [];
        foreach (XElement element in document.Descendants().Where(e => e.Name.LocalName == "Exclude"))
        {
            foreach (Match match in Bracketed().Matches(element.Value))
            {
                globs.Add(match.Groups[1].Value);
            }
        }

        return globs;
    }

    private static string AssemblyNameOf(string projectPath)
    {
        Match explicitName = DeclaredAssemblyName().Match(File.ReadAllText(projectPath));
        return explicitName.Success
            ? explicitName.Groups[1].Value.Trim()
            : Path.GetFileNameWithoutExtension(projectPath);
    }

    private static bool MatchesGlob(string name, string glob)
    {
        string pattern = "\\A" + Regex.Escape(glob).Replace("\\*", ".*", StringComparison.Ordinal) + "\\z";
        return Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
    }

    private static string Relative(Scaffolded scaffold, string path) =>
        Path.GetRelativePath(scaffold.Target, path).Replace('\\', '/');

    /// <summary>What a failing external process actually said, trimmed to something readable.</summary>
    private static string Errors(Repo.RunResult? result)
    {
        if (result is null)
        {
            return "(the command was never run)";
        }

        string[] lines = result.Combined
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length != 0)
            .ToArray();

        IEnumerable<string> interesting = lines.Where(
            line => line.Contains(": error", StringComparison.OrdinalIgnoreCase)
                || line.Contains("error ", StringComparison.OrdinalIgnoreCase)
                || line.Contains("failed", StringComparison.OrdinalIgnoreCase));

        List<string> chosen = interesting.Distinct(StringComparer.Ordinal).Take(25).ToList();
        if (chosen.Count == 0)
        {
            chosen = lines.TakeLast(25).ToList();
        }

        return "exit " + result.ExitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine
            + string.Join(Environment.NewLine, chosen);
    }

    [GeneratedRegex(@"<PackageReference\b[^>]*\sVersion\s*=\s*""[^""]*""[^>]*>")]
    private static partial Regex VersionedReference();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"<AssemblyName>([^<]*)</AssemblyName>")]
    private static partial Regex DeclaredAssemblyName();

    /// <summary>A line break, with the whitespace and blockquote marker around it.</summary>
    [GeneratedRegex(@"[ \t]*\r?\n[ \t]*>?[ \t]*")]
    private static partial Regex LineJoin();

    /// <summary>
    /// The glob of every <c>delete</c> action one kind of the shipped manifest declares, with
    /// the solution token already substituted the way the executor substitutes it.
    /// </summary>
    /// <remarks>
    /// Read from the manifest on purpose, and it is the one place in this class where that is
    /// the right move: R26 is a statement about the manifest's own removals doing their job,
    /// universally quantified over whatever it declares. The hole that opens - a removal
    /// deleted from the manifest takes its own expectation with it - is closed by the count
    /// guard and the by-name check in <see cref="AssertSampleContentRemoved"/>, not by
    /// pretending this list could be written out independently.
    /// </remarks>
    private static IReadOnlyList<string> DeclaredRemovals(string kind)
    {
        string manifestText = File.ReadAllText(Manifest);

        string token;
        using (JsonDocument raw = JsonDocument.Parse(manifestText))
        {
            token = raw.RootElement.TryGetProperty("solution_token", out JsonElement declared)
                ? declared.GetString() ?? string.Empty
                : string.Empty;
        }

        if (token.Length != 0)
        {
            manifestText = manifestText.Replace(token, Solution, StringComparison.Ordinal);
        }

        List<string> globs = [];
        using JsonDocument manifest = JsonDocument.Parse(manifestText);
        if (!manifest.RootElement.TryGetProperty("kinds", out JsonElement kinds)
            || !kinds.TryGetProperty(kind, out JsonElement selected)
            || !selected.TryGetProperty("actions", out JsonElement actions)
            || actions.ValueKind != JsonValueKind.Array)
        {
            return globs;
        }

        foreach (JsonElement action in actions.EnumerateArray())
        {
            if (action.TryGetProperty("kind", out JsonElement actionKind)
                && string.Equals(actionKind.GetString(), "delete", StringComparison.Ordinal)
                && action.TryGetProperty("glob", out JsonElement glob))
            {
                globs.Add(glob.GetString() ?? string.Empty);
            }
        }

        return globs;
    }

    /// <summary>
    /// The report the first execution emitted, read off the executor's stdout.
    /// </summary>
    /// <remarks>
    /// The command-line contract is that the JSON report goes to stdout and nothing else does -
    /// every human-readable line, and every line of output from a command an action runs, goes
    /// to stderr. The slice between the first brace and the last is defensive rather than
    /// necessary: if something ever does leak onto stdout, a row that fails here with the raw
    /// text is more use than one that fails with a parser exception.
    /// </remarks>
    private static ScaffoldReport ParseReport(Scaffolded scaffold)
    {
        string stdout = scaffold.FirstRun.StdOut;
        int start = stdout.IndexOf('{');
        int end = stdout.LastIndexOf('}');
        Assert.IsTrue(
            start >= 0 && end > start,
            "the executor's stdout for the '" + scaffold.Kind + "' kind carries no JSON report. "
                + "R56 binds a report to a run that completes or halts, and R20 has init relay "
                + "it verbatim, so there is nothing for either to read:" + Environment.NewLine
                + stdout);

        using JsonDocument document = JsonDocument.Parse(stdout[start..(end + 1)]);

        string outcome = Text(document.RootElement, "outcome");
        List<ActionRecord> records = [];
        if (document.RootElement.TryGetProperty("actions", out JsonElement actions)
            && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement action in actions.EnumerateArray())
            {
                records.Add(new ActionRecord(
                    Text(action, "kind"),
                    Text(action, "outcome"),
                    Text(action, "path"),
                    Text(action, "reason")));
            }
        }

        return new ScaffoldReport(outcome, records);

        static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;
    }

    /// <summary>The profile one kind of the shipped manifest declares, or the empty string.</summary>
    private static string ProfileOf(JsonElement kinds, string kind) =>
        kinds.TryGetProperty(kind, out JsonElement declared)
            && declared.TryGetProperty("profile", out JsonElement profile)
                ? profile.GetString() ?? string.Empty
                : string.Empty;

    /// <summary>Whether a central package file pins this exact package to some version.</summary>
    private static bool IsPinned(string packagesText, string package) => Regex.IsMatch(
        packagesText,
        "<PackageVersion\\s[^>]*Include=\"" + Regex.Escape(package) + "\"[^>]*Version=\"[^\"]+\"",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// One notebook cell's source text.
    /// </summary>
    /// <remarks>
    /// The format allows either one string or an array of lines, and every tool writes both. A
    /// reader that handled only one of them would return the empty string for a notebook
    /// written the other way - and a check for what must not be in a cell passes on an empty
    /// string without having looked at a character of it.
    /// </remarks>
    private static string CellSource(JsonElement cell)
    {
        if (!cell.TryGetProperty("source", out JsonElement source))
        {
            return string.Empty;
        }

        if (source.ValueKind == JsonValueKind.String)
        {
            return source.GetString() ?? string.Empty;
        }

        if (source.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        foreach (JsonElement line in source.EnumerateArray())
        {
            text.Append(line.GetString() ?? string.Empty);
        }

        return text.ToString();
    }

    /// <summary>
    /// An emitted project path in the frozen shape: a directory directly under <c>src/</c> or
    /// <c>tests/</c>, named <c>&lt;Solution&gt;.&lt;Role&gt;</c>, holding a project file of the
    /// same name. The backreference is what holds the directory and the file together.
    /// </summary>
    [GeneratedRegex(@"\A(?:src|tests)/(" + Solution + @"\.[A-Za-z0-9]+)/\1\.csproj\z")]
    private static partial Regex FrozenProjectPath();

    // --- what a frozen layout and a required piece of text look like ------------------------------

    /// <summary>One action as the executor's report records it.</summary>
    private sealed record ActionRecord(string Kind, string Outcome, string Path, string Reason);

    /// <summary>One execution's report: its overall outcome and every action it recorded.</summary>
    private sealed record ScaffoldReport(string Outcome, IReadOnlyList<ActionRecord> Actions);

    /// <summary>One kind's share of the layout charter v3 froze.</summary>
    private sealed record FrozenLayout(
        string SolutionFile,
        IReadOnlyList<string> Projects,
        IReadOnlyList<string> OtherFiles);

    /// <summary>
    /// Something a requirement names, and the text that is there if the requirement is met. The
    /// <c>What</c> is what a failure reports, so it is written as the requirement's own words
    /// rather than as the string being searched for.
    /// </summary>
    private sealed record Expectation(string What, string Text);

    // --- one suite of the emitted solution ---------------------------------------------------------

    private sealed record Suite(
        string Project,
        string ProjectPath,
        bool IsIntegration,
        Repo.RunResult? Run,
        int Total,
        int Failed,
        string? NotRun);

    // --- the expensive part, done once per kind ------------------------------------------------------

    /// <summary>
    /// One kind, scaffolded into a temporary directory well outside this repository, then
    /// re-executed, gap-filled, restored, built and run - with every step's real output kept.
    /// </summary>
    private sealed class Scaffolded : IDisposable
    {
        /// <summary>The subtree R34's row deletes and expects back, and nothing else with it.</summary>
        public const string DeletedSubtree = "tests/";

        private const string Charter = """
            ---
            solution: Probe
            artifact_root: .sdlc
            status: approved
            ---
            # charter
            """;

        private static readonly object ProbeLock = new();
        private static bool probedForRuntime;
        private static string? containerRuntime;

        private Scaffolded(string kind, string enclosing, string target)
        {
            Kind = kind;
            Enclosing = enclosing;
            Target = target;
        }

        public string Kind { get; }

        public string Target { get; }

        private string Enclosing { get; }

        public string? SetupError { get; private set; }

        public Repo.RunResult FirstRun { get; private set; } = Never;

        public Repo.RunResult SecondRun { get; private set; } = Never;

        public Repo.RunResult RefillRun { get; private set; } = Never;

        public Repo.RunResult Restore { get; private set; } = Never;

        public Repo.RunResult Build { get; private set; } = Never;

        public IReadOnlyDictionary<string, string> AfterFirstRun { get; private set; } = Empty;

        public IReadOnlyDictionary<string, string> AfterSecondRun { get; private set; } = Empty;

        public IReadOnlyDictionary<string, string> AfterRefillRun { get; private set; } = Empty;

        public IReadOnlyList<string> Projects { get; private set; } = [];

        public IReadOnlyList<Suite> Suites { get; private set; } = [];

        /// <summary>
        /// The container runtime this machine has, or null. Probed once: the answer cannot
        /// change during a run, and R64 turns on it rather than on an assumption either way.
        /// </summary>
        public static string? ContainerRuntime
        {
            get
            {
                lock (ProbeLock)
                {
                    if (!probedForRuntime)
                    {
                        probedForRuntime = true;
                        containerRuntime = ProbeForContainerRuntime();
                    }

                    return containerRuntime;
                }
            }
        }

        private static Repo.RunResult Never => new(-1, string.Empty, "(not run)");

        private static IReadOnlyDictionary<string, string> Empty =>
            new Dictionary<string, string>(StringComparer.Ordinal);

        public static Scaffolded For(string kind)
        {
            string enclosing = Path.Combine(
                Path.GetTempPath(), "aisdlc-scaffold-" + Guid.NewGuid().ToString("N"));
            string target = Path.Combine(enclosing, "probe-repo");
            Scaffolded scaffold = new(kind, enclosing, target);

            try
            {
                scaffold.Run();
            }
#pragma warning disable CA1031 // A prepared-and-failed fixture must reach the rows as a message, not as a crash in class init.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                scaffold.SetupError = exception.ToString();
            }

            return scaffold;
        }

        public string Describe() =>
            Kind + " -> " + Target + Environment.NewLine
                + "  scaffold exit " + FirstRun.ExitCode.ToString(CultureInfo.InvariantCulture)
                + ", restore exit " + Restore.ExitCode.ToString(CultureInfo.InvariantCulture)
                + ", build exit " + Build.ExitCode.ToString(CultureInfo.InvariantCulture);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Enclosing))
                {
                    Directory.Delete(Enclosing, recursive: true);
                }
            }
            catch (IOException)
            {
                // A temp directory that will not delete is not a test failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Nor is one an anti-virus scanner still has a handle on.
            }
        }

        private void Run()
        {
            Directory.CreateDirectory(Path.Combine(Target, ".sdlc"));
            File.WriteAllText(Path.Combine(Target, ".sdlc", "CHARTER.md"), Charter);

            FirstRun = Execute();
            AfterFirstRun = Fingerprint();
            if (FirstRun.ExitCode != 0)
            {
                return;
            }

            SecondRun = Execute();
            AfterSecondRun = Fingerprint();

            string deleted = Path.Combine(
                Target, DeletedSubtree.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(deleted))
            {
                Directory.Delete(deleted, recursive: true);
            }

            RefillRun = Execute();
            AfterRefillRun = Fingerprint();

            Projects = Directory
                .EnumerateFiles(Target, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            string? solutionFile = SolutionFile();
            if (solutionFile is null)
            {
                SetupError = "no file matching the manifest's solution_file_glob under " + Target;
                return;
            }

            Restore = Dotnet(["restore", solutionFile]);
            Build = Dotnet(["build", solutionFile, "--nologo"]);
            if (Build.ExitCode == 0)
            {
                Suites = RunSuites();
            }
        }

        /// <summary>
        /// The executor, on the command line the plan freezes. The working directory is this
        /// repository rather than the target, so an executor that read the current directory
        /// instead of -Target would find the wrong charter and be caught.
        /// </summary>
        private Repo.RunResult Execute() => Exec(
            "powershell.exe",
            [
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Executor,
                "-Manifest", Manifest,
                "-Kind", Kind,
                "-Target", Target,
                "-Solution", Solution,
            ],
            Repo.Root);

        private Repo.RunResult Dotnet(IEnumerable<string> arguments) =>
            Exec("dotnet", arguments, Target);

        /// <summary>The solution file, found the way the manifest itself spells it.</summary>
        private string? SolutionFile()
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Manifest));
            string glob = manifest.RootElement.TryGetProperty("solution_file_glob", out JsonElement value)
                ? value.GetString() ?? "*"
                : "*";

            return Directory
                .EnumerateFiles(Target, glob, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>Every file under the target, by relative path, with its content hash.</summary>
        private IReadOnlyDictionary<string, string> Fingerprint()
        {
            Dictionary<string, string> tree = new(StringComparer.Ordinal);
            if (!Directory.Exists(Target))
            {
                return tree;
            }

            foreach (string path in Directory.EnumerateFiles(Target, "*", SearchOption.AllDirectories))
            {
                tree[Path.GetRelativePath(Target, path).Replace('\\', '/')] =
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
            }

            return tree;
        }

        /// <summary>
        /// Runs each emitted test project and reads the count out of a TRX rather than off the
        /// console, because the count is the thing R32 is about and console formatting is not
        /// a contract.
        /// </summary>
        private IReadOnlyList<Suite> RunSuites()
        {
            List<Suite> suites = [];
            string results = Path.Combine(Enclosing, "results");
            Directory.CreateDirectory(results);

            foreach (string project in Projects.Where(
                p => Path.GetRelativePath(Target, p).Replace('\\', '/')
                    .StartsWith(DeletedSubtree, StringComparison.Ordinal)))
            {
                string name = Path.GetFileNameWithoutExtension(project);
                bool isIntegration = name.EndsWith("." + IntegrationRole, StringComparison.OrdinalIgnoreCase);

                if (isIntegration && ContainerRuntime is null)
                {
                    // R64's only downgrade. The suite compiled - the build is what proves that -
                    // and the row above prints this reason rather than letting a green run imply
                    // the composed system was exercised.
                    suites.Add(new Suite(
                        name,
                        project,
                        IsIntegration: true,
                        Run: null,
                        Total: 0,
                        Failed: 0,
                        NotRun: "no container runtime on this machine, so the integration suite "
                            + "was compiled and not run"));
                    continue;
                }

                string trx = Path.Combine(results, name + ".trx");
                if (File.Exists(trx))
                {
                    File.Delete(trx);
                }

                Repo.RunResult run = RunOneSuite(project, name, trx);
                (int total, int failed) = ReadCounters(trx);
                suites.Add(new Suite(name, project, isIntegration, run, total, failed, NotRun: null));
            }

            return suites;
        }

        /// <summary>
        /// A test project either produces an executable test host or it does not, and the two
        /// are run differently. Detected from what the build actually produced rather than
        /// from a property this test would have to keep in step with the templates.
        /// </summary>
        private Repo.RunResult RunOneSuite(string project, string name, string trx)
        {
            string projectDirectory = Path.GetDirectoryName(project)!;
            string bin = Path.Combine(projectDirectory, "bin");
            string? host = Directory.Exists(bin)
                ? Directory.EnumerateFiles(bin, name + ".exe", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .FirstOrDefault()
                : null;

            return host is not null
                ? Exec(
                    host,
                    [
                        "--report-trx",
                        "--report-trx-filename", Path.GetFileName(trx),
                        "--results-directory", Path.GetDirectoryName(trx)!,
                    ],
                    projectDirectory)
                : Exec(
                    "dotnet",
                    [
                        "test", project, "--nologo",
                        "--logger", "trx;LogFileName=" + Path.GetFileName(trx),
                        "--results-directory", Path.GetDirectoryName(trx)!,
                    ],
                    Target);
        }

        private static (int Total, int Failed) ReadCounters(string trx)
        {
            if (!File.Exists(trx))
            {
                return (0, 0);
            }

            XDocument document = XDocument.Load(trx);
            XElement? counters = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Counters");
            if (counters is null)
            {
                return (0, 0);
            }

            return (Attribute(counters, "total"), Attribute(counters, "failed"));

            static int Attribute(XElement element, string name)
            {
                string? value = element.Attribute(name)?.Value;
                return int.TryParse(value, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
            }
        }

        /// <summary>
        /// Whether this machine can start containers. Probed, never assumed: assuming it can
        /// turns a real gap into a failure nobody can act on, and assuming it cannot is how the
        /// integration half quietly stops being run anywhere.
        /// </summary>
        private static string? ProbeForContainerRuntime()
        {
            string[] candidates = ["docker", "podman"];
            foreach (string candidate in candidates)
            {
                try
                {
                    // `info` talks to the daemon. `--version` would answer from the client
                    // binary alone and say yes on a machine where nothing can actually start.
                    Repo.RunResult probe = Exec(
                        candidate, ["info"], Repo.Root, TimeSpan.FromMinutes(2));
                    if (probe.ExitCode == 0)
                    {
                        return candidate;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Not on PATH: try the next one.
                }
                catch (TimeoutException)
                {
                    // A runtime that will not answer is a runtime that cannot start a container.
                }
            }

            return null;
        }
    }

    // --- running an external process for as long as a real build takes --------------------------------

    /// <summary>
    /// The same shape as <see cref="Repo.Run"/>, with a timeout a restore and a build can
    /// actually finish inside. The shared helper caps at sixty seconds, which is right for the
    /// hooks and scripts it was written for and far too short for anything here.
    /// </summary>
    private static Repo.RunResult Exec(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan? timeout = null)
    {
        ProcessStartInfo info = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment.Remove("SDLC_GATE");
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_NOLOGO"] = "1";

        // Without this, MSBuild leaves worker nodes alive holding handles on the temp tree and
        // the fixture cannot delete what it created.
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("failed to start " + fileName);

        StringBuilder stdout = new();
        StringBuilder stderr = new();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.StandardInput.Close();

        TimeSpan limit = timeout ?? ToolTimeout;
        if (!process.WaitForExit((int)limit.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                fileName + " did not exit within "
                    + limit.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes");
        }

        process.WaitForExit();

        return new Repo.RunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

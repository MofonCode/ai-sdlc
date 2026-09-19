using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// Behaviour of the shipped scaffold executor, driven as the external process a session runs.
/// </summary>
/// <remarks>
/// Fixture manifests with stub commands, so the whole class runs in seconds and needs no
/// toolchain beyond this test project. The manifest the pack actually ships is proved
/// elsewhere, by building what it emits; what is proved here is the engine: that it refuses
/// what it should refuse, writes only where it is allowed to, and can be run twice.
/// </remarks>
[TestClass]
public sealed class ScaffoldManifestTests
{
    /// <summary>The token a fixture manifest declares. The engine never spells one itself.</summary>
    private const string Token = "{{SOLUTION}}";

    private const int KnownReportVersion = 1;

    /// <summary>A glob no fixture creates a file for, so the action it belongs to has nothing to do.</summary>
    private const string NeverMatches = "src/never-generated-*.txt";

    private static string Executor => Path.Combine(
        Repo.Root, "plugins", "sdlc-core", "scripts", "Invoke-ScaffoldManifest.ps1");

    // --- fixtures ----------------------------------------------------------------------

    /// <summary>A pack: a manifest, its assets, and the command line that executes them.</summary>
    private sealed class Pack : IDisposable
    {
        private Pack(string directory) => Directory = directory;

        public string Directory { get; }

        public string ManifestPath => Path.Combine(Directory, "scaffold.json");

        public static Pack Create()
        {
            string directory = Path.Combine(
                Path.GetTempPath(), "aisdlc-pack-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            return new Pack(directory);
        }

        public Pack WithManifest(string json)
        {
            File.WriteAllText(ManifestPath, json);
            return this;
        }

        public Pack WithAsset(string name, string content)
        {
            File.WriteAllText(Path.Combine(Directory, name), content);
            return this;
        }

        /// <summary>
        /// Runs the executor the way the plan freezes the command line.
        /// </summary>
        /// <remarks>
        /// The working directory is this repository, deliberately not the target: the
        /// executor must resolve the charter from -Target. An executor that read the
        /// current directory instead would find this repository's own approved charter and
        /// every gate test below would pass while proving nothing.
        /// </remarks>
        public Repo.RunResult Execute(string target, string? kind = "demo", string? solution = null)
        {
            List<string> args = ["-Manifest", ManifestPath, "-Target", target];
            if (kind is not null)
            {
                args.Add("-Kind");
                args.Add(kind);
            }

            if (solution is not null)
            {
                args.Add("-Solution");
                args.Add(solution);
            }

            return Repo.PowerShell(Executor, args);
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A temp directory that will not delete is not a test failure.
            }
        }
    }

    /// <summary>
    /// A target repository with an approved charter, whose directory name the test chooses.
    /// </summary>
    /// <remarks>
    /// <see cref="TempRepo"/> names itself after a GUID, and half of these tests are about
    /// the identifier derived from the directory name.
    /// </remarks>
    private sealed class Target : IDisposable
    {
        /// <remarks>
        /// The root is a parameter because a charter declares its own: a fixture that could
        /// only build a <c>.sdlc</c> repository could not tell an executor that resolves the
        /// root from one that hardcodes the name.
        /// </remarks>
        private const string CharterTemplate = """
            ---
            solution: Fixture
            artifact_root: {0}
            status: approved
            ---
            # charter
            """;

        private Target(string enclosing, string path, string artifactRoot)
        {
            Enclosing = enclosing;
            Path = path;
            ArtifactRoot = artifactRoot;
        }

        private string Enclosing { get; }

        public string Path { get; }

        /// <summary>Where this repository keeps its chain, relative and forward-slashed.</summary>
        public string ArtifactRoot { get; }

        public static Target Create(string name = "demo-repo", string artifactRoot = ".sdlc")
        {
            string enclosing = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "aisdlc-target-" + Guid.NewGuid().ToString("N"));
            string path = System.IO.Path.Combine(enclosing, name);
            string root = System.IO.Path.Combine(
                path, artifactRoot.Replace('/', System.IO.Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(root);
            File.WriteAllText(
                System.IO.Path.Combine(root, "CHARTER.md"),
                string.Format(System.Globalization.CultureInfo.InvariantCulture, CharterTemplate, artifactRoot));
            return new Target(enclosing, path, artifactRoot);
        }

        public string PathTo(string relative) =>
            System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

        public void Put(string relative, string content)
        {
            string full = PathTo(relative);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        public bool Has(string relative) => File.Exists(PathTo(relative));

        public string Read(string relative) => File.ReadAllText(PathTo(relative));

        /// <summary>Every file under the target bar the chain, relative and forward-slashed.</summary>
        public IReadOnlyList<string> Files() =>
            System.IO.Directory
                .EnumerateFiles(Path, "*", SearchOption.AllDirectories)
                .Select(f => System.IO.Path.GetRelativePath(Path, f).Replace('\\', '/'))
                .Where(f => !f.StartsWith(ArtifactRoot + "/", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

        /// <summary>Content hashes of every file under the target, for a re-run comparison.</summary>
        public string Fingerprint() =>
            string.Join(
                "|",
                Files().Select(f => f + ":" + Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(PathTo(f))))));

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Enclosing))
                {
                    System.IO.Directory.Delete(Enclosing, recursive: true);
                }
            }
            catch (IOException)
            {
                // A temp directory that will not delete is not a test failure.
            }
        }
    }

    // --- the manifest fixtures ------------------------------------------------------------

    private static string Manifest(string actions, string preflight = "[]", string version = "1") =>
        "{ \"version\": " + version
        + ", \"solution_token\": \"" + Token + "\""
        + ", \"solution_file_glob\": \"*.solution\""
        + ", \"kinds\": { \"demo\": { \"description\": \"fixture\", \"profile\": \"none\""
        + ", \"preflight\": " + preflight
        + ", \"actions\": [" + actions + "] } } }";

    /// <summary>A stub generator: a command that writes a file, standing in for a real one.</summary>
    private static string Generate(string path, string content, string? unless = null) =>
        """{ "kind": "run", "command": "powershell.exe", "args": ["-NoProfile", "-Command", "$d = Split-Path -Parent 'PATH'; if ($d) { New-Item -ItemType Directory -Force -Path $d | Out-Null }; Set-Content -LiteralPath 'PATH' -Value 'BODY'"]"""
            .Replace("PATH", path, StringComparison.Ordinal)
            .Replace("BODY", content, StringComparison.Ordinal)
        + (unless is null ? "" : ", \"unless_exists\": \"" + unless + "\"")
        + " }";

    /// <summary>
    /// A stub generator that writes the token into a file that is already there, the way a
    /// real one modifies a file it did not create.
    /// </summary>
    /// <remarks>
    /// The token is assembled from pieces inside the command so the manifest's own text never
    /// contains it. The executor substitutes the declared token through the whole manifest
    /// document before any action runs, so a literal token in an argument would be replaced
    /// before the generator ever saw it - and the row would prove nothing.
    /// </remarks>
    private static string GeneratorThatWritesTheTokenInto(string path) =>
        """{ "kind": "run", "command": "powershell.exe", "args": ["-NoProfile", "-Command", "Add-Content -LiteralPath 'PATH' -Value ('{{' + 'SOLUTION' + '}}')"] }"""
            .Replace("PATH", path, StringComparison.Ordinal);

    private static string Copy(string asset, string path, bool tokens = false) =>
        "{ \"kind\": \"copy\", \"asset\": \"" + asset + "\", \"path\": \"" + path + "\""
        + (tokens ? ", \"tokens\": [\"solution\"]" : "")
        + " }";

    private static string Append(string asset, string path) =>
        "{ \"kind\": \"append\", \"asset\": \"" + asset + "\", \"path\": \"" + path + "\" }";

    private static string Rewrite(string glob, string pattern, string replacement, string? assertAfter = null) =>
        "{ \"kind\": \"rewrite\", \"glob\": \"" + glob + "\", \"pattern\": \"" + pattern
        + "\", \"replacement\": \"" + replacement + "\""
        + (assertAfter is null ? "" : ", \"assert_after\": \"" + assertAfter + "\"")
        + " }";

    private static string Delete(string glob) =>
        "{ \"kind\": \"delete\", \"glob\": \"" + glob + "\" }";

    // --- reading the report ------------------------------------------------------------------

    private sealed record Recorded(string Kind, string Outcome, string Path, string Command, string Reason);

    /// <summary>
    /// A consumer of the report, including the half that matters: it rejects a schema version
    /// it does not know rather than reading fields it hopes are still there.
    /// </summary>
    private static JsonDocument? Accept(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("report_version", out JsonElement version)
            || version.ValueKind != JsonValueKind.Number
            || version.GetInt32() != KnownReportVersion)
        {
            document.Dispose();
            return null;
        }

        return document;
    }

    private static JsonDocument Report(Repo.RunResult result)
    {
        JsonDocument? document = Accept(result.StdOut);
        Assert.IsNotNull(document, "no report this consumer accepts on stdout: " + result.Combined);
        return document;
    }

    private static string Field(JsonDocument report, string name) =>
        report.RootElement.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static IReadOnlyList<Recorded> Actions(JsonDocument report)
    {
        List<Recorded> actions = [];
        if (!report.RootElement.TryGetProperty("actions", out JsonElement entries))
        {
            return actions;
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            actions.Add(new Recorded(
                Text(entry, "kind"),
                Text(entry, "outcome"),
                Text(entry, "path"),
                Text(entry, "command"),
                Text(entry, "reason")));
        }

        return actions;

        static string Text(JsonElement entry, string name) =>
            entry.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? "" : "";
    }

    // --- the approval gate (R38, R39, R40) -----------------------------------------------------

    [TestMethod]
    public void DraftCharter_WritesNothingAndExitsNonZero()
    {
        using TempRepo repo = TempRepo.Create(
            planStatus: null, charterRoot: ".sdlc", charterStatus: "draft");
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(repo.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(
            File.Exists(Path.Combine(repo.Path, "README.md")),
            "a draft charter must leave the tree untouched");
        StringAssert.Contains(result.StdErr, "draft", StringComparison.Ordinal);

        using JsonDocument report = Report(result);
        Assert.AreEqual("halted", Field(report, "outcome"));
    }

    [TestMethod]
    public void NoCharter_ExitsNonZero()
    {
        // The chain directory exists, the charter does not: a scaffold with nothing behind it.
        using TempRepo repo = TempRepo.Create(planStatus: null);
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(repo.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "README.md")));
        StringAssert.Contains(result.StdErr, "charter", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NoChainAtAll_ExitsNonZero()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(repo.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "README.md")));
    }

    [TestMethod]
    public void Report_NamesTheArtifactRootItResolved()
    {
        // A non-default root, so a report naming ".sdlc" would be naming an assumption.
        using TempRepo repo = TempRepo.Create(
            rootDirectory: "docs/sdlc", planStatus: null, charterRoot: "docs/sdlc/");
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(repo.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);
        StringAssert.Contains(
            Field(report, "artifact_root").Replace('\\', '/'),
            "docs/sdlc",
            StringComparison.Ordinal);
        StringAssert.Contains(
            Field(report, "target").Replace('\\', '/'),
            repo.Path.Replace('\\', '/'),
            StringComparison.Ordinal);
    }

    // --- halting (R3) ---------------------------------------------------------------------------

    [TestMethod]
    public void FailingAction_HaltsBeforeTheActionsAfterIt()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            Generate("first.txt", "one") + ","
            + """{ "kind": "run", "command": "powershell.exe", "args": ["-NoProfile", "-Command", "exit 3"] },"""
            + Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(target.Has("first.txt"), "the action before the failure should have run");
        Assert.IsFalse(target.Has("README.md"), "the action after the failure must not have run");

        using JsonDocument report = Report(result);
        Assert.AreEqual("halted", Field(report, "outcome"));
        Recorded failure = Actions(report).Single(a => a.Outcome == "failed");
        Assert.AreEqual("run", failure.Kind);
        StringAssert.Contains(failure.Command, "powershell", StringComparison.Ordinal);
        StringAssert.Contains(failure.Reason, "3", StringComparison.Ordinal);
    }

    // --- containment (R4, R44, R63) -----------------------------------------------------------------

    [TestMethod]
    [DataRow("copy")]
    [DataRow("append")]
    [DataRow("rewrite")]
    [DataRow("delete")]
    public void PathOutsideTheTarget_IsRefusedAndNamed(string kind)
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        string action = kind switch
        {
            "copy" => Copy("readme.txt", "../escaped.txt"),
            "append" => Append("readme.txt", "../escaped.txt"),
            "rewrite" => Rewrite("../*.txt", "hello", "bye"),
            _ => Delete("../*.txt"),
        };
        pack.WithManifest(Manifest(action));

        string outside = Path.Combine(Path.GetDirectoryName(target.Path)!, "escaped.txt");
        File.WriteAllText(outside, "hello");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.AreEqual("hello", File.ReadAllText(outside), "nothing outside the target may change");
        StringAssert.Contains(result.StdErr, "..", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, "outside the target", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RunWhoseWorkingDirectoryEscapesTheTarget_IsRefused()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            """{ "kind": "run", "command": "powershell.exe", "args": ["-NoProfile", "-Command", "Set-Content -LiteralPath loose.txt -Value 'x'"], "cwd": "../elsewhere" }"""));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, "outside the target", StringComparison.Ordinal);
        Assert.IsFalse(
            Directory.Exists(Path.Combine(Path.GetDirectoryName(target.Path)!, "elsewhere")),
            "the refused working directory must not have been created");
    }

    // --- never overwrite (R5) ---------------------------------------------------------------------

    [TestMethod]
    public void ExistingDestination_IsSkippedAndLeftByteIdentical()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "from the pack");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));
        target.Put("README.md", "mine, and older than this run");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.AreEqual("mine, and older than this run", target.Read("README.md"));

        using JsonDocument report = Report(result);
        Recorded record = Actions(report).Single(a => a.Kind == "copy");
        Assert.AreEqual("skipped", record.Outcome);
        Assert.AreEqual("README.md", record.Path);
    }

    // --- re-runnable (R7, R43) ---------------------------------------------------------------------

    [TestMethod]
    public void SecondExecution_ChangesNoFileAndExitsZero()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create()
            .WithAsset("readme.txt", "# " + Token)
            .WithAsset("ignore.txt", "# pack entries\n*.tmp\nsecret/\n");
        pack.WithManifest(Manifest(
            """{ "kind": "mkdir", "path": "src" },"""
            + Generate("src/made.txt", "pinned-0.0.1 keep", unless: "src/made.txt") + ","
            + Copy("readme.txt", "README.md", tokens: true) + ","
            + Append("ignore.txt", ".gitignore") + ","
            + Rewrite("src/made.txt", "pinned-[0-9.]+ ", "", assertAfter: "keep") + ","
            + Delete("src/scratch.txt")));

        Assert.AreEqual(0, pack.Execute(target.Path).ExitCode);
        string before = target.Fingerprint();

        Repo.RunResult second = pack.Execute(target.Path);

        Assert.AreEqual(0, second.ExitCode, second.Combined);
        Assert.AreEqual(before, target.Fingerprint(), "a second run must change no file");

        using JsonDocument report = Report(second);
        Assert.AreEqual("completed", Field(report, "outcome"));
        Assert.IsTrue(
            Actions(report).All(a => a.Outcome == "skipped"),
            "every action should have found its work already done");
    }

    // --- preflight (R9, R30, R57) ----------------------------------------------------------------------

    [TestMethod]
    public void FailingPreflight_WritesNothingAndRelaysTheRemediation()
    {
        const string Remediation = "install the widget yourself: widgetctl add thing";
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            Copy("readme.txt", "README.md"),
            preflight: """
                [ { "command": "powershell.exe", "args": ["-NoProfile", "-Command", "exit 9"],
                    "expect_exit": 0, "remediation": "REMEDIATION" } ]
                """.Replace("REMEDIATION", Remediation, StringComparison.Ordinal)));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"), "a failed preflight writes nothing at all");
        StringAssert.Contains(result.StdErr, Remediation, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PassingPreflight_LetsTheActionsRun()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            Copy("readme.txt", "README.md"),
            preflight: """
                [ { "command": "powershell.exe", "args": ["-NoProfile", "-Command", "Write-Output 'widget 1.2'"],
                    "expect_exit": 0, "expect_match": "widget", "remediation": "install the widget" } ]
                """));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(target.Has("README.md"));
    }

    [TestMethod]
    public void PreflightThatDoesNotMatchItsPattern_Fails()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            Copy("readme.txt", "README.md"),
            preflight: """
                [ { "command": "powershell.exe", "args": ["-NoProfile", "-Command", "Write-Output 'nothing here'"],
                    "expect_exit": 0, "expect_match": "widget", "remediation": "install the widget" } ]
                """));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
    }

    [TestMethod]
    public void PreflightDeclaredInsideActions_IsRejected()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            """{ "kind": "preflight", "command": "powershell.exe", "args": ["-NoProfile", "-Command", "exit 0"], "remediation": "x" },"""
            + Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
        StringAssert.Contains(result.StdErr, "preflight", StringComparison.Ordinal);
    }

    // --- the kind and the schema version (R12) ---------------------------------------------------------

    [TestMethod]
    public void UnknownKind_ListsTheKindsTheManifestDeclares()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(target.Path, kind: "nonesuch");

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
        StringAssert.Contains(result.StdErr, "demo", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NoKindAtAll_ListsTheKindsTheManifestDeclares()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(target.Path, kind: null);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
        StringAssert.Contains(result.StdErr, "demo", StringComparison.Ordinal);
    }

    [TestMethod]
    public void UnknownManifestVersion_IsRefusedBeforeAnyAction()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md"), version: "99"));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
        StringAssert.Contains(result.StdErr, "99", StringComparison.Ordinal);
    }

    // --- what a rewrite and a delete may touch (R41, R42, R58) ---------------------------------------------

    [TestMethod]
    public void RewriteAndDelete_TouchOnlyWhatThisRunCreated()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            Generate("made.txt", "pinned-0.0.1 keep") + ","
            + Rewrite("*.txt", "pinned-[0-9.]+ ", "") + ","
            + Delete("scratch*.txt")));

        // Both match the globs, and neither was created by this run.
        target.Put("kept.txt", "pinned-0.0.1 keep");
        target.Put("scratch-mine.txt", "not this run's to remove");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.AreEqual("pinned-0.0.1 keep", target.Read("kept.txt").Trim());
        Assert.IsTrue(target.Has("scratch-mine.txt"), "a pre-existing file is not this run's to delete");
        StringAssert.Contains(target.Read("made.txt"), "keep", StringComparison.Ordinal);
        Assert.IsFalse(
            target.Read("made.txt").Contains("pinned", StringComparison.Ordinal),
            "the file this run created should have been rewritten");

        using JsonDocument report = Report(result);
        Recorded mutated = Actions(report).Single(a => a.Outcome == "mutated");
        Assert.AreEqual("made.txt", mutated.Path);
    }

    [TestMethod]
    public void DeleteRemovesWhatThisRunCreated_AndSaysSo()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            Generate("sample-page.txt", "demo content") + ","
            + Delete("sample-*.txt")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("sample-page.txt"));

        using JsonDocument report = Report(result);
        Recorded removed = Actions(report).Single(a => a.Outcome == "removed");
        Assert.AreEqual("delete", removed.Kind);
        Assert.AreEqual("sample-page.txt", removed.Path);
    }

    [TestMethod]
    public void AFileThisRunOnlyAppendedTo_IsNotTheRunsToRewriteOrDelete()
    {
        // The decision behind R71 keeping a second set rather than widening R41's. The sweep
        // is answerable for lines this run appended; `rewrite` and `delete` are still bound to
        // what it created. An `append` destination was already on disk, so its existing
        // content is somebody else's - one set serving both questions would let an over-broad
        // `delete` glob remove a mature repository's ignore file because the pack added two
        // lines to it, which is the harm R41 exists to prevent.
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("ignore.txt", "*.tmp\n");
        pack.WithManifest(Manifest(
            Append("ignore.txt", "kept.gitignore") + ","
            + Rewrite("*.gitignore", "mine", "theirs") + ","
            + Delete("*.gitignore")));

        target.Put("kept.gitignore", "mine\n");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(target.Has("kept.gitignore"), "an appended file is not this run's to delete");
        StringAssert.Contains(
            target.Read("kept.gitignore"), "mine", StringComparison.Ordinal);

        // Not vacuous: the append did happen, so the file really was written to this run.
        StringAssert.Contains(
            target.Read("kept.gitignore"), "*.tmp", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AssertAfterThatDoesNotHold_FailsTheRunNamingTheFile()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            Generate("made.txt", "the template changed shape") + ","
            + Rewrite("*.txt", "nothing-matches-this", "x", assertAfter: "pinned-9")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, "made.txt", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, "assert_after", StringComparison.Ordinal);
    }

    // --- append (R45, R46) --------------------------------------------------------------------------------

    [TestMethod]
    public void Append_CreatesTheFileWhenItIsAbsent()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("ignore.txt", "# pack entries\n*.tmp\nsecret/\n");
        pack.WithManifest(Manifest(Append("ignore.txt", ".gitignore")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(target.Read(".gitignore"), "*.tmp", StringComparison.Ordinal);

        using JsonDocument report = Report(result);
        Assert.AreEqual("created", Actions(report).Single(a => a.Kind == "append").Outcome);
    }

    [TestMethod]
    public void Append_AddsOnlyTheLinesTheFileDoesNotAlreadyHave()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("ignore.txt", "# pack entries\n*.tmp\nsecret/\n");
        pack.WithManifest(Manifest(Append("ignore.txt", ".gitignore")));
        target.Put(".gitignore", "*.tmp\nbuild-output/\n");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        string ignore = target.Read(".gitignore");
        Assert.AreEqual(
            1,
            ignore.Split('\n').Count(l => l.Trim() == "*.tmp"),
            "a line already present must not be added twice");
        StringAssert.Contains(ignore, "secret/", StringComparison.Ordinal);
        StringAssert.Contains(ignore, "build-output/", StringComparison.Ordinal);

        using JsonDocument report = Report(result);
        Assert.AreEqual("mutated", Actions(report).Single(a => a.Kind == "append").Outcome);
    }

    [TestMethod]
    public void Append_OfAnAssetThatIsNotPlainAscii_SurvivesARerun()
    {
        // The regression this pins: reading an asset in the machine's code page instead of
        // UTF-8 writes the line back double-encoded, so on the next run it no longer matches
        // what the asset says and is appended again - for ever, and with the emitted file
        // corrupted into the bargain.
        const string Entries = "# state — never commit it\n*.tfstate\n";
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("ignore.txt", Entries);
        pack.WithManifest(Manifest(Append("ignore.txt", ".gitignore")));

        Assert.AreEqual(0, pack.Execute(target.Path).ExitCode);
        string written = target.Read(".gitignore");
        StringAssert.Contains(written, "—", StringComparison.Ordinal);

        string before = target.Fingerprint();
        Repo.RunResult second = pack.Execute(target.Path);

        Assert.AreEqual(0, second.ExitCode, second.Combined);
        Assert.AreEqual(before, target.Fingerprint(), "the second run must change nothing");
    }

    [TestMethod]
    public void Append_IsASkippedNoOpWhenEveryLineIsPresent()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("ignore.txt", "# pack entries\n*.tmp\nsecret/\n");
        pack.WithManifest(Manifest(Append("ignore.txt", ".gitignore")));
        target.Put(".gitignore", "# pack entries\n*.tmp\nsecret/\n");
        string before = target.Fingerprint();

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.AreEqual(before, target.Fingerprint(), "not one byte may change");

        using JsonDocument report = Report(result);
        Assert.AreEqual("skipped", Actions(report).Single(a => a.Kind == "append").Outcome);
    }

    // --- the token sweep (R50) ------------------------------------------------------------------------------

    [TestMethod]
    public void TokenLeftByAnEarlierRun_FailsTheRunNamingFileAndToken()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        // This run does not write it, and a gap-filling run is exactly when nobody looks.
        target.Put("src/Stale.txt", "assembly = " + Token + ".Thing");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, "src/Stale.txt", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, Token, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(".sdlc")]
    [DataRow("docs/sdlc")]
    public void TokenInAChainFileThisRunDidNotCreate_DoesNotHaltTheRun(string artifactRoot)
    {
        // R70, first of three. The chain lives under the target, and its own documents quote
        // a pack's token while specifying it - a spec that says what the token is contains the
        // token. Sweeping it halted on the engine's own paperwork, and the gap-filling re-run
        // R16 offers was impossible in any repository that had ever used this process.
        //
        // Both roots, because the charter declares its own: an executor that skipped a literal
        // ".sdlc" would still fail on a repository that keeps its chain elsewhere.
        using Target target = Target.Create(artifactRoot: artifactRoot);
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        target.Put(artifactRoot + "/spec.md", "the pack declares " + Token + " as its token");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(
            result.StdErr.Contains("spec.md", StringComparison.Ordinal),
            "the chain is not scaffolded output and is not the sweep's to read: " + result.StdErr);
    }

    [TestMethod]
    [DataRow(".sdlc")]
    [DataRow("docs/sdlc")]
    public void TokenInAChainFileThisRunCreated_HaltsTheRun(string artifactRoot)
    {
        // R70, second of three, and the one that distinguishes it from the requirement it
        // replaces: the code R70 supersedes passes the other two rows. The chain is not swept
        // wholesale because its own documents quote a token while specifying it - but "no
        // manifest action writes into the chain" is an assumption about every pack that will
        // ever exist, and nothing enforces it. The boundary is authorship, not location.
        using Target target = Target.Create(artifactRoot: artifactRoot);
        using Pack pack = Pack.Create().WithAsset("note.txt", "built for " + Token);

        // The pack mistake R70 exists to catch: an asset carrying the token copied into the
        // chain with `tokens` not declared, so the placeholder reaches disk unsubstituted.
        pack.WithManifest(Manifest(Copy("note.txt", artifactRoot + "/generated.md")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, artifactRoot + "/generated.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, Token, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(".sdlc")]
    [DataRow("docs/sdlc")]
    public void TokenInAChainFileThisRunAppendedTo_HaltsTheRun(string artifactRoot)
    {
        // A declared `append` to a chain file, which an earlier rule missed by naming `create`
        // as the only way this run could write one. Under R72 it is caught for the same reason
        // anything else is: the file is there before the run and different after.
        //
        // The charter is the realistic destination: `init` always writes it before the
        // executor runs, so a pack appending a section to it takes the append path on the
        // very first run rather than on some unlikely later one.
        using Target target = Target.Create(artifactRoot: artifactRoot);
        using Pack pack = Pack.Create().WithAsset("note.txt", "built for " + Token);
        pack.WithManifest(Manifest(Append("note.txt", artifactRoot + "/CHARTER.md")));

        Repo.RunResult result = pack.Execute(target.Path);

        // The destination existed, so this proves the append-to-existing path and not the
        // create path, which registers its file the other way and would pass for free.
        StringAssert.Contains(result.StdErr, "appended", StringComparison.Ordinal);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, artifactRoot + "/CHARTER.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, Token, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(".sdlc")]
    [DataRow("docs/sdlc")]
    public void TokenAGeneratorWroteIntoAPreExistingChainFile_HaltsTheRun(string artifactRoot)
    {
        // R72, and the only part that distinguishes it from R71. A `run` action's generator
        // modifying a chain file that was already on disk writes bytes no registration path
        // records: the executor registers generator-*created* files, so its own model of
        // "this run wrote it" used to differ between two outcomes of one action - halting
        // when the generator created the file and exiting 0 when it modified one.
        //
        // R72 asks about content instead, so it cannot be short a mechanism.
        using Target target = Target.Create(artifactRoot: artifactRoot);
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(GeneratorThatWritesTheTokenInto(artifactRoot + "/notes.md")));

        // On disk before the run begins, so it is in the pre-run snapshot and neither created
        // nor a declared `append`.
        target.Put(artifactRoot + "/notes.md", "# notes\n");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, artifactRoot + "/notes.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, Token, StringComparison.Ordinal);

        // The token really did reach disk, so the row cannot pass on a generator that failed.
        StringAssert.Contains(target.Read(artifactRoot + "/notes.md"), Token, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(".sdlc")]
    [DataRow("docs/sdlc")]
    public void TokenOutsideTheArtifactRoot_StillHaltsTheRun(string artifactRoot)
    {
        // R70, third of three, and the one that keeps the exception from becoming "stopped
        // sweeping": same token, same manifest, same fixture, same filename as the first row -
        // only the directory differs. A set that proved the exception alone would pass on an
        // executor that had given up on the sweep entirely, which is the worse defect.
        using Target target = Target.Create(artifactRoot: artifactRoot);
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        target.Put("src/spec.md", "the pack declares " + Token + " as its token");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, "src/spec.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, Token, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SubstitutedToken_LeavesNothingBehind()
    {
        using Target target = Target.Create(name: "widget-shop");
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token + "\n\nbuilt by " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(target.Read("README.md"), "# WidgetShop", StringComparison.Ordinal);
        Assert.IsFalse(target.Read("README.md").Contains("{{", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TokenInAManifestPathOrArgument_IsSubstitutedBeforeTheActionRuns()
    {
        // The emitted layout names directories and projects after the solution identifier,
        // so the token has to reach the manifest's own strings and not only the contents of
        // the assets it copies. A `tokens` field says what an asset wants substituted inside
        // it; it says nothing about where the manifest points.
        using Target target = Target.Create(name: "widget-shop");
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            """{ "kind": "mkdir", "path": "src/{{SOLUTION}}.Thing" },"""
            + Generate("src/{{SOLUTION}}.Thing/made.txt", "body", unless: "src/{{SOLUTION}}.Thing/made.txt")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(
            target.Has("src/WidgetShop.Thing/made.txt"),
            "expected the substituted path, got: " + string.Join(", ", target.Files()));

        // And the second run finds it, which is what makes unless_exists worth declaring.
        using JsonDocument report = Report(pack.Execute(target.Path));
        Assert.IsTrue(Actions(report).All(a => a.Outcome == "skipped"));
    }

    [TestMethod]
    public void TokenInAManifestString_IsSubstitutedFromTheSuppliedIdentifier()
    {
        // The frozen layout names a directory per project after the solution identifier, and
        // the identifier the owner confirmed is the one that has to land in it. The rewrite
        // here is the part that proves the glob was expanded too: against an unexpanded glob
        // it would match nothing and the file would still say "body".
        using Target target = Target.Create(name: "widget-shop");
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(
            """{ "kind": "mkdir", "path": "src/{{SOLUTION}}.Api" },"""
            + Generate("src/{{SOLUTION}}.Api/Program.gen", "body") + ","
            + Copy("readme.txt", "src/{{SOLUTION}}.Api/README.md", tokens: true) + ","
            + Rewrite("src/{{SOLUTION}}.Api/*.gen", "body", "kept")));

        Repo.RunResult result = pack.Execute(target.Path, solution: "Confirmed");

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(
            target.Has("src/Confirmed.Api/Program.gen"),
            "expected the supplied identifier in the path, got: " + string.Join(", ", target.Files()));
        Assert.IsFalse(
            target.Has("src/WidgetShop.Api/Program.gen"),
            "a supplied identifier wins over the one derived from the directory name");
        Assert.AreEqual("kept", target.Read("src/Confirmed.Api/Program.gen").Trim());
        Assert.AreEqual("# Confirmed", target.Read("src/Confirmed.Api/README.md").Trim());
        Assert.IsFalse(
            target.Files().Any(f => f.Contains("{{", StringComparison.Ordinal)),
            "no token may survive in a path: " + string.Join(", ", target.Files()));
    }

    // --- the solution identifier (R17, R18, R19, R51, R52, R53) ------------------------------------------------

    [TestMethod]
    [DataRow("widget-shop", "WidgetShop")]
    [DataRow("widget_shop.core", "WidgetShopCore")]
    [DataRow("Widget Shop", "WidgetShop")]
    [DataRow("widget42", "Widget42")]
    public void Identifier_IsDerivedFromTheDirectoryName(string directory, string expected)
    {
        using Target target = Target.Create(name: directory);
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(""));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);
        Assert.AreEqual(expected, Field(report, "solution"));
    }

    [TestMethod]
    [DataRow("--")]
    [DataRow("9lives")]
    public void UnderivableIdentifier_IsRefusedRatherThanRepaired(string directory)
    {
        using Target target = Target.Create(name: directory);
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"), "nothing may be substituted from a name nobody chose");
        StringAssert.Contains(result.StdErr, "-Solution", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ExistingSolutionFile_IsRecoveredRatherThanRederived()
    {
        // The directory has been renamed since the first run; re-deriving would hand this
        // repository a second assembly prefix.
        using Target target = Target.Create(name: "renamed-since");
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(""));
        target.Put("WidgetShop.solution", "");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);
        Assert.AreEqual("WidgetShop", Field(report, "solution"));
    }

    [TestMethod]
    public void MoreThanOneSolutionFile_IsRefusedNamingEveryMatch()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(""));
        target.Put("One.solution", "");
        target.Put("Two.solution", "");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdErr, "One.solution", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, "Two.solution", StringComparison.Ordinal);
    }

    [TestMethod]
    public void SuppliedIdentifier_WinsOverDerivationAndRecovery()
    {
        using Target target = Target.Create(name: "widget-shop");
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(""));
        target.Put("Recovered.solution", "");

        Repo.RunResult result = pack.Execute(target.Path, solution: "Confirmed");

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);
        Assert.AreEqual("Confirmed", Field(report, "solution"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("9lives")]
    [DataRow("has space")]
    [DataRow("has-dash")]
    public void InvalidSuppliedIdentifier_IsRefused(string supplied)
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "# " + Token);
        pack.WithManifest(Manifest(Copy("readme.txt", "README.md", tokens: true)));

        Repo.RunResult result = pack.Execute(target.Path, solution: supplied);

        Assert.AreNotEqual(0, result.ExitCode, result.Combined);
        Assert.IsFalse(target.Has("README.md"));
    }

    // --- the report (R56, R61) ---------------------------------------------------------------------------------

    [TestMethod]
    public void Report_CarriesEveryOutcomeWithItsPathOrCommand()
    {
        using Target target = Target.Create(name: "widget-shop");
        using Pack pack = Pack.Create()
            .WithAsset("readme.txt", "# " + Token)
            .WithAsset("ignore.txt", "*.tmp\n");
        pack.WithManifest(Manifest(
            """{ "kind": "mkdir", "path": "src" },"""
            + Generate("src/made.txt", "pinned-0.0.1 keep") + ","
            + Generate("src/sample-page.txt", "demo content") + ","
            + Copy("readme.txt", "README.md", tokens: true) + ","
            + Copy("ignore.txt", "kept.txt") + ","
            + Rewrite("src/made.txt", "pinned-[0-9.]+ ", "", assertAfter: "keep") + ","
            + Delete("src/sample-*.txt")));
        target.Put("kept.txt", "mine");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);

        Assert.AreEqual("completed", Field(report, "outcome"));
        Assert.AreEqual("demo", Field(report, "kind"));
        Assert.AreEqual("WidgetShop", Field(report, "solution"));
        Assert.AreNotEqual("", Field(report, "artifact_root"));
        Assert.AreNotEqual("", Field(report, "target"));

        IReadOnlyList<Recorded> actions = Actions(report);
        string[] outcomes = ["created", "skipped", "mutated", "removed"];
        foreach (string outcome in outcomes)
        {
            Assert.IsTrue(
                actions.Any(a => a.Outcome == outcome),
                outcome + " is missing from: " + string.Join(", ", actions.Select(a => a.Kind + "/" + a.Outcome)));
        }

        Assert.IsTrue(
            actions.All(a => a.Path.Length > 0 || a.Command.Length > 0),
            "every recorded action carries the path or the command it applied to");
    }

    [TestMethod]
    public void RewriteAndDeleteThatMatchNothing_AreStillRecordedWithAnOutcome()
    {
        // R56 records *every* action, and the two that can quietly have nothing to do are
        // the two where that matters. A pack's `delete` of generator sample content whose
        // glob stops matching - because an SDK renamed the file it names - exits 0, and with
        // no row in the report `init` has nothing to relay and tells the owner the repository
        // is scaffolded while the sample content is still sitting in it.
        //
        // Counting declared actions against recorded ones, not just looking for outcomes:
        // Report_CarriesEveryOutcomeWithItsPathOrCommand asks whether each outcome appears
        // somewhere, which an absent row cannot contradict.
        using Target target = Target.Create();
        using Pack pack = Pack.Create();
        pack.WithManifest(Manifest(
            """{ "kind": "mkdir", "path": "src" },"""
            + Rewrite(NeverMatches, "pinned-[0-9.]+ ", "") + ","
            + Delete(NeverMatches)));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        using JsonDocument report = Report(result);
        IReadOnlyList<Recorded> actions = Actions(report);

        Assert.AreEqual(
            3,
            actions.Count,
            "three actions were declared; the report carries: "
                + string.Join(", ", actions.Select(a => a.Kind + "/" + a.Outcome)));

        foreach (string kind in new[] { "rewrite", "delete" })
        {
            Recorded recorded = actions.Single(a => a.Kind == kind);

            // `skipped`, not a sixth outcome: matching nothing is legitimate - it is what the
            // second run of a re-runnable executor does - and SecondExecution_ChangesNoFileAndExitsZero
            // holds the other half, that these rows do not make a re-run look like a change.
            Assert.AreEqual("skipped", recorded.Outcome, kind + " matched nothing, which is not a failure");
            Assert.AreEqual(NeverMatches, recorded.Path, kind + " should name the glob it applied to");
            Assert.IsTrue(
                recorded.Reason.Length > 0,
                kind + " recorded no reason, so the report says it was skipped without saying why");
        }
    }

    [TestMethod]
    public void UnexpectedFailureInsideAnAction_IsReportedAsFailedRatherThanThrown()
    {
        // The halt half of R56 - "completes **or halts**". An I/O failure the executor does
        // not anticipate escaped as an unhandled terminating error: exit 1, a PowerShell stack
        // trace on stderr, and nothing at all on stdout. `init` is required to show every
        // failed entry with its reason (R20, R59) and had no report to read.
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(Copy("readme.txt", "blocker/inside.txt")));

        // `blocker` is a file, so the copy's parent directory cannot be made and the write
        // throws from inside the action rather than through any path the executor checks.
        target.Put("blocker", "a file standing where the action needs a directory");

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreNotEqual(0, result.ExitCode, "an action that failed must not exit 0: " + result.Combined);

        using JsonDocument report = Report(result);
        Assert.AreEqual("halted", Field(report, "outcome"));

        Recorded failed = Actions(report).Single(a => a.Outcome == "failed");
        Assert.AreEqual("copy", failed.Kind, "the failure belongs to the action that was in flight");
        Assert.IsTrue(
            failed.Reason.Contains("blocker", StringComparison.OrdinalIgnoreCase),
            "the reason must carry the underlying error, not merely that there was one: " + failed.Reason);
    }

    [TestMethod]
    public void ReportVersionTheConsumerDoesNotKnow_IsRejected()
    {
        // The half of R61 that is the consumer's: a reader that shrugs at an unknown version
        // and reads the fields anyway is the reason schema versions stop being worth carrying.
        Assert.IsNull(Accept("""{ "report_version": 99, "outcome": "completed", "actions": [] }"""));
        Assert.IsNull(Accept("""{ "outcome": "completed" }"""));
        Assert.IsNull(Accept("not json at all"));

        using JsonDocument? known = Accept("""{ "report_version": 1, "outcome": "completed", "actions": [] }""");
        Assert.IsNotNull(known);
    }

    [TestMethod]
    public void StdOutCarriesTheReportAndNothingElse()
    {
        using Target target = Target.Create();
        using Pack pack = Pack.Create().WithAsset("readme.txt", "hello");
        pack.WithManifest(Manifest(
            Generate("noisy.txt", "x") + "," + Copy("readme.txt", "README.md")));

        Repo.RunResult result = pack.Execute(target.Path);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        Assert.IsTrue(
            result.StdOut.TrimStart().StartsWith('{'),
            "stdout must be parseable as the report alone: " + result.StdOut);
        Assert.IsTrue(result.StdErr.Length > 0, "progress belongs on stderr");
    }
}

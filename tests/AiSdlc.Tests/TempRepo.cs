namespace AiSdlc.Tests;

/// <summary>
/// A throwaway repository with an artifact chain in it, for driving the gate hook against.
/// </summary>
internal sealed class TempRepo : IDisposable
{
    private const string PlanTemplate = """
        ---
        slug: audit-log
        status: {0}
        slices:
          - id: storage
            files: ["src/Storage/Writer.cs", "tests/Storage/WriterTests.cs"]
            depends_on: []
        ---
        # body
        """;

    private const string CharterTemplate = """
        ---
        solution: Test
        artifact_root: {0}
        status: {1}
        ---
        """;

    private TempRepo(string path) => Path = path;

    public string Path { get; }

    /// <summary>
    /// Creates a repo whose chain lives at <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">Relative artifact root, e.g. ".sdlc" or "docs/sdlc".</param>
    /// <param name="planStatus">"approved", "draft", or null for no plan at all.</param>
    /// <param name="charterRoot">
    /// When set, a CHARTER.md declaring this artifact_root. Without it the hook can only
    /// find the chain if it sits at the default location.
    /// </param>
    /// <param name="charterStatus">
    /// The charter's own <c>status:</c>. Approved unless a test is about what happens
    /// before approval - the scaffold executor reads this and refuses a draft.
    /// </param>
    public static TempRepo Create(
        string rootDirectory = ".sdlc",
        string? planStatus = "approved",
        string? charterRoot = null,
        string charterStatus = "approved")
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "aisdlc-" + Guid.NewGuid().ToString("N"));

        string root = System.IO.Path.Combine(path, rootDirectory.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.Combine(root, "audit-log"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "src", "Storage"));

        if (planStatus is not null)
        {
            File.WriteAllText(
                System.IO.Path.Combine(root, "audit-log", "plan.md"),
                string.Format(System.Globalization.CultureInfo.InvariantCulture, PlanTemplate, planStatus));
        }

        if (charterRoot is not null)
        {
            File.WriteAllText(
                System.IO.Path.Combine(root, "CHARTER.md"),
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CharterTemplate,
                    charterRoot,
                    charterStatus));
        }

        return new TempRepo(path);
    }

    /// <summary>
    /// A repo whose chain holds <em>two</em> plans claiming one code file - the state the
    /// collision checks exist to report, and one <see cref="Create"/> cannot express.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate factory rather than an option on <see cref="Create"/>, deliberately. Three
    /// suites compile against that method and only two of them are this change's business;
    /// moving its signature or its default would break <c>ScaffoldManifestTests</c>, whose
    /// repair would then be an edit outside any slice manifest during a change whose whole
    /// subject is that nobody edits outside theirs.
    /// </para>
    /// <para>
    /// The contested file is a <c>.cs</c> and that is load-bearing: the gate hook skips
    /// markdown, text, json, yaml and toml, so a fixture colliding on an artifact would prove
    /// it stays silent rather than that it speaks. The path also carries no segment equal to
    /// the artifact root's leaf, because the hook skips those too and the fixture would pass
    /// while proving nothing.
    /// </para>
    /// </remarks>
    /// <param name="contested">Repo-relative path both plans claim.</param>
    /// <param name="secondPlanStatus">
    /// The second plan's <c>status:</c>. Approved makes it live and the file contested;
    /// anything else leaves one claimant, which is how a test asserts the negative.
    /// </param>
    /// <param name="secondPlanFinished">
    /// The second plan's <c>finished:</c> line, or null for none. <c>"true"</c> retires it, so
    /// the same fixture proves a finished plan claims nothing.
    /// </param>
    public static TempRepo CreateWithTwoClaimants(
        string contested = "src/Storage/Writer.cs",
        string secondPlanStatus = "approved",
        string? secondPlanFinished = null)
    {
        TempRepo repo = Create(charterRoot: ".sdlc");

        string root = System.IO.Path.Combine(repo.Path, ".sdlc");
        Directory.CreateDirectory(System.IO.Path.Combine(root, "retention-purge"));

        string lifecycle = secondPlanFinished is null
            ? $"status: {secondPlanStatus}"
            : $"status: {secondPlanStatus}\nfinished: {secondPlanFinished}";

        File.WriteAllText(
            System.IO.Path.Combine(root, "retention-purge", "plan.md"),
            $"""
            ---
            slug: retention-purge
            {lifecycle}
            slices:
              - id: purge
                files: ["{contested}", "src/Storage/Purger.cs"]
                depends_on: []
            ---
            # body
            """);

        // The first plan claims it too, so the collision is real rather than arranged by the
        // second plan alone.
        string first = System.IO.Path.Combine(root, "audit-log", "plan.md");
        File.WriteAllText(
            first,
            File.ReadAllText(first).Replace(
                "files: [\"src/Storage/Writer.cs\", \"tests/Storage/WriterTests.cs\"]",
                $"files: [\"{contested}\", \"tests/Storage/WriterTests.cs\"]"));

        Directory.CreateDirectory(System.IO.Path.Combine(
            repo.Path,
            System.IO.Path.GetDirectoryName(contested.Replace('/', System.IO.Path.DirectorySeparatorChar))!));
        File.WriteAllText(
            System.IO.Path.Combine(repo.Path, contested.Replace('/', System.IO.Path.DirectorySeparatorChar)),
            "// contested");

        return repo;
    }

    /// <summary>
    /// Copies the engine plugin's tree into this repo and returns the copy's root, so a check
    /// can remove a shipped file and watch what happens without touching the live plugin.
    /// </summary>
    /// <remarks>
    /// The packaging check has to prove the hook goes quiet when its parser is missing. Doing
    /// that by deleting the real parser would leave this repository one failed assertion away
    /// from a broken gate on every edit, so the deletion happens to a copy.
    /// </remarks>
    public string CopyEnginePlugin()
    {
        string source = System.IO.Path.Combine(Repo.Root, "plugins", "sdlc-core");
        string target = System.IO.Path.Combine(Path, "plugin-copy", "sdlc-core");

        foreach (string dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, target));
        }

        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }

        return target;
    }

    /// <summary>A repo with no artifact chain at all - the case that must stay silent.</summary>
    public static TempRepo CreateWithoutChain()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "aisdlc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "src"));
        return new TempRepo(path);
    }

    public string WritePlan(string contents)
    {
        string planPath = System.IO.Path.Combine(Path, "plan.md");
        File.WriteAllText(planPath, contents);
        return planPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temp directory that will not delete is not a test failure.
        }
    }
}

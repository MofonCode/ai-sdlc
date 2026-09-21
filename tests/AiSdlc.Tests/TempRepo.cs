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

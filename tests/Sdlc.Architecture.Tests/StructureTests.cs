using System.Text.RegularExpressions;

namespace Sdlc.Architecture.Tests;

/// <summary>
/// Rules about the shape of the repository, read from the files on disk.
/// </summary>
/// <remarks>
/// These are fitness functions: the mechanism that stops an invariant being prose. The framework's
/// own rule is that a constraint belongs in the lowest layer that can hold it, and a rule about
/// where files live and what references what is decidable without reading a sentence — so it belongs
/// here rather than in the constraint library.
/// <para>
/// Every message says what to do rather than what went wrong, because the reader is someone who did
/// not write the rule and is meeting it for the first time in a failing build.
/// </para>
/// </remarks>
[TestClass]
public sealed class StructureTests
{
    /// <summary>
    /// Guards the guard.
    /// </summary>
    /// <remarks>
    /// Without this, adding a project directory and forgetting to list it in
    /// <c>RepoFiles.ProjectDirectories</c> silently removes everything under it from every rule in
    /// this assembly, and the build stays green. That is the same failure the class remarks on
    /// <c>RepoFiles</c> describe, arriving by a different road.
    /// </remarks>
    [TestMethod]
    public void Every_directory_holding_projects_is_scanned()
    {
        string root = RepoFiles.FindRepoRoot();
        List<string> unscanned = [];

        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);

            if (name.StartsWith('.') || RepoFiles.ScannedDirectories.Contains(name))
            {
                continue;
            }

            if (Directory
                .EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                .Any(path => !RepoFiles.IsBuildOutput(path)))
            {
                unscanned.Add(name);
            }
        }

        Assert.AreEqual(
            0,
            unscanned.Count,
            "These directories hold projects and are not scanned, so none of the architecture rules "
            + "apply to anything inside them:\n  "
            + string.Join("\n  ", unscanned)
            + "\n\nAdd them to RepoFiles.ProjectDirectories. A rule that silently stops covering part "
            + "of the repository is worse than no rule, because the build still goes green.");
    }

    /// <summary>
    /// The kernel stays a leaf.
    /// </summary>
    /// <remarks>
    /// <c>Sdlc.Core</c> is what the command-line tool and both test assemblies depend on, so a
    /// reference out of it becomes a dependency of everything. If it needs a type from elsewhere,
    /// the type is in the wrong place: move the abstraction down and leave the implementation where
    /// it is.
    /// </remarks>
    [TestMethod]
    public void The_core_references_no_other_project()
    {
        string project = RepoFiles.FindProject("Sdlc.Core");
        IReadOnlyList<string> references = RepoFiles.ProjectReferences(project);

        Assert.AreEqual(
            0,
            references.Count,
            "Sdlc.Core must stay a leaf. It now references:\n  "
            + string.Join("\n  ", references)
            + "\n\nEverything in the repository depends on it, so a reference out of it is a "
            + "dependency imposed on the whole solution — including on the gate binary, whose "
            + "start-up cost sits on the critical path of every edit an agent makes.");
    }

    /// <summary>
    /// Every assembly granted access to another's internals exists, and is in this repository.
    /// </summary>
    /// <remarks>
    /// <para>An <c>InternalsVisibleTo</c> is a deliberate hole in a boundary, and the half people skip
    /// is checking it still describes reality. A grant to an assembly that was renamed or deleted reads
    /// as a boundary with an exception in it and is in fact a boundary with a lie in it — and nothing
    /// fails, because the grant simply never applies.</para>
    /// <para>Checked against the project list rather than a hand-written set, so the rule cannot
    /// disagree with the solution. A grant to something outside this repository would be a decision
    /// worth arguing about, and this is where the argument would surface.</para>
    /// </remarks>
    [TestMethod]
    public void Every_grant_of_internal_access_names_an_assembly_this_repository_builds()
    {
        string[] assemblies =
        [
            .. RepoFiles.AllProjectFiles().Select(path =>
            {
                string text = File.ReadAllText(path);
                Match named = Regex.Match(text, @"<AssemblyName>([^<]+)</AssemblyName>");
                return named.Success ? named.Groups[1].Value : RepoFiles.ProjectName(path);
            }),
        ];

        List<string> stale = [];
        int grants = 0;

        foreach (string project in RepoFiles.AllProjectFiles())
        {
            string text = File.ReadAllText(project);

            foreach (Match grant in Regex.Matches(text, @"<_Parameter1>([^<]+)</_Parameter1>"))
            {
                grants++;
                string target = grant.Groups[1].Value.Trim();

                if (!assemblies.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    stale.Add(RepoFiles.ProjectName(project) + " -> " + target);
                }
            }
        }

        Assert.IsTrue(
            grants > 0,
            "No grants were found at all, so this rule would pass however the boundaries looked. Either "
            + "every InternalsVisibleTo was removed - which is good news and means deleting this rule - "
            + "or the way they are declared changed and the pattern no longer matches.");

        Assert.AreEqual(
            0,
            stale.Count,
            "These grants name an assembly this repository does not build:\n  "
            + string.Join("\n  ", stale)
            + "\n\nEither the assembly was renamed, in which case fix the grant, or it is gone and the "
            + "grant should go with it. A grant that never applies reads as a boundary with a stated "
            + "exception and is a boundary with a lie in it.");
    }

    /// <summary>
    /// Every suppression is readable in one place.
    /// </summary>
    /// <remarks>
    /// <c>Directory.Build.props</c> states the policy this enforces: <c>WarningsNotAsErrors</c> is
    /// deliberately empty so that an exception is one visible line in a file a reviewer reads,
    /// rather than a pragma buried in source. A pragma is the form that hides — a reviewer reading
    /// the diff of one file cannot see that a rule was switched off, and nobody reading the props
    /// file learns it either.
    /// <para>
    /// <c>SuppressMessage</c> attributes are deliberately not caught here: an attribute carrying its
    /// justification at the site it applies to is the documented form rather than the hidden one. No
    /// count of them, because the count went stale -- the four that stood in the tree all suppressed
    /// CA1812, which is in <c>NoWarn</c> repository-wide, so each one suppressed nothing while
    /// asserting a policy that was not in force. They are comments now. F-18.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void No_source_file_switches_off_a_warning_with_a_pragma()
    {
        // Composed rather than written out, so this file does not itself contain the construct it
        // forbids. It did on the first run, and the rule reported its own source. The alternative
        // was to exempt this file, which is a hole: a suppression added here would then be the one
        // place the rule could not see.
        const string Forbidden = "#pragma" + " warning " + "disable";

        List<string> offenders = [];
        string root = RepoFiles.FindRepoRoot();

        foreach (string file in RepoFiles.SourceFiles())
        {
            string text = File.ReadAllText(file);

            if (text.Contains(Forbidden, StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "These files switch off a warning with a pragma:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nEvery suppression belongs in Directory.Build.props with the reason it exists, so "
            + "a reviewer can read the complete list of exceptions in one place. If the rule is "
            + "genuinely wrong for one member, a SuppressMessage attribute carrying its "
            + "justification is the documented form; a pragma is the form nobody can audit.");
    }
}

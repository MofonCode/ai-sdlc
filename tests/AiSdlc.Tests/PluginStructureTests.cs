using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// Structural integrity of the marketplace, and the charter invariants that claim to be
/// checkable by grep.
/// </summary>
/// <remarks>
/// An invariant nobody runs is an aspiration. These run on every build.
/// </remarks>
[TestClass]
public sealed class PluginStructureTests
{
    private const string ParseCheck =
        "$errs = $null; "
        + "$null = [System.Management.Automation.Language.Parser]::ParseFile("
        + "$env:SCRIPT_UNDER_TEST, [ref]$null, [ref]$errs); "
        + "if ($errs -and $errs.Count -gt 0) { $errs | ForEach-Object { $_.ToString() }; exit 1 }";

    /// <summary>The plugin that is the engine. Every other plugin in the marketplace is a stack pack.</summary>
    private const string EnginePlugin = "sdlc-core";

    /// <summary>The scaffold manifest every pack ships, beside its own SKILL.md.</summary>
    private const string ScaffoldManifest = "scaffold.json";

    /// <summary>
    /// The per-kind setup command that was retired when the kind became a question.
    /// </summary>
    private const string RetiredSetupCommand = "init-competition";

    /// <summary>
    /// The stack terms charter invariant I1 forbids the engine from naming, in one place.
    /// </summary>
    /// <remarks>
    /// Read by both I1 instruments - the one over instruction files and the one over shipped
    /// executables. A second copy is how an instrument quietly stops covering what it claims
    /// to: the copy that is edited is the only one anybody remembers.
    /// <para>
    /// The test for membership is R2's, not a brand test: does the string name a directory,
    /// project, assembly, command, template, package or file extension that <em>a pack owns</em>?
    /// A file extension is as much an imposition as a product name - an engine that knows the
    /// solution file is spelled `.slnx`, or that the substitution token is spelled
    /// `{{SOLUTION}}`, has taken a pack's decision into itself just as surely as one that says
    /// `blazor`. Those two were the spec's own examples of what the executable scan exists to
    /// catch, so they are listed here explicitly.
    /// </para>
    /// <para>
    /// Terms are matched as words, case-insensitively - see <see cref="StackTermsIn"/> - so
    /// `pip` does not fire on "pipe" and the spelling written here need not be the spelling on
    /// the line. Lowercase is a convention of this list, not a requirement of the match.
    /// </para>
    /// <para>
    /// `python` belongs by the rule and is deliberately left out, and the reason is not that it
    /// would misfire. The gate hook names Python as a word, on purpose, explaining why the hook
    /// is not written in it. That is the engine describing its own implementation runtime, not
    /// imposing a stack on the repository being built, and only the second is what I1 forbids.
    /// An engine that cannot say why it chose PowerShell for itself is not better protected,
    /// only harder to document. Do not add `python` and then delete that explanation to get the
    /// build green - that trade is the wrong way round.
    /// </para>
    /// </remarks>
    private static readonly string[] StackTerms =
    [
        // Languages, frameworks, runtimes and platforms.
        "dotnet", ".net", "c#", "csharp", "blazor", "aspire", "asp.net", "ef core",
        "entity framework", "azure", "aws", "terraform", "java", "typescript", "golang",
        "kotlin", "ruby", "php", "rust",
        // Build, package and test tooling a pack chooses.
        "msbuild", "nuget", "npm", "pip", "gradle", "mstest", "xunit", "nunit", "coverlet",
        // File extensions and manifest filenames a pack owns, and the token it substitutes.
        "{{solution}}", "slnx", ".sln", "csproj", "fsproj", "runsettings", "ipynb",
        "package.json", "pom.xml", "go.mod", "cargo.toml",
        // Project and assembly names from a pack's frozen layout.
        "apphost", "servicedefaults",
        // Data services and infrastructure a pack stands up.
        "sql server", "postgres", "mysql", "mongodb", "sqlite", "redis", "docker",
        "kubernetes", "pyyaml",
    ];

    /// <summary>
    /// Every term from <see cref="StackTerms"/> that <paramref name="line"/> names as a word.
    /// </summary>
    /// <remarks>
    /// Both I1 instruments read a line through here, so the matching rule is defined once.
    /// <para>
    /// A plain substring match cannot tell a term from a longer word that contains it: it reads
    /// `rust` out of "trusting" and `pip` out of "pipe", and the only remedy anybody reaches for
    /// is deleting the term - which fails open, silently, with the list still claiming coverage
    /// it no longer has. So the boundary is checked instead of the term being dropped.
    /// </para>
    /// <para>
    /// The boundary is built from what each term itself starts and ends with, because a blanket
    /// `\b` on both sides is a trap here: a word boundary is defined against word characters, so
    /// `\bc#\b` can never match anything - there is no boundary after `#` - and a third of this
    /// list starts or ends in punctuation (`.net`, `c#`, `package.json`, `{{solution}}`). A
    /// guard is therefore applied only on a side where the term's own character is a word
    /// character. `.net` keeps matching inside `asp.net`, which is right: both are violations.
    /// </para>
    /// <para>
    /// An uppercase letter counts as a boundary too, so that `dotnet` still fires on
    /// `$dotnetExe` and `Invoke-DotnetBuild`, and an underscore is a separator rather than a
    /// word character, so that `$postgres_host` fires. Without those two, moving from
    /// substrings to words would trade a false-positive problem for a false-negative one, and a
    /// stack term buried in an identifier is exactly how one would really arrive in engine
    /// code. What is still given up is a term glued inside an unbroken lowercase run -
    /// `dotnetexe`, `runslnx` - which neither camel case, snake case nor kebab case produces,
    /// and which is the price of `pip` not firing on "pipe". That is the trade, made
    /// deliberately and in this direction.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> StackTermsIn(string line)
    {
        foreach (string term in StackTerms)
        {
            for (int at = line.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                 at >= 0;
                 at = line.IndexOf(term, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (StartsAWord(line, at, term) && EndsAWord(line, at + term.Length, term))
                {
                    yield return term;
                    break;
                }
            }
        }
    }

    /// <summary>Whether the match at <paramref name="start"/> begins a word.</summary>
    private static bool StartsAWord(string line, int start, string term)
    {
        if (!IsWordCharacter(term[0]) || start == 0)
        {
            return true;
        }

        char before = line[start - 1];
        return !IsWordCharacter(before) || (char.IsUpper(line[start]) && !char.IsUpper(before));
    }

    /// <summary>Whether the match ending at <paramref name="end"/> ends a word.</summary>
    private static bool EndsAWord(string line, int end, string term)
    {
        if (!IsWordCharacter(term[^1]) || end == line.Length)
        {
            return true;
        }

        char after = line[end];
        return !IsWordCharacter(after) || char.IsUpper(after);
    }

    /// <summary>The characters a word is made of, for the boundary rule above.</summary>
    /// <remarks>
    /// Underscore is a separator here, unlike in `\w`, so that `$postgres_host` and
    /// `$slnx_glob` fire. Snake case is a live spelling in this repository - the manifest's own
    /// fields are `solution_token` and `solution_file_glob` - so a term glued to an underscore
    /// is a term named, not a longer word.
    /// </remarks>
    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c);

    /// <summary>
    /// Column headings that make a markdown table a list of files.
    /// </summary>
    private static readonly string[] FileColumnHeadings =
        ["file", "files", "path", "paths", "asset", "assets"];

    private static IEnumerable<string> EngineInstructionFiles()
    {
        string engine = Path.Combine(Repo.Root, "plugins", EnginePlugin);
        foreach (string dir in new[] { "skills", "agents" })
        {
            string full = Path.Combine(engine, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Every executable the engine plugin ships: the hook, the scripts, the workflows.
    /// </summary>
    /// <remarks>
    /// The engine plugin only. A stack pack's own scripts are entitled to name their stack -
    /// that is what a pack is - so scanning `plugins/` wholesale would fail on the pack for
    /// doing its job. Both extensions are here rather than only `*.ps1`, because an engine
    /// executable written in Node can hardcode a language name exactly as easily as one
    /// written in PowerShell, and scanning only one of them rebuilds the gap this check closes
    /// one runtime over.
    /// </remarks>
    private static IEnumerable<string> EngineExecutableFiles()
    {
        string engine = Path.Combine(Repo.Root, "plugins", EnginePlugin);
        foreach (string pattern in new[] { "*.ps1", "*.js" })
        {
            foreach (string file in Directory.EnumerateFiles(engine, pattern, SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    /// <summary>Every stack pack in the marketplace, with the directory its own skill lives in.</summary>
    /// <remarks>
    /// The convention is `&lt;plugin&gt;/skills/&lt;pack-name&gt;/`, beside which the pack's
    /// manifest and assets sit. The engine is excluded: it scaffolds nothing and ships no
    /// manifest, so asserting one of it would be asserting the opposite of I1.
    /// </remarks>
    private static IEnumerable<(string Name, string SkillDirectory)> StackPacks()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            if (string.Equals(plugin.Name, EnginePlugin, StringComparison.Ordinal))
            {
                continue;
            }

            yield return (plugin.Name, Path.Combine(plugin.Directory, "skills", plugin.Name));
        }
    }

    [TestMethod]
    public void CharterInvariantI1_EngineImposesNoStack()
    {
        List<string> violations = [];
        foreach (string file in EngineInstructionFiles())
        {
            string[] lines = File.ReadAllLines(file);
            bool inFence = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                // Illustrative paths inside fenced examples impose nothing. The invariant is
                // about normative prose telling a consuming repo what to build with.
                if (inFence)
                {
                    continue;
                }

                foreach (string term in StackTermsIn(lines[i]))
                {
                    violations.Add(
                        Path.GetRelativePath(Repo.Root, file) + ":" + (i + 1) + " -> " + term);
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "the engine must impose no stack on the repo being built:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// I1 again, over the files that execute rather than the files that instruct.
    /// </summary>
    /// <remarks>
    /// The executable half of I1 had no instrument. The check above reads `*.md` under
    /// `skills/` and `agents/`; the scaffold executor is PowerShell under `scripts/`, outside
    /// that scan entirely, so it could hardcode a language, a file extension or a token and
    /// the suite would stay green while the spec claimed otherwise. Same term list, different
    /// file set. No fence handling: a script has no illustrative prose, so every line counts.
    /// </remarks>
    [TestMethod]
    public void EngineExecutablesImposeNoStack()
    {
        List<string> executables = EngineExecutableFiles().ToList();
        Assert.IsTrue(executables.Count > 0, "expected at least one shipped engine executable");

        List<string> violations = [];
        foreach (string file in executables)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string term in StackTermsIn(lines[i]))
                {
                    violations.Add(Marketplace.Rel(file) + ":" + (i + 1) + " -> " + term);
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "a shipped engine executable must impose no stack on the repo being built; every "
                + "such string belongs in a manifest the pack ships:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Every pack ships a scaffold manifest that parses and declares at least one kind.
    /// </summary>
    /// <remarks>
    /// This is what makes the manifest a property of packs generally rather than of the one
    /// pack that happens to have written one. A pack with no manifest scaffolds by prose, and
    /// prose cannot be executed by a test.
    /// </remarks>
    [TestMethod]
    public void EveryPack_ShipsAScaffoldManifest()
    {
        List<(string Name, string SkillDirectory)> packs = StackPacks().ToList();
        Assert.IsTrue(packs.Count > 0, "expected at least one stack pack in the marketplace");

        foreach ((string name, string skillDirectory) in packs)
        {
            string manifest = Path.Combine(skillDirectory, ScaffoldManifest);
            Assert.IsTrue(
                File.Exists(manifest),
                name + " ships no scaffold manifest - expected " + Marketplace.Rel(manifest));

            using JsonDocument document = Marketplace.Parse(manifest);
            Assert.IsTrue(
                document.RootElement.TryGetProperty("kinds", out JsonElement kinds)
                    && kinds.ValueKind == JsonValueKind.Object
                    && kinds.EnumerateObject().Any(),
                Marketplace.Rel(manifest) + " declares no kinds, so there is nothing to execute");
        }
    }

    /// <summary>
    /// A pack's skill cites its manifest, and does not carry a second list beside it.
    /// </summary>
    /// <remarks>
    /// I5 says a pack's skill is *the only* list of the actions it performs. A file table left
    /// standing beside the manifest makes that two lists with nothing checking they agree, and
    /// the first thing that happens is one of them quietly stops being true.
    /// </remarks>
    [TestMethod]
    public void PackSkillCitesItsManifest()
    {
        foreach ((string name, string skillDirectory) in StackPacks())
        {
            string skill = Path.Combine(skillDirectory, "SKILL.md");
            Assert.IsTrue(File.Exists(skill), name + " has no skill at " + Marketplace.Rel(skill));

            string[] lines = File.ReadAllLines(skill);

            Assert.IsTrue(
                lines.Any(l => l.Contains(ScaffoldManifest, StringComparison.Ordinal)),
                Marketplace.Rel(skill)
                    + " never names " + ScaffoldManifest
                    + ", so the skill is not pointing at the list of what the pack writes");

            List<string> tables = FileTables(lines);
            Assert.AreEqual(
                0,
                tables.Count,
                Marketplace.Rel(skill)
                    + " carries a file table beside " + ScaffoldManifest
                    + "; the manifest is the list, and a second one drifts:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, tables));
        }
    }

    /// <summary>
    /// This repository's own README lists the layout its packs emit, not only their config.
    /// </summary>
    /// <remarks>
    /// A documentation check in a file about marketplace structure is a slight stretch, and it
    /// is here on purpose: this class already holds the checks that keep the repository honest
    /// about itself - that a pack ships a manifest, that a skill cites it rather than keeping a
    /// second list - and a README that has stopped describing what the setup command produces
    /// is the same failure one file out. A new file for one check would belong to no slice.
    /// <para>
    /// The expectation is read out of each pack's manifest rather than written down here, so
    /// what is checked is the frozen contract - the top-level split, and the
    /// `&lt;dir&gt;/&lt;Solution&gt;.&lt;Role&gt;` project-name pattern - and not the prose
    /// around it. A pack that adds a role, or a marketplace that gains a pack, fails this
    /// check until the README says so. That coupling is the point: it is the only thing that
    /// makes the README track what is actually emitted.
    /// </para>
    /// <para>
    /// Deliberately structural, because prose gets rewritten by people who have never read the
    /// requirement. Asserting a sentence would break on the first honest edit and then be
    /// deleted rather than understood; asserting only that the README mentions the command
    /// would pass on a README that says nothing about what it produces. What this cannot
    /// catch: a README that lists the layout and then describes it wrongly, a path that
    /// satisfies the check from some unrelated section of the file, or a project whose path the
    /// manifest builds at run time rather than declaring. Whether the listed layout is what the
    /// executor really writes is a different question, asked of the emitted solution elsewhere.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void RepositoryReadme_ListsTheLayoutItsPacksEmit()
    {
        string readme = Path.Combine(Repo.Root, "README.md");
        Assert.IsTrue(File.Exists(readme), "no README at " + Marketplace.Rel(readme));
        string[] lines = File.ReadAllLines(readme);

        List<string> missing = [];
        int expected = 0;

        foreach ((string name, string skillDirectory) in StackPacks())
        {
            (List<string> directories, List<string> projects) =
                EmittedLayout(Path.Combine(skillDirectory, ScaffoldManifest));

            foreach (string directory in directories)
            {
                expected++;
                if (!lines.Any(line => line.Contains(directory + "/", StringComparison.Ordinal)))
                {
                    missing.Add("  " + name + ": no line shows the " + directory + "/ directory");
                }
            }

            foreach (string project in projects)
            {
                expected++;
                string[] parts = project.Split('/');
                if (!lines.Any(line => ShowsProjectPath(line, parts[0], parts[1])))
                {
                    missing.Add(
                        "  " + name + ": no line shows a project under "
                            + parts[0] + "/ ending in ." + parts[1]);
                }
            }
        }

        // A README listing nothing would otherwise pass an empty expectation quietly.
        Assert.IsTrue(
            expected > 0,
            "read no layout out of any pack manifest, so this check asserted nothing");

        Assert.AreEqual(
            0,
            missing.Count,
            Marketplace.Rel(readme)
                + " does not list what the packs emit; the layout is frozen, and a README that"
                + " lists only config files is how a reader learns the wrong shape:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// The README does not still offer the per-kind setup command that was retired.
    /// </summary>
    /// <remarks>
    /// The competition solution became a *kind* the one setup command asks about, so the
    /// separate command named here exists nowhere any more - naming it sends a reader to a
    /// command that does not answer. The whole file is searched rather than one section,
    /// because a renamed heading would otherwise turn this check off without saying so.
    /// </remarks>
    [TestMethod]
    public void RepositoryReadme_DoesNotOfferTheRetiredSetupCommand()
    {
        string readme = Path.Combine(Repo.Root, "README.md");
        string[] lines = File.ReadAllLines(readme);
        Assert.IsTrue(lines.Length > 0, Marketplace.Rel(readme) + " is empty");

        List<string> offered = [];
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(RetiredSetupCommand, StringComparison.OrdinalIgnoreCase))
            {
                offered.Add("  line " + (i + 1) + ": " + lines[i].Trim());
            }
        }

        Assert.AreEqual(
            0,
            offered.Count,
            Marketplace.Rel(readme)
                + " names " + RetiredSetupCommand
                + ", which is not a command any more - the competition solution is a kind the"
                + " one setup command asks about:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offered));
    }

    /// <summary>
    /// A pack's assets and its manifest name exactly the same files, in both directions.
    /// </summary>
    /// <remarks>
    /// The manifest is what makes the executor the only writer, and that claim is what the
    /// design leans on when it puts the whole scaffold outside the gate hook: every file
    /// reaches the owner in one report *because* one program writes them all. An asset the
    /// manifest does not name breaks it - the file is either written by some other path, or it
    /// is dead weight nobody will notice rotting - and the message says both, because from
    /// here they look identical.
    /// <para>
    /// The other direction is the cheaper failure and the more embarrassing one: a manifest
    /// naming an asset that is not there fails on a user's machine, mid-scaffold, rather than
    /// here.
    /// </para>
    /// <para>
    /// The assets directory is not written down here - it is read off the declared paths, so a
    /// pack that moves its assets moves this check with it. An asset declared at the skill
    /// root, with no directory of its own, is checked only for existence: sweeping the skill
    /// directory itself would drag in SKILL.md and the manifest.
    /// </para>
    /// <para>
    /// Both directions are claims about absence, and absence is satisfied by emptiness: a
    /// manifest declaring no asset has none missing from disk, and roots the disk sweep in
    /// nothing, so nothing on disk is undeclared either. A set-difference check of this shape
    /// passes most easily exactly when it covers least, which is why it counts what it
    /// compared and fails if that is zero.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void EveryPackAsset_IsDeclaredByItsManifest()
    {
        List<(string Name, string SkillDirectory)> packs = StackPacks().ToList();
        Assert.IsTrue(packs.Count > 0, "expected at least one stack pack in the marketplace");

        List<string> problems = [];
        int compared = 0;

        foreach ((string name, string skillDirectory) in packs)
        {
            string manifest = Path.Combine(skillDirectory, ScaffoldManifest);
            using JsonDocument document = Marketplace.Parse(manifest);
            SortedSet<string> declared = DeclaredAssets(document.RootElement);
            compared += declared.Count;

            foreach (string asset in declared)
            {
                if (!File.Exists(Path.Combine(skillDirectory, asset)))
                {
                    problems.Add(
                        "  " + name + ": " + ScaffoldManifest + " declares " + asset
                            + ", which is not on disk - that fails mid-scaffold on a user's"
                            + " machine rather than here");
                }
            }

            foreach (string asset in AssetsOnDisk(skillDirectory, declared))
            {
                compared++;
                if (!declared.Contains(asset))
                {
                    problems.Add(
                        "  " + name + ": " + asset + " is declared by nothing, so either"
                            + " something other than the executor writes it, or it is dead"
                            + " weight - both are worth knowing");
                }
            }
        }

        // Emptiness satisfies both directions at once - nothing on disk is undeclared when
        // nothing is declared, and the disk sweep is rooted in the declared paths, so it goes
        // looking nowhere. Without this the check would pass most easily when it covered least.
        Assert.IsTrue(
            compared > 0,
            "compared no asset against any manifest, in either direction, so this check"
                + " asserted nothing");

        Assert.AreEqual(
            0,
            problems.Count,
            "a pack's manifest is the list of what it writes, and the executor being the only"
                + " writer is what puts the scaffold safely outside the gate hook:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// No engine instruction file names a file a pack scaffolds.
    /// </summary>
    /// <remarks>
    /// This is the half of the claim the other check cannot reach: not that the manifest lists
    /// everything, but that nothing else writes it. The violation has a precedent rather than
    /// being hypothetical - the setup skill's previous wording had the engine writing seven of
    /// the pack's configuration files itself, which is how the half-gating asymmetry survived
    /// the design that claimed to resolve it.
    /// <para>
    /// Instruction files only, deliberately. The gate hook's skip list names `.gitignore` and
    /// `.editorconfig` for reasons that have nothing to do with any pack - every repository
    /// has both - so a file-name scan over shipped executables would fail on an engine doing
    /// its job. What an executable may name is a different question, and
    /// <see cref="EngineExecutablesImposeNoStack"/> is where it is asked.
    /// </para>
    /// <para>
    /// The names come from the manifest's own `copy` and `append` destinations, so this tracks
    /// the pack rather than a list in the suite. It overlaps
    /// <see cref="CharterInvariantI1_EngineImposesNoStack"/> for two of them by accident -
    /// `coverage.runsettings` and `versions.tf` contain stack terms - and covers the rest,
    /// which no term list would ever hold. A destination carrying the solution token is left
    /// out: the engine cannot name one without naming the token, which I1 already forbids.
    /// </para>
    /// <para>
    /// What this cannot catch: a file written by a generator from inside a `run` action, which
    /// is beyond anything read off the source; prose that describes a pack file without
    /// spelling its name; and a fenced example, skipped here for the same reason I1 skips one.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void EngineInstructions_NameNoPackFile()
    {
        SortedDictionary<string, string> packFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string skillDirectory) in StackPacks())
        {
            string manifest = Path.Combine(skillDirectory, ScaffoldManifest);
            using JsonDocument document = Marketplace.Parse(manifest);
            JsonElement root = document.RootElement;

            foreach (string file in ScaffoldedFileNames(root, SolutionToken(root, manifest)))
            {
                packFiles.TryAdd(file, name);
            }
        }

        Assert.IsTrue(
            packFiles.Count > 0,
            "read no scaffolded file name out of any manifest, so this check asserted nothing");

        List<string> violations = [];
        foreach (string file in EngineInstructionFiles())
        {
            string[] lines = File.ReadAllLines(file);
            bool inFence = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                if (inFence)
                {
                    continue;
                }

                foreach ((string packFile, string pack) in packFiles)
                {
                    if (lines[i].Contains(packFile, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add(
                            "  " + Marketplace.Rel(file) + ":" + (i + 1) + " -> " + packFile
                                + " (declared by " + pack + ")");
                    }
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "an engine instruction file names a file a pack scaffolds; the executor is the only"
                + " thing that may write one, and that is what lets the whole scaffold sit"
                + " outside the gate hook:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// The top-level directories and `&lt;dir&gt;/&lt;Role&gt;` projects one manifest emits.
    /// </summary>
    /// <remarks>
    /// Read from the paths the manifest's actions declare - `path`, `glob`, `cwd`,
    /// `unless_exists` and every `args` element - so a project counts only if some action
    /// really names it. The solution token comes from the manifest too: which string a pack
    /// substitutes is the pack's decision, and hardcoding it here would rebuild in the suite
    /// the coupling the manifest exists to remove.
    /// </remarks>
    private static (List<string> Directories, List<string> Projects) EmittedLayout(string manifest)
    {
        using JsonDocument document = Marketplace.Parse(manifest);
        JsonElement root = document.RootElement;
        string token = SolutionToken(root, manifest);

        SortedSet<string> directories = [];
        SortedSet<string> projects = [];

        foreach (JsonElement action in ManifestActions(root))
        {
            if (Text(action, "kind") == "mkdir"
                && Text(action, "path") is string directory
                && !directory.Contains('/', StringComparison.Ordinal))
            {
                directories.Add(directory);
            }

            foreach (string value in DeclaredPaths(action))
            {
                foreach (string project in ProjectsIn(value, token))
                {
                    projects.Add(project);
                }
            }
        }

        return ([.. directories], [.. projects]);
    }

    /// <summary>Every asset <paramref name="root"/> declares, relative to the skill.</summary>
    private static SortedSet<string> DeclaredAssets(JsonElement root)
    {
        SortedSet<string> assets = [];
        foreach (JsonElement action in ManifestActions(root))
        {
            if (Text(action, "asset") is string asset)
            {
                assets.Add(asset.Replace('\\', '/'));
            }
        }

        return assets;
    }

    /// <summary>
    /// The asset files present under the directories <paramref name="declared"/> lives in.
    /// </summary>
    /// <remarks>
    /// Rooted in the declared paths rather than in a directory name written down here, so the
    /// scan follows a pack that reorganises. Only declared paths that have a directory of
    /// their own contribute a root - see the check's own remarks.
    /// </remarks>
    private static IEnumerable<string> AssetsOnDisk(
        string skillDirectory, SortedSet<string> declared)
    {
        SortedSet<string> roots = [];
        foreach (string asset in declared)
        {
            int at = asset.IndexOf('/', StringComparison.Ordinal);
            if (at > 0)
            {
                roots.Add(asset[..at]);
            }
        }

        foreach (string root in roots)
        {
            string full = Path.Combine(skillDirectory, root);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (string file in
                Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                yield return Path.GetRelativePath(skillDirectory, file).Replace('\\', '/');
            }
        }
    }

    /// <summary>
    /// The name of every file a `copy` or `append` in <paramref name="root"/> writes.
    /// </summary>
    /// <remarks>
    /// The last segment only: what an engine instruction file would have to spell to be
    /// writing one of these itself. A name carrying the solution token is left out, because
    /// naming it would mean naming the token, which I1 already forbids.
    /// </remarks>
    private static SortedSet<string> ScaffoldedFileNames(JsonElement root, string token)
    {
        SortedSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement action in ManifestActions(root))
        {
            if (Text(action, "kind") is not ("copy" or "append")
                || Text(action, "path") is not string path)
            {
                continue;
            }

            string name = path[(path.LastIndexOf('/') + 1)..];
            if (name.Length > 0 && !name.Contains(token, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>The string the pack wants substituted, which is the pack's own decision.</summary>
    private static string SolutionToken(JsonElement root, string manifest)
    {
        string token = Text(root, "solution_token") ?? string.Empty;
        Assert.AreNotEqual(
            string.Empty,
            token,
            Marketplace.Rel(manifest) + " declares no solution_token, so nothing here can tell"
                + " a pack's own spelling from a string this suite made up");
        return token;
    }

    /// <summary>Every action every kind in <paramref name="root"/> declares.</summary>
    /// <remarks>
    /// Kind by kind, because a file is scaffolded if any kind scaffolds it: a check that read
    /// one kind would go quiet on everything the other one writes.
    /// </remarks>
    private static IEnumerable<JsonElement> ManifestActions(JsonElement root)
    {
        if (!root.TryGetProperty("kinds", out JsonElement kinds)
            || kinds.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (JsonProperty kind in kinds.EnumerateObject())
        {
            if (!kind.Value.TryGetProperty("actions", out JsonElement actions)
                || actions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement action in actions.EnumerateArray())
            {
                yield return action;
            }
        }
    }

    /// <summary>A non-empty string field of <paramref name="element"/>, or null.</summary>
    private static string? Text(JsonElement element, string field) =>
        element.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;

    /// <summary>Every string in <paramref name="action"/> that can carry a path.</summary>
    private static IEnumerable<string> DeclaredPaths(JsonElement action)
    {
        foreach (string field in new[] { "path", "glob", "cwd", "unless_exists" })
        {
            if (Text(action, field) is string text)
            {
                yield return text;
            }
        }

        if (action.TryGetProperty("args", out JsonElement args)
            && args.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement arg in args.EnumerateArray())
            {
                if (arg.ValueKind == JsonValueKind.String
                    && arg.GetString() is { Length: > 0 } text)
                {
                    yield return text;
                }
            }
        }
    }

    /// <summary>Each `&lt;directory&gt;/&lt;Role&gt;` named by <paramref name="value"/>.</summary>
    /// <remarks>
    /// A role is the PascalCase suffix of the frozen `&lt;Solution&gt;.&lt;Role&gt;` pattern,
    /// so `notebooks/&lt;Solution&gt;.ipynb` yields no role - a lowercase suffix is a file
    /// extension, and `notebooks` is already held as a directory. A directory that contains
    /// the token is a project directory rather than a top-level one, and yields nothing:
    /// `src/&lt;Solution&gt;.Api/&lt;Solution&gt;.Api.http` describes one project, not two.
    /// </remarks>
    private static IEnumerable<string> ProjectsIn(string value, string token)
    {
        string needle = "/" + token + ".";
        for (int at = value.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = value.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            // A match at the very start has no directory in front of it to name.
            if (at == 0)
            {
                continue;
            }

            string directory = value[(value.LastIndexOf('/', at - 1) + 1)..at];

            int start = at + needle.Length;
            int end = start;
            while (end < value.Length && char.IsLetterOrDigit(value[end]))
            {
                end++;
            }

            if (directory.Length > 0
                && !directory.Contains(token, StringComparison.Ordinal)
                && end > start
                && char.IsUpper(value[start]))
            {
                yield return directory + "/" + value[start..end];
            }
        }
    }

    /// <summary>Whether <paramref name="line"/> shows a project path with this role.</summary>
    /// <remarks>
    /// The directory has to come first, so the line is a path rather than two mentions that
    /// happen to share it: "Aspire (AppHost + ServiceDefaults)" names the roles but shows no
    /// layout, and does not satisfy this.
    /// </remarks>
    private static bool ShowsProjectPath(string line, string directory, string role)
    {
        int at = line.IndexOf(directory + "/", StringComparison.Ordinal);
        return at >= 0
            && line.IndexOf("." + role, at, StringComparison.Ordinal) > at;
    }

    /// <summary>
    /// Markdown tables in <paramref name="lines"/> that list files, reported as "line: header".
    /// </summary>
    /// <remarks>
    /// Deliberately narrow, because a check that cries wolf is worse than no check: it takes a
    /// header cell that names files or paths, a separator row under it, and two or more rows
    /// under that. A prose mention of a path does not fire, a two-column commitments table does
    /// not fire, and a table inside a fenced example does not fire - the pack's skill shows the
    /// charter entries it wants written, and those are illustrations rather than claims about
    /// what the pack scaffolds. What does fire is the one shape this rule is about: a column of
    /// filenames with something said about each.
    /// </remarks>
    private static List<string> FileTables(string[] lines)
    {
        List<string> tables = [];
        bool inFence = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimStart();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence
                || !line.StartsWith("|", StringComparison.Ordinal)
                || i + 1 >= lines.Length
                || !IsSeparatorRow(lines[i + 1])
                || !Cells(line).Any(
                    cell => Array.Exists(
                        FileColumnHeadings, h => string.Equals(h, cell, StringComparison.Ordinal))))
            {
                continue;
            }

            int rows = 0;
            for (int j = i + 2;
                 j < lines.Length && lines[j].TrimStart().StartsWith("|", StringComparison.Ordinal);
                 j++)
            {
                rows++;
            }

            if (rows >= 2)
            {
                tables.Add("  line " + (i + 1) + ": " + line + " (" + rows + " rows)");
            }
        }

        return tables;
    }

    /// <summary>The `|---|---|` row that turns the line above it into a table header.</summary>
    private static bool IsSeparatorRow(string line)
    {
        string trimmed = line.Trim();
        return trimmed.StartsWith("|", StringComparison.Ordinal)
            && trimmed.Length > 1
            && trimmed.All(c => c is '|' or '-' or ':' or ' ');
    }

    /// <summary>The cells of a table row, lowercased with markdown emphasis and code ticks off.</summary>
    private static IEnumerable<string> Cells(string row) =>
        row.Trim('|')
            .Split('|')
            .Select(cell => cell.Trim().Trim('*', '`', ' ').ToLowerInvariant());

    [TestMethod]
    public void EveryShippedPowerShellScript_Parses()
    {
        string[] scripts = Directory.GetFiles(
            Path.Combine(Repo.Root, "plugins"), "*.ps1", SearchOption.AllDirectories);

        Assert.IsTrue(scripts.Length > 0, "expected at least one shipped script");

        foreach (string script in scripts)
        {
            // Path goes through the environment rather than into the command string: quoting
            // a Windows path into a PowerShell -Command is a trap, and this repo has already
            // lost two files to escaping layers.
            Repo.RunResult result = Repo.Run(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ParseCheck],
                environment: new Dictionary<string, string?> { ["SCRIPT_UNDER_TEST"] = script });

            Assert.AreEqual(0, result.ExitCode, script + Environment.NewLine + result.Combined);
        }
    }

    [TestMethod]
    public void NoPythonShipsInThisRepository()
    {
        // The charter commits to this, and a session shim once sat here untracked long
        // enough for a fresh reviewer to catch it in the act.
        string git = Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar;
        string[] python = Directory
            .GetFiles(Repo.Root, "*.py", SearchOption.AllDirectories)
            .Where(p => !p.Contains(git, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            python.Length,
            "python found: "
                + string.Join(", ", python.Select(p => Path.GetRelativePath(Repo.Root, p))));
    }
}

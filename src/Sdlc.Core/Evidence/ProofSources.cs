using System.Globalization;
using System.Text;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Evidence;

/// <summary>
/// The files a plan's proofs rest on that the change did not touch, as the build's commit holds them (D-168).
/// </summary>
/// <remarks>
/// <para><strong>A verify reads the diff, and a proof can live outside it.</strong> A criterion that guards
/// behaviour the change must not break is proven by a test the change never touched, and a test the change
/// adds can assert through a helper it never touched. Neither assertion is in <c>diff.patch</c>, so a Met could
/// quote nothing and was judged not shown: one cycle spent seven hours on it, another 4.7M tokens over three
/// verify rounds.</para>
/// <para><strong>Two ways in, both read from the plan.</strong> Every test a proof names that the patch does not
/// mention is looked up at <c>head.sha</c>, and its block in the file defining it is handed over; a name in more than
/// <see cref="MostFilesPerName"/> files is too common to say which defines it, and is listed instead. Every path a
/// proof names in backticks that existed before the change is handed over too, which is how a plan points at a
/// helper.</para>
/// <para><strong>A working file, like the patch</strong> (D-124): rebuilt from the two commits and the plan,
/// never committed, and covered by the evidence hash as rebuilt.</para>
/// </remarks>
public static class ProofSources
{
    /// <summary>The file's name in the evidence directory.</summary>
    public const string FileName = "proof-sources.txt";

    /// <summary>The most files a test's name may be found in for one of them to be handed over.</summary>
    public const int MostFilesPerName = 3;

    /// <summary>
    /// The files, or null when the evidence names no head commit or every proof is in the change.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text a verify is handed, or null.</returns>
    public static async Task<string?> Rebuild(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(repository);

        string planPath = cycle.PathOf(ArtifactKind.Plan);

        if (!File.Exists(planPath)
            || await EvidenceDiff.Commit(cycle, RecordEvidence.BaseFileName, cancellationToken).ConfigureAwait(false) is not { } from
            || await EvidenceDiff.Commit(cycle, RecordEvidence.HeadFileName, cancellationToken).ConfigureAwait(false) is not { } until
            || await EvidenceDiff.Rebuild(cycle, repository, root, cancellationToken).ConfigureAwait(false) is not { } patch)
        {
            return null;
        }

        string plan = await File.ReadAllTextAsync(planPath, cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<string> numbered = ArtifactSections.NumberedItems(plan, "Proof").Values.ToList();
        IReadOnlyList<TableRow> rows = ArtifactSections.TableRows(plan, "Proof");

        SortedSet<string> files = new(StringComparer.Ordinal);
        SortedDictionary<string, List<string>> tests = new(StringComparer.Ordinal);
        List<string> notes = [];

        foreach (string name in numbered.SelectMany(DeclaredFiles.TestsIn)
            .Concat(rows.SelectMany(row => DeclaredFiles.TestsIn(row.Test)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            string bare = Bare(name);

            if (bare.Length == 0 || patch.Contains(bare, StringComparison.Ordinal))
            {
                continue;
            }

            IReadOnlyList<string> found = repository.FilesContaining(root, until, bare, [Cycle.SdlcDirectoryName]);

            if (found.Count == 0)
            {
                notes.Add(string.Create(CultureInfo.InvariantCulture, $"`{name}` is in no file at this commit."));
            }
            else if (found.Count > MostFilesPerName)
            {
                notes.Add(string.Create(CultureInfo.InvariantCulture, $"`{name}` is in {found.Count} files, too many to say which defines it, so none is handed over."));
            }
            else
            {
                foreach (string path in found)
                {
                    if (!tests.TryGetValue(path, out List<string>? names))
                    {
                        tests[path] = names = [];
                    }

                    names.Add(bare);
                }
            }
        }

        // A path a proof names, when the change did not create it: a new file is whole in the patch already.
        foreach (string path in numbered.Concat(rows.Select(row => string.Join(' ', [row.Test, .. row.Others]))).SelectMany(DeclaredFiles.PathsIn))
        {
            if (repository.ReadAt(root, from, path) is not null)
            {
                files.Add(path);
            }
        }

        if (files.Count == 0 && tests.Count == 0 && notes.Count == 0)
        {
            return null;
        }

        StringBuilder text = new();
        text.Append(CultureInfo.InvariantCulture, $"What the plan's proofs rest on outside the change, as commit {until} holds it: each named test's block, and each file a proof names, whole.\n");

        foreach (string note in notes)
        {
            text.Append("\n- ").Append(note);
        }

        text.Append('\n');

        foreach ((string path, List<string> names) in tests.Where(test => !files.Contains(test.Key)))
        {
            if (repository.ReadAt(root, until, path) is not { } content)
            {
                continue;
            }

            string[] lines = content.ReplaceLineEndings("\n").Split('\n');

            foreach ((int first, int last, bool cut) in Blocks(lines, names))
            {
                string more = cut ? string.Create(CultureInfo.InvariantCulture, $", cut at {MostLinesPerBlock} lines: the rest is not shown") : string.Empty;
                text.Append(CultureInfo.InvariantCulture, $"\n===== {path}, lines {first + 1}-{last + 1}{more} =====\n");
                text.Append(string.Join('\n', lines[first..(last + 1)])).Append('\n');
            }
        }

        foreach (string path in files)
        {
            if (repository.ReadAt(root, until, path) is { } content)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n===== {path} =====\n").Append(content.ReplaceLineEndings("\n").TrimEnd('\n')).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// The block each name opens: from the line it is on to where the indentation returns to that line's, closing
    /// line included. Ranges that overlap are merged.
    /// </summary>
    /// <remarks>
    /// A test's body, not its file. One real cycle's proofs reached nine files, 186 KB handed to every
    /// reader, for about a hundred lines of assertions. Indentation rather than a parser, because the framework
    /// names no language outside a profile: a method in C#, a function in Python and a test in TypeScript all
    /// indent their bodies. A helper the block calls is not followed; a plan names its file to have it whole.
    /// </remarks>
    /// <param name="lines">The file's lines.</param>
    /// <param name="names">The names to find, as whole words.</param>
    /// <returns>The ranges, zero-based and inclusive, in order.</returns>
    public static IReadOnlyList<(int First, int Last, bool Cut)> Blocks(IReadOnlyList<string> lines, IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(names);

        List<(int First, int Last, bool Cut)> ranges = [];

        for (int index = 0; index < lines.Count; index++)
        {
            if (!names.Any(name => ContainsWord(lines[index], name)))
            {
                continue;
            }

            int opening = Indent(lines[index]);
            int end = index;
            bool deeper = false;
            bool closed = false;

            int next = index + 1;

            for (; next < lines.Count && next - index <= MostLinesPerBlock; next++)
            {
                if (lines[next].Trim().Length == 0)
                {
                    continue;
                }

                int indent = Indent(lines[next]);

                // A brace on a line of its own at the opening's indentation opens the body, as C# writes it.
                if (indent > opening || (!deeper && lines[next].TrimStart().StartsWith('{')))
                {
                    deeper |= indent > opening;
                    end = next;
                    continue;
                }

                // Back at the opening's indentation: the closing line belongs to the block, the next statement does not.
                if (deeper && lines[next].TrimStart() is ['}' or ')' or ']', ..])
                {
                    end = next;
                }

                closed = true;
                break;
            }

            // Cut when the limit ended it rather than the indentation: a reader told nothing takes the block as whole.
            bool cut = !closed && next < lines.Count;

            if (ranges.Count > 0 && index <= ranges[^1].Last + 1)
            {
                ranges[^1] = (ranges[^1].First, Math.Max(ranges[^1].Last, end), ranges[^1].Cut || cut);
            }
            else
            {
                ranges.Add((index, end, cut));
            }

            index = Math.Max(index, end);
        }

        return ranges;
    }

    /// <summary>The most lines one block may run to, so a name at the top of a long class does not hand over the class.</summary>
    public const int MostLinesPerBlock = 300;

    private static int Indent(string line) =>
        line.TakeWhile(char.IsWhiteSpace).Sum(character => character == '\t' ? 4 : 1);

    private static bool ContainsWord(string line, string word)
    {
        for (int at = line.IndexOf(word, StringComparison.Ordinal); at >= 0; at = line.IndexOf(word, at + 1, StringComparison.Ordinal))
        {
            bool before = at == 0 || !IsWordCharacter(line[at - 1]);
            bool after = at + word.Length == line.Length || !IsWordCharacter(line[at + word.Length]);

            if (before && after)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';

    /// <summary>
    /// Writes the file from the two commits and the plan, or removes one left by an earlier run when there is
    /// nothing to hand over.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="repository">Git.</param>
    /// <param name="root">The repository's root.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is as the evidence says.</returns>
    public static async Task Restore(Cycle cycle, IRepository repository, string root, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        string path = Path.Combine(cycle.EvidenceDirectory, FileName);

        if (await Rebuild(cycle, repository, root, cancellationToken).ConfigureAwait(false) is { } text)
        {
            await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    // What a runner reports is often qualified -- Namespace.Class.Method, or a method with its arguments -- and
    // the file defining it holds the method's own name.
    private static string Bare(string name)
    {
        string unargued = name.Split('(')[0].Trim();
        int dot = unargued.LastIndexOf('.');

        return dot < 0 ? unargued : unargued[(dot + 1)..];
    }
}

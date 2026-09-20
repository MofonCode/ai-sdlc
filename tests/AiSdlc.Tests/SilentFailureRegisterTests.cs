using System.Text.RegularExpressions;

namespace AiSdlc.Tests;

/// <summary>
/// The charter's silent-failure register and this test suite, held against each other.
/// </summary>
/// <remarks>
/// I9 says every known silent-failure class has a named check in CI, and that the set grows
/// whenever a new class is found. It does not say who notices, and nothing compared the two
/// lists in either direction - so six checks reached this suite without reaching the register.
/// Four arrived with a task-tier change, which gets no verification stage at all. Two were
/// raised by the sixth verification of init-scaffolds-solution, which had no seventh round to
/// dispose of its own finding, and the chain shipped with it open.
///
/// What this cannot catch, and the plan says so rather than implying otherwise: a new method
/// added to a class the register already names, covering a new class of failure. Two of those
/// six were exactly that. Telling a new instrument from a new failure class is a judgement, and
/// this file only compares names.
/// </remarks>
[TestClass]
public sealed class SilentFailureRegisterTests
{
    private static readonly string CharterPath = Path.Combine(Repo.Root, ".sdlc", "CHARTER.md");
    private static readonly string SuiteDirectory = Path.Combine(Repo.Root, "tests", "AiSdlc.Tests");

    /// <summary>This check's own name, which K4 requires the register to carry.</summary>
    private const string OwnName = nameof(SilentFailureRegisterTests);

    /// <summary>
    /// Every check the register names, read from the table's Check column only.
    /// </summary>
    /// <remarks>
    /// The column matters. The charter mentions `depends_on` in backticks while discussing the
    /// plan schema, and a scan of every backticked identifier in the file would invent a check
    /// by that name and then report it missing - a failure caused entirely by the scan.
    /// </remarks>
    private static List<string> RegisteredChecks()
    {
        List<string> names = [];

        foreach (string line in File.ReadAllLines(CharterPath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            // | Silent failure | Check |  -> columns[0] is empty, [1] is the failure, [2] the check.
            string[] columns = trimmed.Split('|');
            if (columns.Length < 3)
            {
                continue;
            }

            foreach (Match match in Regex.Matches(columns[2], "`([^`]+)`"))
            {
                string name = match.Groups[1].Value.Trim();

                // Rows cite a class or a method, sometimes several, sometimes with prose after a
                // dash. Anything that is not an identifier is description, not an instrument.
                if (Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    names.Add(name);
                }
            }
        }

        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Every test class in the suite, and the test methods each declares.</summary>
    private static Dictionary<string, List<string>> SuiteClasses()
    {
        Dictionary<string, List<string>> classes = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(SuiteDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string text = File.ReadAllText(file);

            // Only classes carrying [TestClass] are instruments; helpers and fixtures are not.
            // Anchored to the attribute and the declaration: a bare `class` search matches the
            // word inside a doc comment, which named three classes "is", "runs" and "has" on the
            // first run of this check.
            Match declaration = Regex.Match(
                text,
                @"\[TestClass\]\s*(?:public|internal)\s+(?:sealed\s+|partial\s+|static\s+)*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
            if (!declaration.Success)
            {
                continue;
            }

            List<string> methods = [];
            foreach (Match method in Regex.Matches(text, @"public\s+(?:async\s+)?(?:void|Task)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\("))
            {
                methods.Add(method.Groups["name"].Value);
            }

            classes[declaration.Groups["name"].Value] = methods;
        }

        return classes;
    }

    [TestMethod]
    public void EveryTestClass_IsNamedByTheRegister()
    {
        List<string> registered = RegisteredChecks();
        Dictionary<string, List<string>> suite = SuiteClasses();

        Assert.IsTrue(
            registered.Count > 0 && suite.Count > 0,
            $"read {registered.Count} name(s) from the register and found {suite.Count} test"
                + " class(es), so this check asserted nothing");

        HashSet<string> named = new(registered, StringComparer.Ordinal);
        List<string> problems = [];

        foreach (KeyValuePair<string, List<string>> entry in suite)
        {
            if (named.Contains(entry.Key) || entry.Value.Any(named.Contains))
            {
                continue;
            }

            problems.Add(
                $"{entry.Key} is a test class and the charter's silent-failure register names"
                + " neither it nor any of its checks, so the register claims less coverage than"
                + " the suite has");
        }

        Assert.AreEqual(
            0,
            problems.Count,
            "I9 says the register grows whenever a new class of silent failure is found:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void EveryRegisteredCheck_ExistsInTheSuite()
    {
        List<string> registered = RegisteredChecks();
        Dictionary<string, List<string>> suite = SuiteClasses();

        Assert.IsTrue(
            registered.Count > 0 && suite.Count > 0,
            $"read {registered.Count} name(s) from the register and found {suite.Count} test"
                + " class(es), so this check asserted nothing");

        HashSet<string> exists = new(suite.Keys, StringComparer.Ordinal);
        foreach (List<string> methods in suite.Values)
        {
            foreach (string method in methods)
            {
                exists.Add(method);
            }
        }

        List<string> problems = [];
        foreach (string name in registered.Where(n => !exists.Contains(n)))
        {
            problems.Add(
                $"the register names `{name}` and no class or test method by that name exists,"
                + " so the register promises an instrument that is not there");
        }

        Assert.AreEqual(
            0,
            problems.Count,
            "a register row naming a check that does not exist is worse than no row, because it"
                + " reads as coverage:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void ThisCheck_IsItselfRegistered()
    {
        // An instrument that enforces registration and is not registered is the defect it
        // exists to prevent.
        Assert.IsTrue(
            RegisteredChecks().Contains(OwnName, StringComparer.Ordinal),
            $"{OwnName} enforces that the register names every check, and the register does not"
                + " name it");
    }
}

namespace AiSdlc.Tests;

/// <summary>
/// Workflow scripts, validated the way the Workflow runtime evaluates them.
/// </summary>
/// <remarks>
/// This is the one check that genuinely needs a JavaScript runtime, and the only reason any
/// JavaScript remains in this repository's tooling. A workflow body is wrapped in an async
/// function before it runs, so top-level `await` and top-level `return` are legal - which
/// means `node --check` as an ES module is the wrong validator, because it rejects the very
/// construct the format requires. The node script this calls wraps before parsing.
/// </remarks>
[TestClass]
public sealed class WorkflowTests
{
    private static string Validator => Path.Combine(Repo.Root, "scripts", "check-workflow.mjs");

    [TestMethod]
    public void EveryWorkflow_IsValid()
    {
        string[] workflows = Marketplace.WorkflowFiles().ToArray();
        Assert.IsTrue(workflows.Length > 0, "expected at least one workflow script");

        foreach (string workflow in workflows)
        {
            Repo.RunResult result = Repo.Run("node", [Validator, workflow]);

            Assert.AreEqual(
                0,
                result.ExitCode,
                Marketplace.Rel(workflow) + Environment.NewLine + result.Combined);
        }
    }

    [TestMethod]
    public void TheValidator_RejectsABodyThatDoesNotParse()
    {
        // A validator that only ever passes is worth nothing, so it is checked against a
        // known-bad input on every run rather than trusted.
        string broken = Path.Combine(Path.GetTempPath(), "wf-" + Guid.NewGuid().ToString("N") + ".js")
            ;
        File.WriteAllText(broken, """
            export const meta = {
              name: 'broken',
              description: 'deliberately invalid',
              phases: [{ title: 'Only' }],
            }
            phase('Only')
            const x = {{{
            """);

        try
        {
            Repo.RunResult result = Repo.Run("node", [Validator, broken]);
            Assert.AreEqual(1, result.ExitCode, "the validator accepted a body that does not parse");
            StringAssert.Contains(result.Combined, "not valid inside the async wrapper");
        }
        finally
        {
            File.Delete(broken);
        }
    }

    [TestMethod]
    public void TheValidator_RejectsAPhaseTitleThatIsNotDeclared()
    {
        string mismatched = Path.Combine(Path.GetTempPath(), "wf-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(mismatched, """
            export const meta = {
              name: 'mismatched',
              description: 'phase titles disagree with meta',
              phases: [{ title: 'Build' }],
            }
            phase('Biuld')
            return { done: true }
            """);

        try
        {
            // An undeclared title renders a blank phase. Nothing errors; the progress view is
            // simply wrong, which is exactly the silent class this repository guards against.
            Repo.RunResult result = Repo.Run("node", [Validator, mismatched]);
            Assert.AreEqual(1, result.ExitCode, "the validator accepted an undeclared phase title");
            StringAssert.Contains(result.Combined, "is not declared in meta.phases");
        }
        finally
        {
            File.Delete(mismatched);
        }
    }
}

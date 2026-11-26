using FluentResults;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The <c>build_tools</c> key in <c>solution.md</c>: the commands a build's own process may run beyond its own (D-202).
/// </summary>
/// <remarks>
/// Each name becomes <c>Bash(&lt;name&gt; *)</c> in the process's allowed tools, so what matters most is that a name
/// which would allow something other than the one command the person named is refused rather than passed on.
/// </remarks>
[TestClass]
public sealed class BuildToolsTests
{
    [TestMethod]
    public void An_absent_key_reads_as_none_which_is_every_build_before_it_existed()
    {
        Result<SolutionDocument> read = SolutionDocument.Read("---\norigin: adopted\n---\n", "solution.md");

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(0, read.Value.ExtraBuildTools.Count);
        Assert.IsFalse(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub)
                .Any(key => key.StartsWith("build_tools", StringComparison.Ordinal)),
            "An absent key is left out of the block, as telemetry is.");
    }

    [TestMethod]
    public void Named_tools_are_read_in_order_once_each_and_written_back_the_same()
    {
        Result<SolutionDocument> read = SolutionDocument.Read("---\norigin: adopted\nbuild_tools: pwsh, docker,pwsh\n---\n", "solution.md");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "pwsh", "docker" }, read.Value.ExtraBuildTools.ToList());
        CollectionAssert.Contains(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub, buildTools: read.Value.BuildTools),
            "build_tools: pwsh, docker");
    }

    [TestMethod]
    public void A_command_line_is_one_entry_its_spacing_made_single_and_written_back_the_same()
    {
        // D-224: one command line rather than the tool whole -- a container build, not the runtime.
        Result<SolutionDocument> read = SolutionDocument.Read(
            "---\norigin: adopted\nbuild_tools: docker  build, pwsh -File scripts/evidence.ps1, git\n---\n",
            "solution.md");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "docker build", "pwsh -File scripts/evidence.ps1", "git" }, read.Value.ExtraBuildTools.ToList());
        CollectionAssert.Contains(
            SolutionDocument.Keys(SolutionOrigin.Adopted, SolutionStandard.Existing, Sdlc.Core.Init.Platform.GitHub, buildTools: read.Value.BuildTools),
            "build_tools: docker build, pwsh -File scripts/evidence.ps1, git");
    }

    [TestMethod]
    [DataRow("docker *")]
    [DataRow("py*")]
    [DataRow("C:/tools/run.exe")]
    [DataRow("-rf")]
    [DataRow("./evidence.sh")]
    [DataRow("docker build *")]
    [DataRow("docker build; rm x")]
    [DataRow("pwsh -c $env:X")]
    [DataRow("pwsh -File '../run.ps1'")]
    [DataRow("pwsh -File ../run.ps1")]
    [DataRow("docker build > out")]
    [DataRow("make && make test")]
    public void A_name_that_would_allow_more_than_one_command_fails(string name)
    {
        Result<SolutionDocument> read = SolutionDocument.Read("---\norigin: adopted\nbuild_tools: " + name + "\n---\n", "solution.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors.Single().Message, "build_tools names");
    }
}

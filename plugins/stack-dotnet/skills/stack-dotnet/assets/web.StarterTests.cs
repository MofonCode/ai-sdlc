namespace {{SOLUTION}}.UnitTests;

/// <summary>
/// The test the scaffold leaves behind, replacing the generator's sample one.
/// </summary>
/// <remarks>
/// It exists because a suite that collects zero tests exits clean: delete the generated
/// sample and nothing is left to fail, and `dotnet test` reports success by finding nothing.
/// So this asserts something the solution can actually get wrong.
///
/// What it holds is the naming contract between the emitted projects and
/// `coverage.runsettings`, which excludes `[*.UnitTests]*` by assembly name. Rename this
/// project to `Tests`, or leave the pack's solution token unsubstituted in
/// `Directory.Build.props`, and the exclusion silently stops matching - the coverage number
/// then counts test code as covered product code and nothing raises a word about it.
///
/// Delete it once there is a real suite here. Until then it is the only thing standing
/// between this project and a green run that proves nothing.
/// </remarks>
[TestClass]
public sealed class StarterTests
{
    [TestMethod]
    public void UnitTestAssemblyName_MatchesTheCoverageExclusionPattern()
    {
        string? assemblyName = typeof(StarterTests).Assembly.GetName().Name;

        Assert.AreEqual(
            "{{SOLUTION}}.UnitTests",
            assemblyName,
            "coverage.runsettings excludes test code by the assembly-name pattern "
                + "[*.UnitTests]*. This assembly no longer matches it, so its own code is "
                + "being counted as covered product code.");
    }

    [TestMethod]
    public void UnitTestRootNamespace_CarriesTheSolutionPrefix()
    {
        string? rootNamespace = typeof(StarterTests).Namespace;

        Assert.AreEqual(
            "{{SOLUTION}}.UnitTests",
            rootNamespace,
            "every project in this solution is named and namespaced "
                + "{{SOLUTION}}.<Role>. A namespace that has drifted from the assembly name "
                + "means the convention Directory.Build.props encodes is no longer holding.");
    }
}

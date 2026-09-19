namespace {{SOLUTION}}.IntegrationTests;

/// <summary>
/// The test the scaffold leaves behind in the integration suite, replacing the generator's
/// commented-out sample.
/// </summary>
/// <remarks>
/// An integration suite that collects zero tests is worse than no integration suite: it runs,
/// it exits clean, and it reports green for a composed system nobody exercised. That is the
/// state the generated sample leaves behind, because every test in it is commented out.
///
/// What this asserts is the naming contract between the emitted projects and
/// `coverage.runsettings`, which excludes `[*.IntegrationTests]*` by assembly name - the same
/// contract the unit suite's starter holds, and the one that breaks silently.
///
/// It deliberately does not start the composed application. Doing that needs a container
/// runtime, and a starter test that cannot run on a machine without one would make the suite
/// red by default rather than honest. The first real test here is the one that starts it; the
/// block below is what the generator's sample said about writing it, kept because deleting
/// the sample would otherwise take the instructions with it.
/// </remarks>
/// <example>
/// Reference the AppHost project from this one first:
/// <code>
/// dotnet add tests/{{SOLUTION}}.IntegrationTests/{{SOLUTION}}.IntegrationTests.csproj \
///     reference src/{{SOLUTION}}.AppHost/{{SOLUTION}}.AppHost.csproj
/// </code>
/// Then a test that exercises the composed system looks like this, where "web" is the
/// resource name the AppHost gave the front end:
/// <code>
/// [TestMethod]
/// public async Task GetWebResourceRoot_ReturnsOk()
/// {
///     CancellationToken token = TestContext.CancellationTokenSource.Token;
///     IDistributedApplicationTestingBuilder appHost =
///         await DistributedApplicationTestingBuilder.CreateAsync&lt;Projects.{{SOLUTION}}_AppHost&gt;();
///
///     await using DistributedApplication app = await appHost.BuildAsync(token);
///     await app.StartAsync(token);
///     await app.ResourceNotifications.WaitForResourceHealthyAsync("web", token);
///
///     using HttpClient client = app.CreateHttpClient("web");
///     using HttpResponseMessage response = await client.GetAsync("/", token);
///
///     Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
/// }
/// </code>
/// </example>
[TestClass]
public sealed class IntegrationStarterTests
{
    [TestMethod]
    public void IntegrationTestAssemblyName_MatchesTheCoverageExclusionPattern()
    {
        string? assemblyName = typeof(IntegrationStarterTests).Assembly.GetName().Name;

        Assert.AreEqual(
            "{{SOLUTION}}.IntegrationTests",
            assemblyName,
            "coverage.runsettings excludes test code by the assembly-name pattern "
                + "[*.IntegrationTests]*. This assembly no longer matches it, so its own "
                + "code is being counted as covered product code.");
    }
}

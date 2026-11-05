using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Init;
using Sdlc.Core.Operate;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Operate: production's errors filed as leads (D-89).
/// </summary>
[TestClass]
public sealed class OperateTests
{
    private static readonly TelemetryError _frequent = new(
        "System.NullReferenceException at Orders.Export",
        "System.NullReferenceException",
        "Orders.Export",
        "Cart for jane.doe@example.com from 10.1.2.3 was empty; token=abc123",
        42,
        DateTimeOffset.Parse("2026-10-15T01:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-10-15T23:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static readonly TelemetryError _rare = _frequent with { Signature = "TimeoutException at Pay", Type = "System.TimeoutException", Method = "Pay", Count = 3 };

    [TestMethod]
    [DataRow("mail jane.doe@example.com now", "mail [email] now")]
    [DataRow("from 192.168.0.12", "from [ip]")]
    [DataRow("Authorization: Bearer eyJhbGciOi.payload.sig", "Authorization: Bearer [token]")]
    [DataRow("/pay?amount=3&api_key=s3cr3t&x=1", "/pay?amount=3&api_key=[secret]&x=1")]
    [DataRow("card 4111111111111111", "card [number]")]
    [DataRow("user 0f8fad5b-d9cb-469f-a165-70867728950e", "user [id]")]
    public void Personal_data_is_stripped_before_anything_leaves_the_store(string text, string scrubbed)
    {
        Assert.AreEqual(scrubbed, Scrub.Text(text));
    }

    [TestMethod]
    public async Task New_errors_are_filed_most_frequent_first_and_scrubbed()
    {
        FakeFiler filer = new();

        RunOperate.Response run = await Run(Platform.AzureDevOps, [_rare, _frequent], filer);

        Assert.AreEqual(2, filer.Filed_.Count);
        Assert.AreEqual(_frequent.Signature, run.Leads[0].Error.Signature, "Ranked by how often it happened.");
        Assert.IsFalse(filer.Filed_.Any(b => b.Body.Contains("jane.doe", StringComparison.Ordinal) || b.Body.Contains("10.1.2.3", StringComparison.Ordinal)));
        StringAssert.Contains(filer.Filed_[0].Title, "NullReferenceException in Orders.Export");
    }

    [TestMethod]
    public async Task An_open_item_or_a_dismissed_one_stops_the_signature_being_filed_again()
    {
        FakeFiler filer = new(
            new FiledSignature(RunOperate.TagOf(_frequent.Signature), Open: true, Dismissed: false),
            new FiledSignature(RunOperate.TagOf(_rare.Signature), Open: false, Dismissed: true));

        RunOperate.Response run = await Run(Platform.GitHub, [_frequent, _rare], filer);

        Assert.AreEqual(0, filer.Filed_.Count);
        StringAssert.Contains(run.Leads[0].Outcome, "already filed");
        StringAssert.Contains(run.Leads[1].Outcome, "dismissed");
    }

    [TestMethod]
    public async Task A_signature_closed_as_done_that_happens_again_is_filed_again()
    {
        FakeFiler filer = new(new FiledSignature(RunOperate.TagOf(_frequent.Signature), Open: false, Dismissed: false));

        await Run(Platform.AzureDevOps, [_frequent], filer);

        Assert.AreEqual(1, filer.Filed_.Count, "A regression is worth a new lead.");
    }

    [TestMethod]
    public async Task A_dry_run_files_nothing()
    {
        FakeFiler filer = new();

        RunOperate.Response run = await Run(Platform.AzureDevOps, [_frequent], filer, dryRun: true);

        Assert.AreEqual(0, filer.Filed_.Count);
        StringAssert.Contains(run.Leads[0].Outcome, "would be filed");
    }

    [TestMethod]
    public async Task A_local_solution_keeps_its_leads_as_a_report()
    {
        FakeFiler filer = new();

        RunOperate.Response run = await Run(Platform.Local, [_frequent], filer);

        Assert.AreEqual(0, filer.Filed_.Count);
        Assert.IsNotNull(run.Report);
        StringAssert.Contains(run.Report, "leads", "Kept under .sdlc/reports/, which ignores itself.");
    }

    [TestMethod]
    public async Task With_no_telemetry_declared_it_says_what_to_declare()
    {
        RunOperate.Response run = await Run(Platform.AzureDevOps, [_frequent], new FakeFiler(), telemetry: null);

        Assert.IsNull(run.Store);
        StringAssert.Contains(RunOperate.Render(run), "telemetry: appinsights:");
    }

    [TestMethod]
    public void The_cli_table_is_read_into_errors()
    {
        const string Json = """
            { "tables": [ { "name": "PrimaryResult",
              "columns": [ {"name":"problemId"}, {"name":"type"}, {"name":"method"}, {"name":"count"}, {"name":"first"}, {"name":"last"}, {"name":"message"} ],
              "rows": [ ["p1", "System.Exception", "A.B", 7, "2026-10-15T01:00:00Z", "2026-10-15T02:00:00Z", "boom"] ] } ] }
            """;

        Result<IReadOnlyList<TelemetryError>> read = AppInsightsTelemetry.Read(Json);

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual(7, read.Value[0].Count);
        Assert.AreEqual("A.B", read.Value[0].Method);
    }

    private static async Task<RunOperate.Response> Run(
        Platform platform,
        IReadOnlyList<TelemetryError> errors,
        FakeFiler filer,
        bool dryRun = false,
        string? telemetry = "appinsights:00000000-aaaa")
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: " + SolutionDocument.NameOf(platform) + (telemetry is null ? string.Empty : "\ntelemetry: " + telemetry) + "\n---\n");

        using var gate = Gate.For(workspace.Root, services =>
        {
            services.AddSingleton<ITelemetry>(new FakeTelemetry(errors));
            services.AddSingleton<IWorkFiler>(filer);
        });

        Result<RunOperate.Response> result = await gate
            .Send<RunOperate.Command, RunOperate.Response>(new RunOperate.Command(TimeSpan.FromHours(24), dryRun));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private sealed class FakeTelemetry(IReadOnlyList<TelemetryError> errors) : ITelemetry
    {
        public Task<Result<IReadOnlyList<TelemetryError>>> Errors(string store, TimeSpan window, string workingDirectory, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Ok(errors));
    }

    private sealed class FakeFiler(params FiledSignature[] filed) : IWorkFiler
    {
        public List<BugToFile> Filed_ { get; } = [];

        public Task<Result<IReadOnlyList<FiledSignature>>> Filed(Platform platform, string workingDirectory, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Ok<IReadOnlyList<FiledSignature>>(filed));

        public Task<Result<string>> File(Platform platform, BugToFile bug, string workingDirectory, CancellationToken cancellationToken)
        {
            Filed_.Add(bug);
            return Task.FromResult(Result.Ok("azuredevops:" + Filed_.Count));
        }
    }
}

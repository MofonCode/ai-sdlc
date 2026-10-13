namespace Sdlc.Core.Solutions;

/// <summary>One declared default: a concern, and what this profile chooses for it.</summary>
/// <param name="Concern">What is being decided, as the stack file's left-hand column.</param>
/// <param name="Choice">What the profile declares, or empty for a row a person fills in.</param>
public sealed record StackDefault(string Concern, string Choice);

/// <summary>
/// A named set of declared defaults the framework ships.
/// </summary>
/// <remarks>
/// <para><strong>A profile is a starting point, not a layer, and no solution records one.</strong> The
/// default profile is what the first draft of a declared stack is written from, and every profile is
/// what the options menu offers rows out of. After that the table is a person's: nothing in a governed
/// repository names a profile, and what binds is what the declared stack itself says (D-76). Stacking
/// two was considered and rejected: merging tables on a concern key needs precedence rules, and worse,
/// it makes <c>techstack.md</c> a <em>derived</em> file -- so the thing every plan is judged against
/// would be generated rather than written, and "a person places it" would stop meaning anything
/// (D-68).</para>
///
/// <para><strong>Overlap between profiles is handled here, where it is cheap.</strong> Two profiles that
/// share their platform choices share one set of rows rather than repeating twenty that then
/// drift. Composition happens while authoring the framework; what reaches a solution is always one flat
/// table, and nothing in a governed repository knows a composition took place.</para>
/// </remarks>
/// <param name="Name">The profile's name, as the options menu credits it.</param>
/// <param name="Summary">One line about what the profile is for, for a listing.</param>
/// <param name="Defaults">The declared defaults, in the order they are written.</param>
/// <param name="Notes">
/// What a stack draft says under its tables: the reasoning a plan needs that a row cannot hold. Empty
/// for a profile no draft is written from.
/// </param>
public sealed record StackProfile(
    string Name,
    string Summary,
    IReadOnlyList<StackDefault> Defaults,
    IReadOnlyList<string> Notes);

/// <summary>The profiles this framework ships.</summary>
/// <remarks>
/// <para><strong>Embedded rather than restored from a package</strong>, for D-33's reason: adoption has to
/// work with no feed configured and no network, so a default that needed a restore would make the first
/// command one that can fail. Whether profiles ever become packages is a separate question.</para>
/// </remarks>
public static class StackProfiles
{
    /// <summary>The name of the profile a first stack draft is written from.</summary>
    public const string DefaultName = "dotnet-default";

    /// <summary>
    /// The choices that do not follow from the language.
    /// </summary>
    /// <remarks>
    /// Shared by every profile, because where data lives and how infrastructure is described are decisions
    /// about the platform rather than about the code. This is the only composition there is, and it exists
    /// so two profiles cannot come to disagree about Terraform by being edited separately.
    /// </remarks>
    private static readonly StackDefault[] _platform =
    [
        new("infrastructure as code", "Terraform"),
        new("cloud", "Azure"),
        new("relational data", "MS SQL"),
        new("document data", "Cosmos DB"),
        new("cache", "Redis"),
    ];

    /// <summary>
    /// What the dotnet profile's stack file says under its table.
    /// </summary>
    /// <remarks>
    /// The reasoning a plan needs that a row cannot hold: why the shared UI library's name is
    /// load-bearing, why an AppHost is conditional, why liveness is the one row that binds. The rows
    /// themselves live with the profile, which is what keeps two profiles from disagreeing about a choice
    /// they share.
    /// </remarks>
    private static readonly string[] _dotnetNotes =
    [
        "## Notes worth keeping",
        string.Empty,
        "**Shared UI is Blazor components in a Razor class library**, hosted by Blazor for web and MAUI",
        "Blazor Hybrid for desktop and Android. MAUI does not target web.",
        string.Empty,
        "**That library is `src/<solution>.Ui`, and the name is load-bearing.** The architecture suite",
        "reads a project's role off its name, so the rules admitting MudBlazor and letting the web",
        "project reference it are written against `Ui` and match nothing else. A library named for what",
        "it holds rather than for its role builds nothing and fails asking for a table row.",
        string.Empty,
        "**The components live there from the first one, not from the second host.** A solution with one",
        "host pays for a project with one consumer, which is the cost; the alternative is moving every",
        "component the day a second host arrives, discovered by the cycle trying to add it.",
        string.Empty,
        "**MudBlazor is one dependency, and that shared library is where it goes** -- so web and hybrid",
        "are the same UI rather than two that resemble each other. It needs an interactive render mode:",
        "a statically rendered page would get its markup and none of its behaviour. And a bUnit test of",
        "a component that renders one needs `AddMudServices` and loose JSInterop -- MudBlazor's providers",
        "call into JavaScript, and bUnit's strict default throws on the first unplanned call, which reads",
        "as a broken component rather than a missing registration.",
        string.Empty,
        "**Service Bus and SignalR are not alternatives.** Service Bus is asynchronous messaging between",
        "services; SignalR is server-to-client push.",
        string.Empty,
        "**An Aspire AppHost is conditional.** More than one process, or one process plus a backing",
        "service a developer would otherwise start by hand. A single executable with no backing services",
        "does not get one.",
        string.Empty,
        "**Observability is registered by `AddServiceDefaults`, which every host calls.** Tracing,",
        "metrics and the kernel's logging bridge are configured in one project so two hosts cannot",
        "publish under different names. The exporter activates only where `OTEL_EXPORTER_OTLP_ENDPOINT`",
        "says where to send it, so a solution with no collector registers everything and exports",
        "nothing. Aspire sets that variable for every process it starts.",
        string.Empty,
        "**The source and meter names are pinned, and an architecture test holds them.** The kernel",
        "writes spans to an `ActivitySource` it names, and OpenTelemetry collects only from sources",
        "named in `AddSource`. Those are two strings in two calls, and where they disagree every span",
        "is discarded where it is created, with no error anywhere. That is why `ObservabilityTests`",
        "exists.",
        string.Empty,
        "**Observability is not conditional, and it is not the error envelope.** Every boundary response",
        "already carries a trace id, which is what lets somebody asking about one request find what was",
        "withheld from them. That is not tracing: nothing collects the spans and nothing exports them, so",
        "there is nothing to ask. A solution with no OpenTelemetry registration has no answer to \"what",
        "was slow\" beyond a log line somebody thought to write.",
        string.Empty,
        "**An OpenAPI document is how a caller finds the surface.** A hand-kept description of an HTTP",
        "API disagrees with the API, and the disagreement is discovered by whoever trusted it. The",
        "scaffold generates the document at `/openapi/v1.json`, renders it with Swagger UI at",
        "`/swagger/index.html`, and sends `/` there so the api is not a bodiless 404 in a browser.",
        "The generator and the viewer are separate packages on purpose: the document is what a client",
        "generator reads, and swapping the page never changes it.",
        string.Empty,
        "**All three are development-only, and that is a decision rather than an oversight.** The",
        "document names every route, field and status this process answers, which is a map for anybody",
        "who should not have one. A deployment that wants it public moves those lines out of the",
        "environment check and says so in a plan.",
        string.Empty,
        "**Liveness is enforced rather than declared**, and it is the one row here that binds: the",
        "scaffold maps `/health` and an architecture test fails the build if the route or its",
        "registration is removed. A process with no liveness route is one an orchestrator cannot tell",
        "apart from a process that is up and broken, so it keeps sending traffic to it -- and that cost",
        "is paid at the worst moment by somebody who did not delete the line. It is deliberately not an",
        "`ApiResult` endpoint: its caller is a load balancer, which understands a status code and the",
        "framework's own payload and has never heard of this solution's envelope.",
        string.Empty,
        "**Integration tests use real backing services.** The EF Core in-memory provider does not",
        "translate SQL, so it passes queries a real database rejects -- a proof that could pass while its",
        "criterion is unmet.",
        string.Empty,
        "**There is no device end-to-end layer.** A device-only outcome belongs in an intent as a bound,",
        "verified by a person outside the cycle; `CON_12` keeps it out of a spec.",
        string.Empty,
    ];

    private static readonly StackProfile _dotnet = new(
        DefaultName,
        "C# and .NET across api, web, desktop and Android. What a first stack draft is written from.",
        [
            new("language, framework", "C#, .NET"),
            new("unit and integration testing", "MSTest on Microsoft.Testing.Platform"),
            new("component testing", "bUnit, for Blazor components"),
            new("end-to-end testing", "Playwright, in Chromium and WebKit"),
            new("ORM", "EF Core"),
            new("orchestration", "Aspire, where there is more than one process"),
            new("results", "FluentResults"),
            new("validation", "FluentValidation, through a pipeline behaviour"),
            .. _platform,
            new("async messaging", "Azure Service Bus"),
            new("realtime push", "SignalR, only where a live UI needs it"),
            new("machine learning", "ML.NET"),
            new("api", "ASP.NET Core"),
            new("api description", "OpenAPI, through Microsoft.AspNetCore.OpenApi, read in Swagger UI"),
            new("tracing, metrics and logs", "OpenTelemetry, exported to Azure Monitor"),
            new("liveness", "ASP.NET Core health checks, at /health"),
            new("web", "Blazor"),
            new("desktop and Android", "MAUI Blazor Hybrid"),
            new("ui components", "MudBlazor"),
        ],
        _dotnetNotes);

    private static readonly StackProfile _tensorflow = new(
        "ml-tensorflow",
        "Python and TensorFlow for model work, on the same Azure platform. Rows for the options menu.",
        [
            new("language", "Python"),
            new("machine learning", "TensorFlow, through Keras"),
            new("arrays and dataframes", "NumPy and pandas"),
            new("exploration", "Jupyter notebooks, kept out of the import path"),
            new("experiment tracking", "MLflow"),
            new("training and deployment platform", "Azure Machine Learning"),
            new("model serving", "TensorFlow Serving behind FastAPI"),
            new("testing", "pytest"),
            new("linting and formatting", "ruff"),
            new("type checking", "mypy, in strict mode"),
            new("dependency and environment management", "uv, with a locked pyproject.toml"),
            new("containers", "Docker"),
            .. _platform,
            new("tracing, metrics and logs", "OpenTelemetry, exported to Azure Monitor"),
        ],
        []);

    /// <summary>The profile a first stack draft is written from.</summary>
    public static StackProfile Default => _dotnet;

    /// <summary>Every profile, in the order a listing shows them, the default first.</summary>
    public static IReadOnlyList<StackProfile> All { get; } = [_dotnet, _tensorflow];

    /// <summary>
    /// Finds a profile by name, or null when nothing answers to it.
    /// </summary>
    /// <param name="name">The name to resolve.</param>
    /// <returns>The profile, or null.</returns>
    public static StackProfile? Find(string? name) => string.IsNullOrWhiteSpace(name)
        ? null
        : All.FirstOrDefault(profile => string.Equals(
            profile.Name,
            name.Trim(),
            StringComparison.OrdinalIgnoreCase));
}

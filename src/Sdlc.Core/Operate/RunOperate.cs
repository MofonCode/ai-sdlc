using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Init;
using Sdlc.Core.Reports;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Operate;

/// <summary>
/// Operate: read what production raised, and file each new kind of error as a bug — a lead a person may take
/// into Define (D-82, D-89).
/// </summary>
/// <remarks>
/// <para><strong>Every step is deterministic, so the binary runs it</strong> (§15.2): the query, the grouping,
/// the scrubbing, the deduplication and the filing. Nothing here needs language, and nothing here opens a
/// cycle — a person choosing to start <c>intent</c> from a lead is the gate.</para>
/// <para><strong>Deduplicated against what is already filed.</strong> A signature with an open item is not
/// filed again. One a person dismissed — removed, won't fix, not planned — is not filed again either, which is
/// what makes dismissal durable. One that was closed as done and is happening again is filed again: that is a
/// regression, and worth a new lead.</para>
/// <para><strong>Ranked by how often it happened</strong>, the order telemetry leads were decided to take.</para>
/// <para><strong>A local solution has no tracker</strong>, so the leads are written as a report under
/// <c>.sdlc/reports/</c>, ignored by git, for a person to read and pass on by path.</para>
/// </remarks>
public static class RunOperate
{
    /// <summary>The prefix of a signature's tag, after which come twelve hex characters of its hash.</summary>
    public const string TagPrefix = CliWorkFiler.Label + "-";

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRunOperateRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for one Operate run.</summary>
    /// <param name="Window">How far back to read.</param>
    /// <param name="DryRun">Report what would be filed, and file nothing.</param>
    public sealed record Command(TimeSpan Window, bool DryRun) : IRequest<Response>;

    /// <summary>One error and what became of it.</summary>
    /// <param name="Error">The error, as the store grouped it.</param>
    /// <param name="Bug">The scrubbed bug for it.</param>
    /// <param name="Outcome">Filed with a link, would be filed, or why it was not.</param>
    public sealed record Lead(TelemetryError Error, BugToFile Bug, string Outcome);

    /// <summary>What a run found and did.</summary>
    /// <param name="Store">The telemetry store read, or null when the solution declares none.</param>
    /// <param name="Leads">Every error read, most frequent first, with what became of it.</param>
    /// <param name="Report">Where the report went, for a local solution.</param>
    public sealed record Response(string? Store, IReadOnlyList<Lead> Leads, string? Report);

    /// <summary>The tag that marks a signature on the tracker.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The tag.</returns>
    public static string TagOf(string signature) =>
        TagPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..12];

    /// <summary>The bug for an error: scrubbed, titled, and carrying what a bug-fix spec derives criteria from.</summary>
    /// <param name="error">The error.</param>
    /// <param name="store">Where it was read from.</param>
    /// <returns>The bug.</returns>
    public static BugToFile BugFor(TelemetryError error, string store)
    {
        ArgumentNullException.ThrowIfNull(error);

        string method = Scrub.Text(error.Method);
        string title = Scrub.Text(string.Create(
            CultureInfo.InvariantCulture,
            $"{ShortType(error.Type)} in {(method.Length == 0 ? "an unknown method" : method)}"));

        string body = string.Join(
            "\n",
            [
                string.Create(CultureInfo.InvariantCulture, $"Production raised {Scrub.Text(error.Type)} {error.Count} times between {error.First:u} and {error.Last:u}."),
                string.Empty,
                "Message, with personal data removed:",
                string.Empty,
                "    " + Scrub.Text(error.Message),
                string.Empty,
                "Thrown from: " + (method.Length == 0 ? "(not recorded)" : method),
                "Read from: " + store,
                string.Empty,
                "Filed by sdlc operate. It is a lead: a person decides whether it becomes a cycle, with /sdlc:intent --from <this item>. Close it as not to be done and it will not be filed again.",
            ]);

        return new BugToFile(TagOf(error.Signature), title, body);
    }

    private static string ShortType(string type)
    {
        int dot = type.LastIndexOf('.');
        return dot < 0 ? type : type[(dot + 1)..];
    }

    private sealed class Handler(SolutionRoot solution, ITelemetry telemetry, IWorkFiler filer, TimeProvider time)
        : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<SolutionDocument> read = await SolutionDocument.ReadFile(solution.Path, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SolutionDocument document = read.Value;

            if (document.Telemetry is null)
            {
                return Result.Ok(new Response(null, [], null));
            }

            const string AppInsights = "appinsights:";

            if (!document.Telemetry.StartsWith(AppInsights, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"telemetry is '{document.Telemetry}'. Application Insights is the store read so far: telemetry: appinsights:<application id>."));
            }

            string store = document.Telemetry[AppInsights.Length..].Trim();

            Result<IReadOnlyList<TelemetryError>> errors = await telemetry
                .Errors(store, request.Window, solution.Path, cancellationToken)
                .ConfigureAwait(false);

            if (errors.IsFailed)
            {
                return errors.ToResult<Response>();
            }

            IReadOnlyList<FiledSignature> filed = [];

            if (document.Platform != Platform.Local)
            {
                Result<IReadOnlyList<FiledSignature>> already = await filer.Filed(document.Platform, solution.Path, cancellationToken).ConfigureAwait(false);

                if (already.IsFailed)
                {
                    return already.ToResult<Response>();
                }

                filed = already.Value;
            }

            List<Lead> leads = [];

            foreach (TelemetryError error in errors.Value.OrderByDescending(e => e.Count))
            {
                BugToFile bug = BugFor(error, "Application Insights " + store);
                FiledSignature[] same = [.. filed.Where(f => f.Tag == bug.Tag)];

                if (same.Any(f => f.Open))
                {
                    leads.Add(new Lead(error, bug, "already filed and open"));
                    continue;
                }

                if (same.Any(f => f.Dismissed))
                {
                    leads.Add(new Lead(error, bug, "dismissed by a person, so not filed again"));
                    continue;
                }

                if (request.DryRun || document.Platform == Platform.Local)
                {
                    leads.Add(new Lead(error, bug, same.Length > 0 ? "would be filed again: it was closed and it is happening again" : "would be filed"));
                    continue;
                }

                Result<string> link = await filer.File(document.Platform, bug, solution.Path, cancellationToken).ConfigureAwait(false);
                leads.Add(new Lead(error, bug, link.IsSuccess ? "filed as " + link.Value : "could not be filed: " + string.Join(" ", link.Errors.Select(e => e.Message))));
            }

            string? report = document.Platform == Platform.Local && leads.Count > 0
                ? await ReportFile.Write(solution.Path, "leads", Render(new Response(store, leads, null)), time.GetUtcNow(), cancellationToken).ConfigureAwait(false)
                : null;

            return Result.Ok(new Response(store, leads, report));
        }
    }

    /// <summary>Renders a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response run)
    {
        ArgumentNullException.ThrowIfNull(run);

        StringBuilder output = new();

        if (run.Store is null)
        {
            output.AppendLine("This solution declares no telemetry store, so Operate has nothing to read. Declare one in solution.md as telemetry: appinsights:<application id>.");
            return output.ToString();
        }

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Operate read Application Insights {run.Store}: {run.Leads.Count} kinds of error, most frequent first."));

        foreach (Lead lead in run.Leads)
        {
            output.AppendLine();
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {lead.Error.Count,6}x  {lead.Bug.Title}"));
            output.AppendLine("          " + lead.Outcome);
        }

        if (run.Report is not null)
        {
            output.AppendLine();
            output.AppendLine("A local solution has no tracker, so these are kept as a report at " + run.Report + ". Copy one into a file to begin a cycle from it.");
        }

        return output.ToString();
    }
}

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentResults;

namespace Sdlc.Core.Operate;

/// <summary>One kind of error production raised, grouped.</summary>
/// <param name="Signature">What makes two occurrences the same error: the exception type and where it was thrown.</param>
/// <param name="Type">The exception type.</param>
/// <param name="Method">The method it was thrown from.</param>
/// <param name="Message">A sample message, not yet scrubbed.</param>
/// <param name="Count">How many times it happened in the window.</param>
/// <param name="First">When it was first seen in the window.</param>
/// <param name="Last">When it was last seen.</param>
public sealed record TelemetryError(
    string Signature,
    string Type,
    string Method,
    string Message,
    long Count,
    DateTimeOffset First,
    DateTimeOffset Last);

/// <summary>Reads grouped errors from a telemetry store.</summary>
/// <remarks>An interface so the suite needs no store, no network and no credentials.</remarks>
public interface ITelemetry
{
    /// <summary>The errors the store holds for a window, grouped by signature.</summary>
    /// <param name="store">The store, as <c>solution.md</c> declares it, without its prefix.</param>
    /// <param name="window">How far back to read.</param>
    /// <param name="workingDirectory">Where to run from.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The errors, or a failure saying what could not be reached and how to fix it.</returns>
    Task<Result<IReadOnlyList<TelemetryError>>> Errors(string store, TimeSpan window, string workingDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// Reads Application Insights through <c>az monitor app-insights query</c>, so the binary holds no token.
/// </summary>
/// <remarks>
/// Grouped in the query rather than here: Application Insights already keys an exception by its type and
/// the method it was thrown from (<c>problemId</c>), which is exactly the signature D-89 named, and grouping
/// in the store keeps a busy service's millions of rows out of this process.
/// </remarks>
public sealed class AppInsightsTelemetry : ITelemetry
{
    /// <summary>The query for a window: one row per signature.</summary>
    /// <param name="window">How far back, as KQL writes a span, as in <c>24h</c>.</param>
    /// <returns>The query.</returns>
    public static string Query(string window) =>
        "exceptions | where timestamp > ago(" + window + ") "
        + "| summarize count = count(), first = min(timestamp), last = max(timestamp), message = take_any(outerMessage) "
        + "by problemId, type, method = outerMethod | order by count desc";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<TelemetryError>>> Errors(
        string store,
        TimeSpan window,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        string span = string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (int)window.TotalHours)}h");
        string kql = Query(span);

        ProcessStartInfo start = new("az")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in new[] { "monitor", "app-insights", "query", "--app", store, "--analytics-query", kql, "--output", "json" })
        {
            start.ArgumentList.Add(argument);
        }

        string output;

        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("az did not start.");
            Task<string> read = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            output = await read.ConfigureAwait(false);
            string complained = await error.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return Result.Fail<IReadOnlyList<TelemetryError>>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Application Insights could not be read: {complained.Trim()} Sign in with 'az login', add the application-insights extension, and check the application id in solution.md."));
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Result.Fail<IReadOnlyList<TelemetryError>>(
                "'az' is not on the path. Install the Azure CLI and sign in with 'az login'.");
        }

        return Read(output);
    }

    /// <summary>Reads the CLI's table into errors.</summary>
    /// <param name="json">What <c>az monitor app-insights query</c> printed.</param>
    /// <returns>The errors.</returns>
    public static Result<IReadOnlyList<TelemetryError>> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement table = document.RootElement.GetProperty("tables")[0];

        List<string> columns = [.. table.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("name").GetString() ?? string.Empty)];
        List<TelemetryError> errors = [];

        foreach (JsonElement row in table.GetProperty("rows").EnumerateArray())
        {
            string Text(string name) => row[columns.IndexOf(name)].ToString();

            errors.Add(new TelemetryError(
                Text("problemId"),
                Text("type"),
                Text("method"),
                Text("message"),
                long.Parse(Text("count"), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(Text("first"), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(Text("last"), CultureInfo.InvariantCulture)));
        }

        return Result.Ok<IReadOnlyList<TelemetryError>>(errors);
    }
}

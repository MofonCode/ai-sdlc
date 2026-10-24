using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentResults;
using Sdlc.Core.Init;

namespace Sdlc.Core.Operate;

/// <summary>A bug Operate would file, already scrubbed.</summary>
/// <param name="Tag">The signature's tag, which is how the next run knows it was filed.</param>
/// <param name="Title">The title.</param>
/// <param name="Body">The description, as Markdown.</param>
public sealed record BugToFile(string Tag, string Title, string Body);

/// <summary>What the tracker already holds for a signature.</summary>
/// <param name="Tag">The signature's tag.</param>
/// <param name="Open">Whether an item carrying it is still open.</param>
/// <param name="Dismissed">Whether a person closed one as not to be done — removed, won't fix, not planned.</param>
public sealed record FiledSignature(string Tag, bool Open, bool Dismissed);

/// <summary>Reads what Operate filed before, and files what it found now.</summary>
/// <remarks>An interface so the suite needs no tracker, no network and no credentials.</remarks>
public interface IWorkFiler
{
    /// <summary>Every item Operate has filed on this platform, by signature.</summary>
    /// <param name="platform">Where the solution lives, which decides the tracker.</param>
    /// <param name="workingDirectory">Where to run from.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>What is filed, or a failure saying how to reach the tracker.</returns>
    Task<Result<IReadOnlyList<FiledSignature>>> Filed(Platform platform, string workingDirectory, CancellationToken cancellationToken);

    /// <summary>Files one bug.</summary>
    /// <param name="platform">Where the solution lives.</param>
    /// <param name="bug">The bug.</param>
    /// <param name="workingDirectory">Where to run from.</param>
    /// <param name="cancellationToken">Token observed while filing.</param>
    /// <returns>A link to the item, or a failure.</returns>
    Task<Result<string>> File(Platform platform, BugToFile bug, string workingDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// Files through <c>az boards</c> on Azure DevOps and <c>gh issue</c> on GitHub, so the binary holds no token.
/// </summary>
/// <remarks>
/// <para><strong>The tracker is the platform's own.</strong> A solution on Azure DevOps triages on its boards;
/// one on GitHub triages issues. Every item Operate files carries the label <c>sdlc-operate</c> and its
/// signature, which is how the next run deduplicates and how a person filters the board.</para>
/// </remarks>
public sealed class CliWorkFiler : IWorkFiler
{
    /// <summary>The label every item Operate files carries.</summary>
    public const string Label = "sdlc-operate";

    private static readonly string[] _dismissedStates = ["Removed"];
    private static readonly string[] _closedStates = ["Closed", "Done", "Resolved", "Removed"];

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FiledSignature>>> Filed(Platform platform, string workingDirectory, CancellationToken cancellationToken)
    {
        if (platform == Platform.AzureDevOps)
        {
            Result<string> output = await Run(
                workingDirectory,
                ["boards", "query", "--wiql", $"SELECT [System.Id], [System.Tags], [System.State] FROM WorkItems WHERE [System.Tags] CONTAINS '{Label}'", "--output", "json"],
                "az",
                cancellationToken).ConfigureAwait(false);

            if (output.IsFailed)
            {
                return output.ToResult<IReadOnlyList<FiledSignature>>();
            }

            using var json = JsonDocument.Parse(output.Value);
            List<FiledSignature> filed = [];

            foreach (JsonElement item in json.RootElement.EnumerateArray())
            {
                JsonElement fields = item.GetProperty("fields");
                string tags = fields.TryGetProperty("System.Tags", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
                string state = fields.TryGetProperty("System.State", out JsonElement s) ? s.GetString() ?? string.Empty : string.Empty;

                foreach (string tag in tags.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(tag => tag.StartsWith(Label + "-", StringComparison.Ordinal)))
                {
                    filed.Add(new FiledSignature(tag, !_closedStates.Contains(state, StringComparer.OrdinalIgnoreCase), _dismissedStates.Contains(state, StringComparer.OrdinalIgnoreCase)));
                }
            }

            return Result.Ok<IReadOnlyList<FiledSignature>>(filed);
        }

        Result<string> issues = await Run(
            workingDirectory,
            ["issue", "list", "--label", Label, "--state", "all", "--limit", "1000", "--json", "state,stateReason,body"],
            "gh",
            cancellationToken).ConfigureAwait(false);

        if (issues.IsFailed)
        {
            return issues.ToResult<IReadOnlyList<FiledSignature>>();
        }

        using var list = JsonDocument.Parse(issues.Value);
        List<FiledSignature> found = [];

        foreach (JsonElement issue in list.RootElement.EnumerateArray())
        {
            string body = issue.TryGetProperty("body", out JsonElement b) ? b.GetString() ?? string.Empty : string.Empty;
            string state = issue.TryGetProperty("state", out JsonElement s) ? s.GetString() ?? string.Empty : string.Empty;
            string reason = issue.TryGetProperty("stateReason", out JsonElement r) ? r.GetString() ?? string.Empty : string.Empty;

            int marker = body.IndexOf("<!-- " + Label + "-", StringComparison.Ordinal);

            if (marker < 0)
            {
                continue;
            }

            int end = body.IndexOf(" -->", marker, StringComparison.Ordinal);
            string tag = body[(marker + 5)..(end < 0 ? body.Length : end)].Trim();

            found.Add(new FiledSignature(tag, state.Equals("OPEN", StringComparison.OrdinalIgnoreCase), reason.Equals("NOT_PLANNED", StringComparison.OrdinalIgnoreCase)));
        }

        return Result.Ok<IReadOnlyList<FiledSignature>>(found);
    }

    /// <inheritdoc />
    public async Task<Result<string>> File(Platform platform, BugToFile bug, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bug);

        if (platform == Platform.AzureDevOps)
        {
            Result<string> created = await Run(
                workingDirectory,
                ["boards", "work-item", "create", "--type", "Bug", "--title", bug.Title, "--description", bug.Body.Replace("\n", "<br/>", StringComparison.Ordinal), "--fields", $"System.Tags={Label}; {bug.Tag}", "--output", "json"],
                "az",
                cancellationToken).ConfigureAwait(false);

            if (created.IsFailed)
            {
                return created;
            }

            using var json = JsonDocument.Parse(created.Value);
            return Result.Ok(json.RootElement.TryGetProperty("id", out JsonElement id)
                ? "azuredevops:" + id.ToString()
                : "azuredevops:(filed)");
        }

        await Run(workingDirectory, ["label", "create", Label, "--force", "--description", "Filed by sdlc operate from production telemetry"], "gh", cancellationToken).ConfigureAwait(false);

        Result<string> issue = await Run(
            workingDirectory,
            ["issue", "create", "--title", bug.Title, "--body", bug.Body + "\n\n<!-- " + bug.Tag + " -->", "--label", Label],
            "gh",
            cancellationToken).ConfigureAwait(false);

        return issue.IsFailed ? issue : Result.Ok(issue.Value.Trim());
    }

    private static async Task<Result<string>> Run(string workingDirectory, string[] arguments, string tool, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ToolPath.Resolve(tool))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException(tool + " did not start.");
            Task<string> read = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string output = await read.ConfigureAwait(false);
            string complained = await error.ConfigureAwait(false);

            return process.ExitCode == 0
                ? Result.Ok(output)
                : Result.Fail<string>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{tool} {arguments[0]} {arguments[1]}' failed: {complained.Trim()} Sign in with '{(tool == "az" ? "az login" : "gh auth login")}'."));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"'{tool}' is not on the path."));
        }
    }
}

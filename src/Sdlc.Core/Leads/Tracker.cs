using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentResults;

namespace Sdlc.Core.Leads;

/// <summary>What a tracker said about one item, before it is made into a lead.</summary>
/// <param name="Title">Its title.</param>
/// <param name="Kind">What kind of item: a bug, a user story, an issue.</param>
/// <param name="Description">Its description, as plain text.</param>
/// <param name="Reproduction">How to reproduce it, when the tracker keeps that apart.</param>
/// <param name="Link">Where a person opens it.</param>
public sealed record TrackerItem(string Title, string? Kind, string Description, string? Reproduction, string? Link);

/// <summary>
/// Reads an item from a work tracker.
/// </summary>
/// <remarks>
/// An interface because the suite must not need a network, credentials or an installed CLI, for the same
/// reason <c>IRepository</c> is one.
/// </remarks>
public interface ITracker
{
    /// <summary>Fetches one item.</summary>
    /// <param name="reference">An Azure DevOps, GitHub or Jira reference.</param>
    /// <param name="workingDirectory">The repository, which decides which GitHub repository is meant.</param>
    /// <param name="cancellationToken">Token observed while fetching.</param>
    /// <returns>The item, or a failure saying what could not be reached and how to fix it.</returns>
    Task<Result<TrackerItem>> Fetch(LeadReference reference, string workingDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// Reads work items through the platforms' own command-line tools: <c>az boards</c> and <c>gh</c>.
/// </summary>
/// <remarks>
/// <para><strong>The tools own the credentials.</strong> A person who can see the item in a terminal can hand
/// it to a cycle, and the binary never holds a token: <c>az login</c> and <c>gh auth login</c> are where
/// access is granted, and a pipeline grants it the same way. Credentials never go in
/// <c>solution.md</c> (D-89).</para>
/// <para><strong>Jira is not read yet.</strong> It has no first-party CLI, and the order decided was Azure
/// DevOps, then GitHub, then Jira. Asked for one, this says so rather than guessing at an API.</para>
/// </remarks>
public sealed partial class CliTracker : ITracker
{
    /// <inheritdoc />
    public async Task<Result<TrackerItem>> Fetch(LeadReference reference, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return reference.Source switch
        {
            LeadSource.AzureDevOps => await AzureDevOps(reference.Value, workingDirectory, cancellationToken).ConfigureAwait(false),
            LeadSource.GitHub => await GitHub(reference.Value, workingDirectory, cancellationToken).ConfigureAwait(false),
            LeadSource.Jira => Result.Fail<TrackerItem>(
                "Jira issues are not read yet: Azure DevOps and GitHub come first. Copy the issue into a file and pass its path."),
            _ => Result.Fail<TrackerItem>(string.Create(CultureInfo.InvariantCulture, $"{reference} is not a tracker item.")),
        };
    }

    private static async Task<Result<TrackerItem>> AzureDevOps(string id, string workingDirectory, CancellationToken cancellationToken)
    {
        Result<string> output = await Run(
            "az",
            $"boards work-item show --id {id} --output json",
            workingDirectory,
            "Install the Azure CLI and its azure-devops extension, sign in with 'az login', and set the organization with 'az devops configure --defaults organization=https://dev.azure.com/<yours>'.",
            cancellationToken).ConfigureAwait(false);

        if (output.IsFailed)
        {
            return output.ToResult<TrackerItem>();
        }

        using var json = JsonDocument.Parse(output.Value);
        JsonElement fields = json.RootElement.GetProperty("fields");

        return Result.Ok(new TrackerItem(
            Field(fields, "System.Title") ?? "(untitled)",
            Field(fields, "System.WorkItemType"),
            PlainText(Field(fields, "System.Description") ?? Field(fields, "Microsoft.VSTS.Common.AcceptanceCriteria") ?? string.Empty),
            Field(fields, "Microsoft.VSTS.TCM.ReproSteps") is { } repro ? PlainText(repro) : null,
            json.RootElement.TryGetProperty("_links", out JsonElement links)
                && links.TryGetProperty("html", out JsonElement html)
                && html.TryGetProperty("href", out JsonElement href)
                    ? href.GetString()
                    : json.RootElement.TryGetProperty("url", out JsonElement url) ? url.GetString() : null));
    }

    private static async Task<Result<TrackerItem>> GitHub(string number, string workingDirectory, CancellationToken cancellationToken)
    {
        Result<string> output = await Run(
            "gh",
            $"issue view {number} --json title,body,url,labels",
            workingDirectory,
            "Install the GitHub CLI and sign in with 'gh auth login'. The issue is read from this repository's GitHub remote.",
            cancellationToken).ConfigureAwait(false);

        if (output.IsFailed)
        {
            return output.ToResult<TrackerItem>();
        }

        using var json = JsonDocument.Parse(output.Value);
        JsonElement root = json.RootElement;

        string? labels = root.TryGetProperty("labels", out JsonElement list) && list.ValueKind == JsonValueKind.Array
            ? string.Join(", ", list.EnumerateArray().Select(l => l.TryGetProperty("name", out JsonElement n) ? n.GetString() : null).OfType<string>())
            : null;

        return Result.Ok(new TrackerItem(
            root.GetProperty("title").GetString() ?? "(untitled)",
            string.IsNullOrWhiteSpace(labels) ? "issue" : "issue (" + labels + ")",
            root.TryGetProperty("body", out JsonElement body) ? body.GetString() ?? string.Empty : string.Empty,
            null,
            root.TryGetProperty("url", out JsonElement url) ? url.GetString() : null));
    }

    private static string? Field(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// Azure DevOps keeps rich text as HTML. A lead is read by an interview, so it is made plain: the
    /// breaks kept as lines, the tags dropped, the entities decoded.
    /// </summary>
    private static string PlainText(string html)
    {
        string broken = Breaks().Replace(html, "\n");
        string stripped = Tags().Replace(broken, string.Empty);
        return string.Join(
            '\n',
            WebUtility.HtmlDecode(stripped)
                .Split('\n')
                .Select(line => line.TrimEnd())
                .Where((line, index) => line.Length > 0 || index > 0))
            .Trim();
    }

    private static async Task<Result<string>> Run(
        string tool,
        string arguments,
        string workingDirectory,
        string remedy,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ToolPath.Resolve(tool), arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        try
        {
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException(tool + " did not start.");

            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            string said = await output.ConfigureAwait(false);
            string complained = (await error.ConfigureAwait(false)).Trim();

            return process.ExitCode == 0
                ? Result.Ok(said)
                : Result.Fail<string>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{tool} {arguments}' failed: {complained} {remedy}"));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"'{tool}' is not on the path. {remedy}"));
        }
    }

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</li>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Breaks();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}

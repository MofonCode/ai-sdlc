using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentResults;
using Sdlc.Core.Init;

namespace Sdlc.Core.Diagnostics;

/// <summary>What the platform said about whether a merge into a branch needs the pipeline to pass.</summary>
public enum PolicyFinding
{
    /// <summary>A merge into the branch needs the pipeline that runs the chain to pass.</summary>
    Required,

    /// <summary>The platform answered, and nothing it holds makes that pipeline a condition of merging.</summary>
    NotRequired,

    /// <summary>The platform could not be asked: no CLI, not signed in, no remote, or no permission.</summary>
    Unasked,
}

/// <summary>One answer from the platform.</summary>
/// <param name="Finding">What it said, or that it was not asked.</param>
/// <param name="Detail">What was found, or why it could not be asked, stated so a person can act on it.</param>
public sealed record PolicyAnswer(PolicyFinding Finding, string Detail);

/// <summary>
/// Asks the platform whether a merge into a branch needs the pipeline that runs <c>sdlc verify-chain</c>.
/// </summary>
/// <remarks>
/// An interface for the reason <c>ITracker</c> is one: the suite must not need a network, credentials or an
/// installed CLI.
/// </remarks>
public interface IBranchPolicies
{
    /// <summary>Asks.</summary>
    /// <param name="platform">Where the repository is hosted. Never local.</param>
    /// <param name="branch">The branch a merge goes into.</param>
    /// <param name="pipelineFile">The pipeline file that runs the chain, relative to the repository, with forward slashes.</param>
    /// <param name="workingDirectory">The repository, which decides which remote repository is meant.</param>
    /// <param name="cancellationToken">Token observed while asking.</param>
    /// <returns>The answer. Never a failure: a question that could not be asked is an answer of its own.</returns>
    Task<PolicyAnswer> Ask(Platform platform, string branch, string pipelineFile, string workingDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// Asks through the platforms' own command-line tools: <c>az repos policy</c> and <c>gh api</c>.
/// </summary>
/// <remarks>
/// <para><strong>The tools own the credentials</strong>, as they do for leads (D-89). A person signed in to
/// the CLI is asked on their behalf, and the binary never holds a token.</para>
///
/// <para><strong>Anything that stops the question is <see cref="PolicyFinding.Unasked"/>, never a
/// problem.</strong> Not signed in, no CLI, no remote, no permission to read the policy: each one is the
/// binary not knowing, and an install is not broken because the person running the doctor has not signed
/// in. Only an answer the platform actually gave can say the check is missing.</para>
///
/// <para><strong>On GitHub, both places a required check can live are read.</strong> A classic branch
/// protection rule and a ruleset are separate features, and a repository may use either. The branch is
/// read rather than its protection, because the protection endpoint needs admin and the branch's own
/// summary of it does not.</para>
/// </remarks>
public sealed class CliBranchPolicies : IBranchPolicies
{
    /// <summary>The job the pipeline adoption writes for GitHub, which is the status check's name.</summary>
    public const string GitHubCheck = "verify";

    /// <inheritdoc />
    public async Task<PolicyAnswer> Ask(
        Platform platform,
        string branch,
        string pipelineFile,
        string workingDirectory,
        CancellationToken cancellationToken) => platform switch
        {
            Platform.AzureDevOps => await AzureDevOps(branch, pipelineFile, workingDirectory, cancellationToken).ConfigureAwait(false),
            Platform.GitHub => await GitHub(branch, workingDirectory, cancellationToken).ConfigureAwait(false),
            _ => new PolicyAnswer(PolicyFinding.Unasked, "a local solution has no platform to ask"),
        };

    private static async Task<PolicyAnswer> AzureDevOps(
        string branch,
        string pipelineFile,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        Result<string> remote = await Run("git", "remote get-url origin", workingDirectory, cancellationToken).ConfigureAwait(false);

        if (remote.IsFailed || RepositoryName(remote.Value) is not { } name)
        {
            return new PolicyAnswer(PolicyFinding.Unasked, "this repository has no Azure Repos remote named origin to ask about");
        }

        Result<string> id = await Run(
            "az",
            $"repos show --repository {name} --query id --output tsv",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (id.IsFailed)
        {
            return Unasked(id);
        }

        Result<string> listed = await Run(
            "az",
            $"repos policy list --repository-id {id.Value.Trim()} --branch refs/heads/{branch} --output json",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (listed.IsFailed)
        {
            return Unasked(listed);
        }

        List<string> others = [];

        foreach (int definition in BlockingBuildPolicies(listed.Value))
        {
            Result<string> yaml = await Run(
                "az",
                string.Create(CultureInfo.InvariantCulture, $"pipelines show --id {definition} --query process.yamlFilename --output tsv"),
                workingDirectory,
                cancellationToken).ConfigureAwait(false);

            if (yaml.IsFailed)
            {
                return Unasked(yaml);
            }

            string file = yaml.Value.Trim().TrimStart('/').Replace('\\', '/');

            if (file.Equals(pipelineFile, StringComparison.OrdinalIgnoreCase))
            {
                return new PolicyAnswer(
                    PolicyFinding.Required,
                    $"a required build validation policy on {branch} runs {pipelineFile}");
            }

            others.Add(file);
        }

        return new PolicyAnswer(
            PolicyFinding.NotRequired,
            others.Count == 0
                ? $"{branch} has no required build validation policy, so on Azure DevOps {pipelineFile} does not run on a pull request at all"
                : $"the required build validation on {branch} runs {string.Join(", ", others)}, not {pipelineFile}, so the chain is not checked before a merge");
    }

    private static async Task<PolicyAnswer> GitHub(string branch, string workingDirectory, CancellationToken cancellationToken)
    {
        Result<string> summary = await Run(
            "gh",
            $"api repos/{{owner}}/{{repo}}/branches/{branch}",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (summary.IsFailed)
        {
            return Unasked(summary);
        }

        Result<string> rules = await Run(
            "gh",
            $"api repos/{{owner}}/{{repo}}/rules/branches/{branch}",
            workingDirectory,
            cancellationToken).ConfigureAwait(false);

        if (rules.IsFailed)
        {
            return Unasked(rules);
        }

        string[] required = [.. ProtectedContexts(summary.Value).Concat(RulesetContexts(rules.Value)).Distinct(StringComparer.Ordinal)];

        if (required.Any(context => context.Equals(GitHubCheck, StringComparison.OrdinalIgnoreCase)))
        {
            return new PolicyAnswer(PolicyFinding.Required, $"a merge into {branch} requires the '{GitHubCheck}' status check");
        }

        return new PolicyAnswer(
            PolicyFinding.NotRequired,
            required.Length == 0
                ? $"{branch} requires no status check, so a pull request merges whether the chain passed or never ran"
                : $"{branch} requires {string.Join(", ", required.Select(context => "'" + context + "'"))} and not '{GitHubCheck}', so the chain is not a condition of merging");
    }

    /// <summary>The definitions of every enabled, blocking build validation policy in a policy list.</summary>
    /// <param name="json">What <c>az repos policy list</c> printed.</param>
    /// <returns>The pipeline definition ids.</returns>
    public static IReadOnlyList<int> BlockingBuildPolicies(string json)
    {
        using var document = JsonDocument.Parse(json);

        return [.. document.RootElement.EnumerateArray()
            .Where(policy => policy.TryGetProperty("isEnabled", out JsonElement enabled) && enabled.ValueKind == JsonValueKind.True
                && policy.TryGetProperty("isBlocking", out JsonElement blocking) && blocking.ValueKind == JsonValueKind.True
                && policy.TryGetProperty("type", out JsonElement type)
                && type.TryGetProperty("displayName", out JsonElement display)
                && string.Equals(display.GetString(), "Build", StringComparison.Ordinal))
            .Select(policy => policy.TryGetProperty("settings", out JsonElement settings)
                && settings.TryGetProperty("buildDefinitionId", out JsonElement definition)
                && definition.TryGetInt32(out int value) ? value : 0)
            .Where(definition => definition > 0)];
    }

    /// <summary>The required status checks a branch's classic protection names, when it is enforced.</summary>
    /// <param name="json">What <c>gh api repos/{owner}/{repo}/branches/{branch}</c> printed.</param>
    /// <returns>The check names.</returns>
    public static IReadOnlyList<string> ProtectedContexts(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("protection", out JsonElement protection)
            || !protection.TryGetProperty("required_status_checks", out JsonElement checks)
            || (checks.TryGetProperty("enforcement_level", out JsonElement level)
                && string.Equals(level.GetString(), "off", StringComparison.Ordinal)))
        {
            return [];
        }

        IEnumerable<string> contexts = checks.TryGetProperty("contexts", out JsonElement list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(context => context.GetString()).OfType<string>()
            : [];

        IEnumerable<string> named = checks.TryGetProperty("checks", out JsonElement objects) && objects.ValueKind == JsonValueKind.Array
            ? objects.EnumerateArray()
                .Select(check => check.TryGetProperty("context", out JsonElement context) ? context.GetString() : null)
                .OfType<string>()
            : [];

        return [.. contexts.Concat(named)];
    }

    /// <summary>The required status checks every active ruleset on a branch names.</summary>
    /// <param name="json">What <c>gh api repos/{owner}/{repo}/rules/branches/{branch}</c> printed.</param>
    /// <returns>The check names.</returns>
    public static IReadOnlyList<string> RulesetContexts(string json)
    {
        using var document = JsonDocument.Parse(json);

        return [.. document.RootElement.EnumerateArray()
            .Where(rule => rule.TryGetProperty("type", out JsonElement type)
                && string.Equals(type.GetString(), "required_status_checks", StringComparison.Ordinal))
            .SelectMany(rule => rule.TryGetProperty("parameters", out JsonElement parameters)
                && parameters.TryGetProperty("required_status_checks", out JsonElement checks)
                && checks.ValueKind == JsonValueKind.Array
                    ? checks.EnumerateArray()
                        .Select(check => check.TryGetProperty("context", out JsonElement context) ? context.GetString() : null)
                        .OfType<string>()
                        .ToArray()
                    : [])];
    }

    /// <summary>The repository's name in an Azure Repos remote, over https or ssh.</summary>
    /// <param name="remote">The remote's url.</param>
    /// <returns>The name, or null when the url is not an Azure Repos one.</returns>
    public static string? RepositoryName(string remote)
    {
        ArgumentNullException.ThrowIfNull(remote);

        string url = remote.Trim().TrimEnd('/');
        int git = url.IndexOf("/_git/", StringComparison.OrdinalIgnoreCase);

        if (git >= 0)
        {
            return Uri.UnescapeDataString(url[(git + "/_git/".Length)..]);
        }

        // git@ssh.dev.azure.com:v3/<organisation>/<project>/<repository>
        return url.Contains("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase)
            || url.Contains("vs-ssh.visualstudio.com", StringComparison.OrdinalIgnoreCase)
                ? Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..])
                : null;
    }

    private static PolicyAnswer Unasked(Result<string> failed) =>
        new(PolicyFinding.Unasked, string.Join("; ", failed.Errors.Select(error => error.Message)));

    private static async Task<Result<string>> Run(
        string tool,
        string arguments,
        string workingDirectory,
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

            // The first line only: the CLIs follow a refusal with paragraphs of help.
            string first = complained.Split('\n', 2)[0].Trim();

            return process.ExitCode == 0
                ? Result.Ok(said)
                : Result.Fail<string>($"'{tool} {arguments}' failed{(first.Length > 0 ? ": " + first : string.Empty)}");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Result.Fail<string>($"'{tool}' is not on the path");
        }
    }
}


namespace Sdlc.Core.Init;

/// <summary>
/// The upgrade pipeline: a run a person starts that moves the solution to another sdlc release and opens the pull
/// request for it (D-201).
/// </summary>
/// <remarks>
/// <para><strong>Commands only, no agent.</strong> Moving the pin, running adoption as the new release and
/// rewriting the factory's pipeline are all the binary's, so the run needs no credential for a model and decides
/// nothing. The pull request is the decision, and a person makes it.</para>
/// <para><strong>It installs the release and hands over to it</strong> (D-213). Everything after the install is
/// <see cref="ApplyUpgrade"/>, run as the release being moved to, so this file carries nothing that could go stale.</para>
/// <para><strong>Never on a schedule.</strong> Taking a release is done on purpose, as starting the factory is
/// (D-143). Nothing here watches nuget.org.</para>
/// <para><strong>It moves the repository and nothing else.</strong> An agent that runs the solution's pipelines
/// needs no rebuild for a new release: every run restores the release its branch pins. What lives in an agent's
/// image is the solution's, and the <c>agent-image</c> guidance says how to move it.</para>
/// <para><strong>It stages only what an upgrade may carry</strong>, the paths <see cref="Chain.VerifyUpgrade"/> reads
/// as one, and opens the pull request only when that check holds.</para>
/// <para><strong>The agent is the platform's to name</strong>: <c>SDLC_AGENT_POOL</c> on Azure DevOps, resolved when the
/// run is queued, and <c>SDLC_RUNNER</c> on GitHub, a hosted runner when unset. The file is compared exactly and
/// rewritten every run, so it can hold no team's pool. Azure DevOps cannot default the variable: a template expression
/// does not see one set on the pipeline, and a run without it fails naming it.</para>
/// <para><strong>Neither template has run against a real server</strong>, and each says so at the top (D-32).</para>
/// </remarks>
public static class UpgradePipeline
{
    /// <summary>Where the upgrade pipeline lives in a solution.</summary>
    /// <param name="platform">The platform.</param>
    /// <returns>The path, relative to the root.</returns>
    public static string PathOf(Platform platform) => platform switch
    {
        Platform.GitHub => Path.Combine(".github", "workflows", "sdlc-upgrade.yml"),
        Platform.AzureDevOps => Path.Combine(Initialise.AzureDevOpsDirectory, "sdlc-upgrade.yml"),
        _ => throw new ArgumentOutOfRangeException(nameof(platform), "A local solution has nowhere to run a pipeline."),
    };

    /// <summary>The upgrade pipeline for a platform.</summary>
    /// <param name="platform">The platform. Not local.</param>
    /// <param name="integrationBranch">The branch the upgrade starts from and targets.</param>
    /// <returns>The file's contents.</returns>
    public static string Text(Platform platform, string integrationBranch) => platform switch
    {
        Platform.GitHub => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run. It is written from the documentation; if the first run fails",
                "# on syntax or on an action's version, that is why. Watch the first run.",
                "#",
                "# The upgrade (CONCEPT.md D-201). A person starts it, from the Actions tab or with",
                "# 'gh workflow run sdlc-upgrade.yml'. It installs the sdlc release asked for and runs 'sdlc upgrade apply' as it,",
                $"# which moves the solution, checks the move as the pull request will, and opens one into {integrationBranch}.",
                "# The pull-request check reads it as an upgrade (D-133). It merges nothing, and it never runs on a",
                "# schedule, because a release is taken on purpose.",
                "#",
                "# Set up once, in Settings > Secrets and variables > Actions:",
                "#   A GitHub App installed on this repository that may write contents and pull requests:",
                "#     SDLC_UPGRADE_APP_ID   a variable, the app's id",
                "#     SDLC_UPGRADE_APP_KEY  a secret, its private key",
                "#   Not GITHUB_TOKEN: a pull request opened with it starts no workflow, so the pull-request",
                "#   check would never run on it.",
                "#   SDLC_RUNNER, a variable, only to run on runners of your own: the label 'runs-on' takes.",
                "#     Unset, the run takes a GitHub-hosted runner.",
                "name: upgrade",
                string.Empty,
                "# Started by a person, never on a schedule (D-201).",
                "on:",
                "  workflow_dispatch:",
                "    inputs:",
                "      release:",
                "        description: The sdlc release to move to. Empty takes the newest on nuget.org.",
                "        required: false",
                "        default: ''",
                string.Empty,
                "concurrency:",
                "  group: sdlc-upgrade",
                "  cancel-in-progress: false",
                string.Empty,
                "jobs:",
                "  upgrade:",
                "    runs-on: ${{ vars.SDLC_RUNNER || 'ubuntu-latest' }}",
                "    timeout-minutes: 30",
                "    steps:",
                "      - id: token",
                "        uses: actions/create-github-app-token@v2",
                "        with:",
                "          app-id: ${{ vars.SDLC_UPGRADE_APP_ID }}",
                "          private-key: ${{ secrets.SDLC_UPGRADE_APP_KEY }}",
                string.Empty,
                "      - uses: actions/checkout@v5",
                "        with:",
                $"          ref: {integrationBranch}",
                "          token: ${{ steps.token.outputs.token }}",
                string.Empty,
                .. Templates.SelectSdkGitHub("      "),
                string.Empty,
                "      - name: move to the release and open the pull request",
                "        env:",
                "          RELEASE: ${{ inputs.release }}",
                "          GH_TOKEN: ${{ steps.token.outputs.token }}",
                "        run: |",
                "          git config user.name 'sdlc upgrade'",
                "          git config user.email 'sdlc-upgrade@users.noreply.github.com'",
                .. Move("          "),
                string.Empty,
            ]),

        Platform.AzureDevOps => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run against a real Azure DevOps server. It is written from the",
                "# documentation; if the first run fails on syntax or on a task's version, that is why. Watch the",
                "# first run.",
                "#",
                "# The upgrade (CONCEPT.md D-201). A person starts it, from Pipelines > Run pipeline or with",
                "# 'az pipelines run'. It installs the sdlc release asked for and runs 'sdlc upgrade apply' as it, which",
                $"# moves the solution, checks the move as the pull request will, and opens one into {integrationBranch}.",
                "# The pull-request check reads it as an upgrade (D-133). It merges nothing, and it never runs on a",
                "# schedule, because a release is taken on purpose.",
                "#",
                "# What a person sets up, once:",
                "#   - Pipelines > New pipeline > this file.",
                "#   - The project's build service may contribute, create branches and contribute to pull",
                "#     requests on this repository (Project settings > Repositories > Security).",
                "#   - SDLC_AGENT_POOL, a variable on this pipeline: the agent pool it runs on, or 'Azure Pipelines'",
                "#     for a Microsoft-hosted agent. A pool of your own also lets this pipeline use it, once, under",
                "#     the pool's Security > Pipeline permissions. Unset, the run fails: 'Could not find a pool with",
                "#     name $(SDLC_AGENT_POOL)'. A variable rather than this file, because the upgrade check compares",
                "#     the file exactly, and every run rewrites it.",
                "# Started by a person, never on a schedule (D-201).",
                "trigger: none",
                "pr: none",
                string.Empty,
                "parameters:",
                "  - name: release",
                "    displayName: The sdlc release to move to. Empty takes the newest on nuget.org.",
                "    type: string",
                "    default: ' '",
                string.Empty,
                "jobs:",
                "  - job: upgrade",
                "    timeoutInMinutes: 30",
                "    # The pool's name is resolved when the run is queued; vmImage is read only by a Microsoft-hosted pool.",
                "    pool:",
                "      name: $(SDLC_AGENT_POOL)",
                "      vmImage: ubuntu-latest",
                "    steps:",
                "      - checkout: self",
                "        fetchDepth: 0",
                "        persistCredentials: true",
                string.Empty,
                .. Templates.SelectSdkAzure("      "),
                string.Empty,
                "      - script: |",
                $"          git switch {integrationBranch}",
                "          git config user.name 'sdlc upgrade'",
                "          git config user.email 'sdlc-upgrade@localhost'",
                .. Move("          "),
                "        displayName: move to the release and open the pull request",
                "        env:",
                "          RELEASE: ${{ parameters.release }}",
                "          AZURE_DEVOPS_EXT_PAT: $(System.AccessToken)",
                string.Empty,
            ]),

        _ => throw new ArgumentOutOfRangeException(nameof(platform), "A local solution has nowhere to run a pipeline."),
    };

    /// <summary>
    /// The lines that settle the release, install it, and hand the move to it (D-213).
    /// </summary>
    /// <remarks>
    /// <para><strong>As few lines as can do it, because they stay.</strong> This file is the one part of a move the
    /// older release writes, so everything that could be wrong lives in the release being moved to:
    /// <c>sdlc upgrade apply --push</c> runs adoption and the factory's merge, commits what an upgrade may carry, holds
    /// it to the upgrade check, and pushes the branch and opens the pull request only when the check holds. A fix to any
    /// of that reaches a solution on its next move rather than after a person moves it by hand.</para>
    /// <para><strong>The release is named or the newest on nuget.org</strong>, never a preview. Azure DevOps will
    /// not take an empty string as a parameter's default, so the default is a space, trimmed here. A solution already
    /// on it stops cleanly, as success.</para>
    /// <para><strong><c>dotnet sdlc</c> rather than a global install</strong>: an agent's image may carry an older
    /// global binary in a lower layer, which <c>dotnet tool uninstall</c> cannot remove there ("Invalid cross-device
    /// link"), and a hosted agent carries none.</para>
    /// </remarks>
    private static IEnumerable<string> Move(string indent)
    {
        yield return indent + "# Stop at the first command that fails, on either platform.";
        yield return indent + "set -e";
        yield return indent + "release=\"$(printf '%s' \"${RELEASE:-}\" | tr -d '[:space:]')\"";
        yield return indent + "if [ -z \"$release\" ]; then release=\"$(curl -fsSL https://api.nuget.org/v3-flatcontainer/mofoncode.sdlc.cli/index.json | jq -r '[.versions[] | select(contains(\"-\") | not)] | last // empty')\"; fi";
        yield return indent + "if [ -z \"$release\" ]; then echo 'No release was named, and nuget.org listed none.' >&2; exit 1; fi";
        yield return indent + $"[ -f {PinnedRelease.ManifestPath} ] || {{ echo \"This solution pins no sdlc release. Run 'sdlc init' on the integration branch and commit {PinnedRelease.ManifestPath}.\" >&2; exit 1; }}";
        yield return indent + "pinned=\"$(dotnet tool list --local | awk 'tolower($1) == \"mofoncode.sdlc.cli\" { print $2 }')\"";
        yield return indent + "if [ \"$pinned\" = \"$release\" ]; then echo \"This solution pins sdlc $release already. Nothing to upgrade.\"; exit 0; fi";
        yield return indent + $"dotnet tool update --local {PinnedRelease.PackageId} --version \"$release\"";
        yield return indent + "# Everything after the install is the new release's: it moves the solution, checks the move, and opens the pull request.";
        yield return indent + "dotnet sdlc upgrade apply --push";
    }
}

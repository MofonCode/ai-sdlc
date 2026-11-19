using Sdlc.Core.Chain;
using Sdlc.Core.Factory;

namespace Sdlc.Core.Init;

/// <summary>
/// The upgrade pipeline: a run a person starts that moves the solution to another sdlc release and opens the pull
/// request for it (D-201).
/// </summary>
/// <remarks>
/// <para><strong>Commands only, no agent.</strong> Moving the pin, running adoption as the new release and
/// rewriting the factory's pipeline are all the binary's, so the run needs no credential for a model and decides
/// nothing. The pull request is the decision, and a person makes it.</para>
/// <para><strong>Never on a schedule.</strong> Taking a release is done on purpose, as starting the factory is
/// (D-143). Nothing here watches nuget.org.</para>
/// <para><strong>It moves the repository and nothing else.</strong> An agent that runs the solution's pipelines
/// needs no rebuild for a new release: every run restores the release its branch pins. What lives in an agent's
/// image is the solution's, and the <c>agent-image</c> guidance says how to move it.</para>
/// <para><strong>It stages only what an upgrade may carry</strong>, the paths <see cref="VerifyUpgrade"/> reads as
/// one, so the pull request it opens is one the check accepts or names the file it refuses.</para>
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
        Platform.AzureDevOps => "azure-pipelines.upgrade.yml",
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
                "# 'gh workflow run sdlc-upgrade.yml'. It moves the sdlc release this solution pins, runs adoption as",
                $"# that release, rewrites the factory's pipeline when there is one, and opens a pull request into {integrationBranch}.",
                "# The pull-request check reads it as an upgrade (D-133). It merges nothing, and it never runs on a",
                "# schedule, because a release is taken on purpose.",
                "#",
                "# Set up once, in Settings > Secrets and variables > Actions:",
                "#   A GitHub App installed on this repository that may write contents and pull requests:",
                "#     SDLC_UPGRADE_APP_ID   a variable, the app's id",
                "#     SDLC_UPGRADE_APP_KEY  a secret, its private key",
                "#   Not GITHUB_TOKEN: a pull request opened with it starts no workflow, so the pull-request",
                "#   check would never run on it.",
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
                "    runs-on: ubuntu-latest",
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
                .. Move("          "),
                "          git config user.name 'sdlc upgrade'",
                "          git config user.email 'sdlc-upgrade@users.noreply.github.com'",
                .. Commit("          "),
                $"          if url=\"$(gh pr view \"$branch\" --json url --jq .url 2>/dev/null)\"; then echo \"The pull request is open already, and now carries sdlc $release: $url\"; exit 0; fi",
                $"          gh pr create --base {integrationBranch} --head \"$branch\" --title \"$title\" --body \"$body\"",
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
                "# 'az pipelines run'. It moves the sdlc release this solution pins, runs adoption as that release,",
                $"# rewrites the factory's pipeline when there is one, and opens a pull request into {integrationBranch}.",
                "# The pull-request check reads it as an upgrade (D-133). It merges nothing, and it never runs on a",
                "# schedule, because a release is taken on purpose.",
                "#",
                "# What a person sets up, once:",
                "#   - Pipelines > New pipeline > this file.",
                "#   - The project's build service may contribute, create branches and contribute to pull",
                "#     requests on this repository (Project settings > Repositories > Security).",
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
                "    pool:",
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
                .. Move("          "),
                "          git config user.name 'sdlc upgrade'",
                "          git config user.email 'sdlc-upgrade@localhost'",
                .. Commit("          "),
                "          az extension add --name azure-devops --only-show-errors",
                "          az devops configure --defaults organization=\"$(System.CollectionUri)\" project=\"$(System.TeamProject)\"",
                "          open=\"$(az repos pr list --repository \"$(Build.Repository.Name)\" --source-branch \"$branch\" --status active --query '[0].pullRequestId' -o tsv)\"",
                "          if [ -n \"$open\" ]; then echo \"Pull request $open is open already, and now carries sdlc $release.\"; exit 0; fi",
                $"          az repos pr create --repository \"$(Build.Repository.Name)\" --source-branch \"$branch\" --target-branch {integrationBranch} --title \"$title\" --description \"$body\" --output none",
                "        displayName: move to the release and open the pull request",
                "        env:",
                "          RELEASE: ${{ parameters.release }}",
                "          AZURE_DEVOPS_EXT_PAT: $(System.AccessToken)",
                string.Empty,
            ]),

        _ => throw new ArgumentOutOfRangeException(nameof(platform), "A local solution has nowhere to run a pipeline."),
    };

    /// <summary>
    /// The lines that settle the release, move the pin, and run what the release writes as that release.
    /// </summary>
    /// <remarks>
    /// <para><strong>The release is named or the newest on nuget.org</strong>, never a preview. Azure DevOps will
    /// not take an empty string as a parameter's default, so the default is a space, trimmed here.</para>
    /// <para><strong>A solution already on the release stops cleanly</strong>, as success: there is nothing to
    /// propose.</para>
    /// <para><strong>Adoption runs as the new release.</strong> The global binary is the release itself, and the
    /// manifest pins it, so <c>sdlc init</c> writes what that release writes; the pull-request check compares
    /// against the same release.</para>
    /// </remarks>
    private static IEnumerable<string> Move(string indent)
    {
        yield return indent + "# Stop at the first command that fails, on either platform, so nothing half-moved is committed.";
        yield return indent + "set -e";
        yield return indent + "release=\"$(printf '%s' \"${RELEASE:-}\" | tr -d '[:space:]')\"";
        yield return indent + "if [ -z \"$release\" ]; then release=\"$(curl -fsSL https://api.nuget.org/v3-flatcontainer/mofoncode.sdlc.cli/index.json | jq -r '[.versions[] | select(contains(\"-\") | not)] | last // empty')\"; fi";
        yield return indent + "if [ -z \"$release\" ]; then echo 'No release was named, and nuget.org listed none.' >&2; exit 1; fi";
        yield return indent + $"[ -f {PinnedRelease.ManifestPath} ] || {{ echo \"This solution pins no sdlc release. Run 'sdlc init' on the integration branch and commit {PinnedRelease.ManifestPath}.\" >&2; exit 1; }}";
        yield return indent + "pinned=\"$(dotnet tool list --local | awk 'tolower($1) == \"mofoncode.sdlc.cli\" { print $2 }')\"";
        yield return indent + "if [ \"$pinned\" = \"$release\" ]; then echo \"This solution pins sdlc $release already. Nothing to upgrade.\"; exit 0; fi";
        yield return indent + $"dotnet tool update --local {PinnedRelease.PackageId} --version \"$release\"";
        yield return indent + $"dotnet tool uninstall --global {PinnedRelease.PackageId} >/dev/null 2>&1 || true";
        yield return indent + $"dotnet tool install --global {PinnedRelease.PackageId} --version \"$release\"";
        yield return indent + "sdlc init";
        yield return indent + "# The factory's pipeline is rewritten whole. A step the team added to it shows in the diff, to put back.";
        yield return indent + $"if [ -f {Slashed(FactoryPipeline.PathOf(Platform.GitHub))} ] || [ -f {Slashed(FactoryPipeline.PathOf(Platform.AzureDevOps))} ]; then sdlc factory; fi";
    }

    /// <summary>
    /// The lines that commit what an upgrade may carry on the upgrade's own branch and push it, setting the pull
    /// request's branch, title and body.
    /// </summary>
    /// <remarks>
    /// A run asked again for the same release pushes over its own branch, so an open pull request takes the
    /// refreshed files rather than a second one opening beside it.
    /// </remarks>
    private static IEnumerable<string> Commit(string indent)
    {
        yield return indent + "branch=\"upgrade/sdlc-$release\"";
        yield return indent + "title=\"Move to sdlc $release\"";
        yield return indent + "body=\"Moves this solution from sdlc $pinned to $release: the pin, and what adoption and 'sdlc factory' wrote as $release. The pull-request check reads it as an upgrade. What the release asks of a solution: https://github.com/MofonCode/ai-sdlc/blob/main/CHANGELOG.md. A factory pipeline is rewritten whole, so put back any step the team added before merging. Open cycles keep the release their branch pins.\"";
        yield return indent + "git switch -c \"$branch\"";
        yield return indent + "for path in " + string.Join(' ', VerifyUpgrade.Paths) + "; do if [ -e \"$path\" ]; then git add -- \"$path\"; fi; done";
        yield return indent + "if left=\"$(git status --porcelain --untracked-files=all | grep -v '^[AM] ')\"; then echo \"Left out, because an upgrade may not carry it:\"; echo \"$left\"; fi";
        yield return indent + "if git diff --cached --quiet; then echo \"sdlc $release writes nothing new here.\"; exit 0; fi";
        yield return indent + "git commit -m \"$title\"";
        yield return indent + "git push --force origin \"$branch\"";
    }

    private static string Slashed(string path) => path.Replace('\\', '/');
}

using Sdlc.Core.Init;

namespace Sdlc.Core.Factory;

/// <summary>
/// The factory's pipeline: a schedule that runs <c>batch-build --pr --unattended</c> against the integration
/// branch, one run at a time, inside a time limit, and publishes the sitrep (D-84).
/// </summary>
/// <remarks>
/// <para><strong>It automates four things and nothing else</strong>: finding approved plans, building,
/// verifying, opening pull requests. It merges nothing and pushes nothing but the cycles' own branches; a
/// person reviews and merges every pull request it opens.</para>
/// <para><strong>The gate is proved before it is trusted.</strong> The run asks the headless agent to write a
/// file only a person writes and fails if the file appears, because enforcement lives below the agent (§2.1)
/// and a host whose hooks did not load would build all night with nothing refusing anything.</para>
/// <para><strong>Neither template has run against a real server</strong>, and each says so at the top, on
/// the terms D-32 set for the first Azure DevOps pipeline: a pipeline nobody has run looks exactly like a
/// working one.</para>
/// </remarks>
public static class FactoryPipeline
{
    /// <summary>Where the factory's pipeline lives in a solution.</summary>
    /// <param name="platform">The platform.</param>
    /// <returns>The path, relative to the root.</returns>
    public static string PathOf(Platform platform) => platform switch
    {
        Platform.GitHub => Path.Combine(".github", "workflows", "sdlc-factory.yml"),
        Platform.AzureDevOps => "azure-pipelines.factory.yml",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), "A local solution has nowhere to run a factory."),
    };

    /// <summary>The factory's pipeline for a platform.</summary>
    /// <param name="platform">The platform. Not local.</param>
    /// <param name="integrationBranch">The branch the factory builds from and targets.</param>
    /// <param name="version">The pinned version of the gate to install.</param>
    /// <returns>The file's contents.</returns>
    public static string Text(Platform platform, string integrationBranch, string version) => platform switch
    {
        Platform.GitHub => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run. It is written from the documentation; if the first run fails",
                "# on syntax or on an action's version, that is why. Run it first against one throwaway approved",
                "# cycle, and watch it.",
                "#",
                "# The factory (CONCEPT.md section 10.5, D-84). Every night it finds the cycles approved on",
                $"# {integrationBranch} with no verify, builds and verifies each on its own branch, and opens a pull",
                "# request per cycle -- a draft for one that did not pass. It merges nothing: a person reviews and",
                "# merges every pull request, one at a time, starting from the sitrep this run publishes.",
                "#",
                "# Secrets, set in Settings > Secrets and variables > Actions:",
                "#   One way for the agent to sign in -- either, and the API key wins when both are set:",
                "#     ANTHROPIC_API_KEY        billed per token to an Anthropic Console account",
                "#     CLAUDE_CODE_OAUTH_TOKEN  from 'claude setup-token', billed to a Claude subscription. Check",
                "#                              its terms and limits cover a team's nightly run",
                "#   SDLC_FACTORY_TOKEN a token that may push branches and open pull requests. Not GITHUB_TOKEN:",
                "#                      a pull request opened with it starts no workflow, so the pull-request",
                "#                      check would never run on anything the factory opened.",
                "name: factory",
                string.Empty,
                "on:",
                "  schedule:",
                "    - cron: '0 2 * * *'",
                "  workflow_dispatch:",
                string.Empty,
                "# One run at a time. A second waits rather than picking the same cycles.",
                "concurrency:",
                "  group: sdlc-factory",
                "  cancel-in-progress: false",
                string.Empty,
                "jobs:",
                "  factory:",
                "    runs-on: ubuntu-latest",
                "    # The spend ceiling. Remediation has no pass limit (D-66); this stops a run, writes no outcome,",
                "    # and leaves the gate's progress rule as it is.",
                "    timeout-minutes: 360",
                "    steps:",
                "      - uses: actions/checkout@v4",
                "        with:",
                $"          ref: {integrationBranch}",
                "          fetch-depth: 0",
                "          token: ${{ secrets.SDLC_FACTORY_TOKEN }}",
                string.Empty,
                "      - uses: actions/setup-dotnet@v4",
                "        with:",
                "          global-json-file: global.json",
                string.Empty,
                "      - name: install the gate and the agent",
                "        run: |",
                $"          dotnet tool install --global MofonCode.Sdlc.Cli --version {version}",
                "          npm install --global @anthropic-ai/claude-code",
                "          claude plugin marketplace add MofonCode/ai-sdlc",
                "          claude plugin install sdlc@ai-sdlc",
                "          git config user.name 'sdlc factory'",
                "          git config user.email 'sdlc-factory@users.noreply.github.com'",
                string.Empty,
                "      # Enforcement lives below the agent. Prove it is there before the agent is trusted with",
                "      # anything: ask it to write a file only a person writes, and stop if the file appears.",
                "      - name: prove the agent signs in and the gate refuses",
                "        env:",
                "          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}",
                "          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}",
                "        run: |",
                .. Credential("          ", check: true),
                "          claude -p 'Create the file .sdlc/approval.md containing the single word smoke. Do nothing else.' --permission-mode bypassPermissions || true",
                "          if [ -e .sdlc/approval.md ]; then echo 'The gate did not refuse a protected write, so nothing would refuse anything tonight.'; exit 1; fi",
                string.Empty,
                "      # Operate: production's errors since the last run, filed as leads on the tracker. Nothing",
                "      # to read when solution.md declares no telemetry store, which it says and moves on.",
                "      # Reading Application Insights needs the runner signed in to Azure: add an azure/login step",
                "      # before this one when a store is declared.",
                "      - name: operate",
                "        env:",
                "          GH_TOKEN: ${{ secrets.SDLC_FACTORY_TOKEN }}",
                "        run: sdlc operate --since 24h",
                string.Empty,
                "      - name: build the queue",
                "        env:",
                "          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}",
                "          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}",
                "          GH_TOKEN: ${{ secrets.SDLC_FACTORY_TOKEN }}",
                "        run: |",
                .. Credential("          ", check: false),
                "          claude -p '/sdlc:batch-build --pr --unattended' --permission-mode bypassPermissions",
                string.Empty,
                "      # The morning starts from this page, whatever the run above did.",
                "      - name: the sitrep",
                "        if: always()",
                "        run: |",
                "          git fetch origin",
                "          sdlc sitrep | tee sitrep.md >> \"$GITHUB_STEP_SUMMARY\"",
                string.Empty,
            ]),

        Platform.AzureDevOps => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run against a real Azure DevOps server. It is written from the",
                "# documentation; if the first run fails on syntax or on a task's version, that is why. Run it first",
                "# against one throwaway approved cycle, and watch it.",
                "#",
                "# The factory (CONCEPT.md section 10.5, D-84). Every night it finds the cycles approved on",
                $"# {integrationBranch} with no verify, builds and verifies each on its own branch, and opens a pull",
                "# request per cycle -- a draft for one that did not pass. It merges nothing: a person reviews and",
                "# merges every pull request, one at a time, starting from the sitrep this run publishes.",
                "#",
                "# What a person sets up, once:",
                "#   - Pipelines > New pipeline > this file.",
                "#   - One secret variable for the agent to sign in -- either, and the API key wins when both",
                "#     are set: ANTHROPIC_API_KEY, billed per token to an Anthropic Console account, or",
                "#     CLAUDE_CODE_OAUTH_TOKEN, from 'claude setup-token' and billed to a Claude subscription.",
                "#     Check the subscription's terms and limits cover a team's nightly run.",
                "#   - The project's build service may contribute, create branches and contribute to pull",
                "#     requests on this repository (Project settings > Repositories > Security).",
                "#   - Pipelines > Environments > sdlc-factory > Approvals and checks > Exclusive lock. Without it",
                "#     two runs could pick the same cycles; with it a second waits.",
                "trigger: none",
                "pr: none",
                string.Empty,
                "schedules:",
                "  - cron: '0 2 * * *'",
                "    displayName: nightly",
                "    branches:",
                $"      include: [{integrationBranch}]",
                "    always: true",
                string.Empty,
                "lockBehavior: sequential",
                string.Empty,
                "jobs:",
                "  - deployment: factory",
                "    environment: sdlc-factory",
                "    # The spend ceiling. Remediation has no pass limit (D-66); this stops a run, writes no",
                "    # outcome, and leaves the gate's progress rule as it is.",
                "    timeoutInMinutes: 360",
                "    pool:",
                "      vmImage: ubuntu-latest",
                "    strategy:",
                "      runOnce:",
                "        deploy:",
                "          steps:",
                "            - checkout: self",
                "              fetchDepth: 0",
                "              persistCredentials: true",
                string.Empty,
                "            - task: UseDotNet@2",
                "              inputs:",
                "                useGlobalJson: true",
                string.Empty,
                "            # Signs the run in to this organization's Azure Artifacts feeds, which is where the gate",
                "            # comes from until it is on a public feed: name the feed in nuget.config.",
                "            - task: NuGetAuthenticate@1",
                string.Empty,
                "            - script: |",
                $"                git switch {integrationBranch}",
                $"                dotnet tool install --global MofonCode.Sdlc.Cli --version {version}",
                "                az extension add --name azure-devops --only-show-errors",
                "                az devops configure --defaults organization=\"$(System.CollectionUri)\" project=\"$(System.TeamProject)\"",
                "                npm install --global @anthropic-ai/claude-code",
                "                claude plugin marketplace add MofonCode/ai-sdlc",
                "                claude plugin install sdlc@ai-sdlc",
                "                git config user.name 'sdlc factory'",
                "                git config user.email 'sdlc-factory@localhost'",
                "              displayName: install the gate and the agent",
                string.Empty,
                "            # Enforcement lives below the agent. Prove it is there before the agent is trusted",
                "            # with anything: ask it to write a file only a person writes, and stop if it appears.",
                "            - script: |",
                .. Credential("                ", check: true),
                "                claude -p 'Create the file .sdlc/approval.md containing the single word smoke. Do nothing else.' --permission-mode bypassPermissions || true",
                "                if [ -e .sdlc/approval.md ]; then echo 'The gate did not refuse a protected write, so nothing would refuse anything tonight.'; exit 1; fi",
                "              displayName: prove the agent signs in and the gate refuses",
                "              env:",
                "                ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)",
                "                CLAUDE_CODE_OAUTH_TOKEN: $(CLAUDE_CODE_OAUTH_TOKEN)",
                string.Empty,
                "            # Operate: production's errors since the last run, filed as leads on the tracker.",
                "            # Nothing to read when solution.md declares no telemetry store; it says so and moves on.",
                "            # Reading Application Insights needs Azure credentials: when a store is declared, run",
                "            # this as an AzureCLI@2 task with a service connection that may read it.",
                "            - script: sdlc operate --since 24h",
                "              displayName: operate",
                "              env:",
                "                AZURE_DEVOPS_EXT_PAT: $(System.AccessToken)",
                string.Empty,
                "            - script: |",
                .. Credential("                ", check: false),
                "                claude -p '/sdlc:batch-build --pr --unattended' --permission-mode bypassPermissions",
                "              displayName: build the queue",
                "              env:",
                "                ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)",
                "                CLAUDE_CODE_OAUTH_TOKEN: $(CLAUDE_CODE_OAUTH_TOKEN)",
                "                AZURE_DEVOPS_EXT_PAT: $(System.AccessToken)",
                string.Empty,
                "            # The morning starts from this page, whatever the run above did.",
                "            - script: |",
                "                git fetch origin",
                "                sdlc sitrep > \"$(Agent.TempDirectory)/sitrep.md\"",
                "                echo \"##vso[task.uploadsummary]$(Agent.TempDirectory)/sitrep.md\"",
                "              displayName: the sitrep",
                "              condition: always()",
                string.Empty,
            ]),

        _ => throw new ArgumentOutOfRangeException(nameof(platform), "A local solution has nowhere to run a factory."),
    };

    /// <summary>
    /// The lines that leave the agent exactly one credential, and on the first step prove it works.
    /// </summary>
    /// <remarks>
    /// <para><strong>Either credential, and the choice is the pipeline's rather than the agent's.</strong> A
    /// Console API key and a subscription token from <c>claude setup-token</c> both sign the CLI in; when both
    /// secrets are set the API key is used and the token is unset, so which account pays does not depend on
    /// the agent's own precedence.</para>
    /// <para><strong>An unset secret is not always empty.</strong> GitHub passes an empty string, and Azure
    /// DevOps leaves a mapped variable that does not exist as its own literal text, <c>$(NAME)</c>. Both
    /// count as unset here. The pattern is written <c>\$\(</c> so Azure DevOps does not read it as a
    /// variable to expand.</para>
    /// <para><strong>The sign-in is proved before the gate.</strong> The gate check passes when the agent
    /// writes nothing, and an agent that cannot sign in writes nothing, so without this a broken credential
    /// would prove the gate and then fail every build.</para>
    /// </remarks>
    private static IEnumerable<string> Credential(string indent, bool check)
    {
        yield return indent + "# One credential: the API key when it is set, otherwise the subscription token.";
        yield return indent + "case \"${ANTHROPIC_API_KEY:-}\" in ''|\\$\\(*) unset ANTHROPIC_API_KEY ;; *) unset CLAUDE_CODE_OAUTH_TOKEN ;; esac";
        yield return indent + "case \"${CLAUDE_CODE_OAUTH_TOKEN:-}\" in ''|\\$\\(*) unset CLAUDE_CODE_OAUTH_TOKEN ;; esac";

        if (check)
        {
            yield return indent + "if [ -z \"${ANTHROPIC_API_KEY:-}${CLAUDE_CODE_OAUTH_TOKEN:-}\" ]; then echo 'Set ANTHROPIC_API_KEY or CLAUDE_CODE_OAUTH_TOKEN as a secret: the agent has no way to sign in.'; exit 1; fi";
            yield return indent + "claude -p 'Reply with the single word ready.' | grep -qi ready || { echo 'The agent did not answer, so its credential does not work. Nothing was built.'; exit 1; }";
        }
    }
}

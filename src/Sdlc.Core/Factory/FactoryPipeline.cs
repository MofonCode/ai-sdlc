using System.Globalization;
using Sdlc.Core.Init;

namespace Sdlc.Core.Factory;

/// <summary>
/// The factory's pipeline: a run a person starts, of <c>batch-build --pr --unattended</c> against the integration
/// branch, one run at a time, inside a time limit, and publishes the sitrep (D-84).
/// </summary>
/// <remarks>
/// <para><strong>It automates four things and nothing else</strong>: finding approved plans, building,
/// verifying, opening pull requests. It merges nothing and pushes nothing but the cycles' own branches; a
/// person reviews and merges every pull request it opens.</para>
/// <para><strong>The hooks are proved before the agent is trusted.</strong> The run asks the headless agent to write a
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
    /// <returns>The file's contents.</returns>
    public static string Text(Platform platform, string integrationBranch) => platform switch
    {
        Platform.GitHub => string.Join(
            Environment.NewLine,
            [
                "# NOTE: this pipeline has never run. It is written from the documentation; if the first run fails",
                "# on syntax or on an action's version, that is why. Run it first against one throwaway approved",
                "# cycle, and watch it.",
                "#",
                "# The factory (CONCEPT.md section 10.5, D-84, D-143). A person starts it, from the Actions tab or with",
                $"# 'gh workflow run sdlc-factory.yml'. It finds the cycles approved on {integrationBranch} with no verify,",
                "# builds and verifies each on its own branch -- the one the most successors wait on first -- and opens a",
                "# pull request per cycle, a draft for one that did not pass. Name one cycle to build that one alone.",
                "# It merges nothing: a person reviews and merges every pull request, starting from the sitrep this run",
                "# publishes, and starts it again for what those merges made ready.",
                "#",
                "# Secrets, set in Settings > Secrets and variables > Actions:",
                "#   One way for the agent to sign in -- either, and the API key wins when both are set:",
                "#     ANTHROPIC_API_KEY        billed per token to an Anthropic Console account",
                "#     CLAUDE_CODE_OAUTH_TOKEN  from 'claude setup-token', billed to a Claude subscription. Check",
                "#                              its terms and limits cover a team's runs",
                "#   SDLC_FACTORY_TOKEN a token that may push branches and open pull requests. Not GITHUB_TOKEN:",
                "#                      a pull request opened with it starts no workflow, so the pull-request",
                "#                      check would never run on anything the factory opened.",
                "name: factory",
                string.Empty,
                "# Started by a person, never on a schedule: starting it is the decision to spend (D-143).",
                "on:",
                "  workflow_dispatch:",
                "    inputs:",
                "      cycle:",
                "        description: One cycle id to build alone. Empty builds every queued cycle.",
                "        required: false",
                "        default: ''",
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
                "      - uses: actions/checkout@v5",
                "        with:",
                $"          ref: {integrationBranch}",
                "          fetch-depth: 0",
                "          token: ${{ secrets.SDLC_FACTORY_TOKEN }}",
                string.Empty,
                .. Templates.SelectSdkGitHub("      "),
                string.Empty,
                "      # Each is used when the agent already carries it -- a self-hosted agent may -- and",
                "      # installed when it does not. sdlc is the release the integration branch pins either way.",
                "      - name: install sdlc and the agent",
                "        run: |",
                .. Templates.InstallSdlc("          "),
                .. Agent("          "),
                "          git config user.name 'sdlc factory'",
                "          git config user.email 'sdlc-factory@users.noreply.github.com'",
                string.Empty,
                "      # Enforcement lives below the agent. Prove it is there before the agent is trusted with",
                "      # anything: ask it to write a file only a person writes, and stop if the file appears.",
                "      - name: prove the agent signs in and the hooks refuse",
                "        env:",
                "          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}",
                "          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}",
                "        run: |",
                .. Credential("          ", check: true),
                "          sdlc claude -p 'Create the file .sdlc/approval.md containing the single word smoke. Do nothing else.' --permission-mode bypassPermissions < /dev/null || true",
                "          if [ -e .sdlc/approval.md ]; then echo 'The hooks did not refuse a protected write, so nothing would refuse anything in this run.'; exit 1; fi",
                string.Empty,
                "      - name: build the queue",
                "        env:",
                "          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}",
                "          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}",
                "          GH_TOKEN: ${{ secrets.SDLC_FACTORY_TOKEN }}",
                "          CYCLE: ${{ inputs.cycle }}",
                "        run: |",
                .. Credential("          ", check: false),
                .. Asked("          "),
                "          sdlc claude -p '/sdlc:batch-build '\"$CYCLE\"' --pr --unattended' --permission-mode bypassPermissions < /dev/null",
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
                "# The factory (CONCEPT.md section 10.5, D-84, D-143). A person starts it, from Pipelines > Run pipeline",
                $"# or with 'az pipelines run'. It finds the cycles approved on {integrationBranch} with no verify, builds",
                "# and verifies each on its own branch -- the one the most successors wait on first -- and opens a pull",
                "# request per cycle, a draft for one that did not pass. Name one cycle to build that one alone. It",
                "# merges nothing: a person reviews and merges every pull request, starting from the sitrep this run",
                "# publishes, and starts it again for what those merges made ready.",
                "#",
                "# What a person sets up, once:",
                "#   - Pipelines > New pipeline > this file.",
                "#   - One secret variable for the agent to sign in -- either, and the API key wins when both",
                "#     are set: ANTHROPIC_API_KEY, billed per token to an Anthropic Console account, or",
                "#     CLAUDE_CODE_OAUTH_TOKEN, from 'claude setup-token' and billed to a Claude subscription.",
                "#     Check the subscription's terms and limits cover a team's runs.",
                "#   - The project's build service may contribute, create branches and contribute to pull",
                "#     requests on this repository (Project settings > Repositories > Security).",
                "#   - Pipelines > Environments > sdlc-factory > Approvals and checks > Exclusive lock. Without it",
                "#     two runs could pick the same cycles; with it a second waits.",
                "# Started by a person, never on a schedule: starting it is the decision to spend (D-143).",
                "trigger: none",
                "pr: none",
                string.Empty,
                "parameters:",
                "  - name: cycle",
                "    displayName: One cycle id to build alone. Empty builds every queued cycle.",
                "    type: string",
                "    default: ' '",
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
                .. Templates.SelectSdkAzure("            "),
                string.Empty,
                "            - script: |",
                $"                git switch {integrationBranch}",
                .. Templates.InstallSdlc("                "),
                "                az extension add --name azure-devops --only-show-errors",
                "                az devops configure --defaults organization=\"$(System.CollectionUri)\" project=\"$(System.TeamProject)\"",
                .. Agent("                "),
                "                git config user.name 'sdlc factory'",
                "                git config user.email 'sdlc-factory@localhost'",
                "              displayName: install sdlc and the agent",
                string.Empty,
                "            # Each is used above when the agent already carries it, and installed when it does not.",
                "            # Enforcement lives below the agent. Prove it is there before the agent is trusted",
                "            # with anything: ask it to write a file only a person writes, and stop if it appears.",
                "            - script: |",
                .. Credential("                ", check: true),
                "                sdlc claude -p 'Create the file .sdlc/approval.md containing the single word smoke. Do nothing else.' --permission-mode bypassPermissions < /dev/null || true",
                "                if [ -e .sdlc/approval.md ]; then echo 'The hooks did not refuse a protected write, so nothing would refuse anything in this run.'; exit 1; fi",
                "              displayName: prove the agent signs in and the hooks refuse",
                "              env:",
                "                ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)",
                "                CLAUDE_CODE_OAUTH_TOKEN: $(CLAUDE_CODE_OAUTH_TOKEN)",
                string.Empty,
                "            - script: |",
                .. Credential("                ", check: false),
                .. Asked("                "),
                "                sdlc claude -p '/sdlc:batch-build '\"$CYCLE\"' --pr --unattended' --permission-mode bypassPermissions < /dev/null",
                "              displayName: build the queue",
                "              env:",
                "                ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)",
                "                CLAUDE_CODE_OAUTH_TOKEN: $(CLAUDE_CODE_OAUTH_TOKEN)",
                "                AZURE_DEVOPS_EXT_PAT: $(System.AccessToken)",
                "                CYCLE: ${{ parameters.cycle }}",
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
    /// The lines that read the one cycle a person asked the run to build, and stop the run before it builds
    /// anything when that cycle is not queued.
    /// </summary>
    /// <remarks>
    /// <para><strong>The binary reads it before the agent does.</strong> The input is text a person typed into a
    /// form, and it reaches the agent's prompt; <c>sdlc cycles --queued</c> refuses anything that is not a cycle id,
    /// and a cycle that is not queued, with why, so only an id it accepted is ever passed on.</para>
    /// <para><strong>Empty means every queued cycle.</strong> Azure DevOps will not take an empty string as a
    /// parameter's default, so the default is a space, trimmed here.</para>
    /// </remarks>
    private static IEnumerable<string> Asked(string indent)
    {
        yield return indent + "# The cycle asked for, if any: refused here, before anything is built, unless it is queued.";
        yield return indent + "CYCLE=\"$(printf '%s' \"${CYCLE:-}\" | tr -d '[:space:]')\"";
        yield return indent + "if [ -n \"$CYCLE\" ]; then sdlc cycles --queued \"$CYCLE\" || exit 1; fi";
    }

    /// <summary>
    /// The line that puts Claude Code on the agent, unless it already carries it.
    /// </summary>
    /// <remarks>
    /// No plugin is installed. Every session the factory starts is started by <c>sdlc claude</c>, which loads
    /// the skills packed beside the release the integration branch pins, over any plugin the agent carries
    /// (D-131). An installed plugin would be a second copy of the skills, from whatever release installed it.
    /// </remarks>
    private static IEnumerable<string> Agent(string indent)
    {
        yield return indent + "command -v claude >/dev/null 2>&1 || npm install --global @anthropic-ai/claude-code";
    }

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
    /// <para><strong>The sign-in is proved before the hooks.</strong> The hook check passes when the agent
    /// writes nothing, and an agent that cannot sign in writes nothing, so without this a broken credential
    /// would prove the hooks and then fail every build.</para>
    /// <para><strong>The credential is written into the agent's own settings as well.</strong> Claude Code does
    /// not hand its credential to the commands it runs, so the fresh reader <c>interrogate</c> starts (D-01)
    /// signed in with nothing on the first real run, and every verify went unread while the build passed. A
    /// <c>claude</c> reads the <c>env</c> of its user settings for itself, nested or not. The value is merged in,
    /// because the same file records the installed plugin, and the file is the agent user's alone; the job's
    /// container is discarded after the run.</para>
    /// <para><strong>The nested sign-in is proved too</strong>, by the agent starting a <c>claude</c> of its own
    /// and the shell, not the agent, reading what that one answered.</para>
    /// <para><strong>Every <c>claude -p</c> reads its standard input from nothing.</strong> Otherwise it waits
    /// three seconds for a prompt on standard input and warns, on every call.</para>
    /// </remarks>
    private static IEnumerable<string> Credential(string indent, bool check)
    {
        yield return indent + "# One credential: the API key when it is set, otherwise the subscription token.";
        yield return indent + "case \"${ANTHROPIC_API_KEY:-}\" in ''|\\$\\(*) unset ANTHROPIC_API_KEY ;; *) unset CLAUDE_CODE_OAUTH_TOKEN ;; esac";
        yield return indent + "case \"${CLAUDE_CODE_OAUTH_TOKEN:-}\" in ''|\\$\\(*) unset CLAUDE_CODE_OAUTH_TOKEN ;; esac";

        if (check)
        {
            yield return indent + "if [ -z \"${ANTHROPIC_API_KEY:-}${CLAUDE_CODE_OAUTH_TOKEN:-}\" ]; then echo 'Set ANTHROPIC_API_KEY or CLAUDE_CODE_OAUTH_TOKEN as a secret: the agent has no way to sign in.'; exit 1; fi";
        }

        yield return indent + "# And in the agent's own settings, since a claude the agent starts is handed no credential.";
        yield return indent + "if [ -n \"${ANTHROPIC_API_KEY:-}\" ]; then name=ANTHROPIC_API_KEY; else name=CLAUDE_CODE_OAUTH_TOKEN; fi";
        yield return indent + "mkdir -p ~/.claude && { [ -s ~/.claude/settings.json ] || echo '{}' > ~/.claude/settings.json; }";
        yield return indent + "jq --arg n \"$name\" --arg v \"${!name:-}\" '.env = ((.env // {}) + {($n): $v})' ~/.claude/settings.json > ~/.claude/settings.json.new && mv ~/.claude/settings.json.new ~/.claude/settings.json && chmod 600 ~/.claude/settings.json";

        if (check)
        {
            yield return indent + "claude -p 'Reply with the single word ready.' < /dev/null | grep -qi ready || { echo 'The agent did not answer, so its credential does not work. Nothing was built.'; exit 1; }";
            yield return indent + "nested=\"${TMPDIR:-/tmp}/sdlc-nested-sign-in.txt\"; rm -f \"$nested\"";
            yield return indent + "claude -p \"Run this bash command exactly as written and nothing else: claude -p 'Reply with the single word ready.' < /dev/null > $nested 2>&1\" --permission-mode bypassPermissions < /dev/null > /dev/null || true";
            yield return indent + "grep -qi ready \"$nested\" 2>/dev/null || { echo 'A claude the agent starts could not sign in, so interrogation would have no fresh reader. Nothing was built.'; cat \"$nested\" 2>/dev/null; exit 1; }";
        }
    }
}

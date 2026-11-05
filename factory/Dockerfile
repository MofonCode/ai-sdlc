# syntax=docker/dockerfile:1
#
# The factory's agent: an Azure Pipelines agent with everything a factory run needs already inside it.
# factory/README.md says what is in it and why, and how to build, run and upgrade it.
#
# Built from the repository root, so the binary and the plugin both come from the commit being built:
#
#   docker build -f factory/Dockerfile -t ai-sdlc-factory-agent .
#
# Nothing below names the release version. The binary is packed from src/ and the plugin copied from
# skills/ and hooks/ in the same build, so the two cannot come from different releases (D-91) and there is
# no version here to go stale. Ask the image what it carries: docker run --rm --entrypoint sdlc <image> version

# The SDK band global.json pins (10.0.100, rollForward latestFeature). A test holds the two together.
ARG DOTNET_SDK=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK}-noble AS pack
WORKDIR /src
COPY . .
# A raw string literal carries its source file's line endings into the binary's output, and a Windows
# checkout's are CRLF, so a binary packed from one prints "sdlc 0.27.0\r" on Linux. The pipeline compares
# that line exactly, misses, and tries to reinstall from a feed. Pack from LF sources, as git stores them,
# and tell .editorconfig so, since it states crlf and the build enforces formatting.
RUN find src -name '*.cs' -exec sed -i 's/\r$//' {} + \
 && sed -i 's/^end_of_line = crlf/end_of_line = lf/' .editorconfig \
 && dotnet pack src/Sdlc.Cli/Sdlc.Cli.csproj --configuration Release -o /pack

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK}-noble

# The agent release the organization's other agents run. Claude Code's channel or an exact version.
ARG AZP_AGENT_VERSION=5.278.0
ARG CLAUDE_CODE_VERSION=stable

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl git gnupg jq unzip \
 && rm -rf /var/lib/apt/lists/*

# The Azure CLI: the pipeline opens pull requests and reads the board through it.
RUN curl -fsSL https://aka.ms/InstallAzureCLIDeb | bash \
 && rm -rf /var/lib/apt/lists/*

# The Docker CLI and nothing else of Docker. A solution's integration tests start their containers on the
# host's engine, through the socket the run command mounts.
RUN install -m 0755 -d /etc/apt/keyrings \
 && curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
      > /etc/apt/sources.list.d/docker.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends docker-ce-cli docker-buildx-plugin docker-compose-plugin \
 && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/bash agent

WORKDIR /azp
RUN curl -fsSL "https://download.agent.dev.azure.com/agent/${AZP_AGENT_VERSION}/vsts-agent-linux-x64-${AZP_AGENT_VERSION}.tar.gz" | tar -xz \
 && ./bin/installdependencies.sh \
 && rm -rf /var/lib/apt/lists/* \
 && chown -R agent:agent /azp

COPY --from=pack /pack /tmp/pack

# The plugin is these files and nothing else: the two manifests, the skills, the hooks.
COPY --chown=agent:agent .claude-plugin /opt/ai-sdlc/.claude-plugin
COPY --chown=agent:agent plugin.json LICENSE /opt/ai-sdlc/
COPY --chown=agent:agent skills /opt/ai-sdlc/skills
COPY --chown=agent:agent hooks /opt/ai-sdlc/hooks

# .gitattributes checks out native endings, so a Windows checkout hands this script CRLF. Strip it here
# rather than pin an ending against that file's decision.
COPY --chown=agent:agent --chmod=0755 factory/start.sh /azp/start.sh
RUN sed -i 's/\r$//' /azp/start.sh

USER agent
ENV PATH=/home/agent/.local/bin:/home/agent/.dotnet/tools:${PATH}

# The plugin's hooks run `sdlc hook`, so the binary has to be on the PATH wherever Claude runs.
RUN dotnet tool install --global MofonCode.Sdlc.Cli --add-source /tmp/pack --prerelease \
 && curl -fsSL https://claude.ai/install.sh | bash -s "${CLAUDE_CODE_VERSION}" \
 && claude plugin marketplace add /opt/ai-sdlc \
 && claude plugin install sdlc@ai-sdlc \
 && az extension add --name azure-devops --only-show-errors \
 && sdlc version | head -n 1 \
 && claude --version

LABEL org.opencontainers.image.title="ai-sdlc factory agent" \
      org.opencontainers.image.source="https://github.com/MofonCode/ai-sdlc" \
      org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["/azp/start.sh"]

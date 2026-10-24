# The factory bundle

What runs unattended or on a schedule, kept apart from what a person uses in a cycle (D-91). **Today it
holds one thing: the factory's agent image.** The rest that D-91 names — the bundle's own plugin manifest,
the pipeline templates `sdlc factory` writes, and Operate's scheduled run — still lives in the `sdlc`
plugin and the binary, and moves here when there is a second template to ship.

| File | What it is |
|---|---|
| [`Dockerfile`](Dockerfile) | the image: an Azure Pipelines agent with everything a factory run needs already inside |
| [`Dockerfile.dockerignore`](Dockerfile.dockerignore) | what the image build may read from the repository |
| [`start.sh`](start.sh) | the image's entrypoint: register, run one job, unregister |
| [`sdlc-factory-agent.service`](sdlc-factory-agent.service) | the systemd unit that runs it on a host, one fresh container per job |

**How to set a factory up with it** is [`../docs/guides/setting-up-the-factory.md`](../docs/guides/setting-up-the-factory.md)
step 2. This page says what the image is.

**It has never run a real factory job.** It builds, it carries the binary and the plugin at the release it
was built from, the pipeline's install steps find all three and skip, and it reaches Azure DevOps to
register. A night's build on it has not happened yet, so the first run is a watched one, as for the
pipeline itself.

---

## `Dockerfile`

**A Linux image that is an Azure Pipelines agent, carrying `sdlc`, Claude Code and the `sdlc` plugin, all
from the commit it is built from.** Built from the repository root:

```sh
docker build -f factory/Dockerfile -t ai-sdlc-factory-agent .
```

**Why an image rather than tools installed on an agent.** The pipeline installs whatever an agent lacks,
but `sdlc` is on no public feed and the plugin installs from the repository's default branch, so a bare
agent needs both put there by hand, and kept at the same release by hand. Built from one commit, the
binary and the plugin cannot disagree (D-91's reason for one binary), and a fresh container per job means
no run inherits what the last one left.

| Inside | Why |
|---|---|
| the .NET SDK, Ubuntu 24.04 | the band `global.json` pins, which the suite holds to the `DOTNET_SDK` argument. A solution's own pin is applied by the pipeline's `UseDotNet@2` step |
| `sdlc` | packed from `src/Sdlc.Cli` in the same build and installed as a global tool. The plugin's hooks run `sdlc hook`, so it has to be on the PATH wherever Claude runs |
| the `sdlc` plugin | the two manifests, `skills/` and `hooks/`, copied from the same build and installed from them |
| Claude Code | the native installer, at the `CLAUDE_CODE_VERSION` argument: `stable` unless set |
| the Azure CLI, with `azure-devops` | the pipeline opens pull requests and reads the board through it |
| the Docker CLI | a solution's integration tests start their containers on the host's engine, through the mounted socket |
| git, jq, curl, unzip | what the pipeline's scripts call |
| the Azure Pipelines agent | at the `AZP_AGENT_VERSION` argument, with its dependencies |

It runs as a user named `agent`, not root. **Nothing in it names the release version**: ask the image what
it carries with `docker run --rm --entrypoint sdlc ai-sdlc-factory-agent version`.

**The pack stage normalises line endings.** A raw string literal carries its source file's endings into
the binary's output, and a Windows checkout's are CRLF, so a binary packed from one prints
`sdlc 0.27.0\r` on Linux. The pipeline compares that line exactly, misses, and tries to reinstall from a
feed. The stage strips the CRs, and tells `.editorconfig` it has done so, so the image is the same whichever
machine built it.

## `Dockerfile.dockerignore`

**What the build may read.** Build output, test results and `.git` stay out, so the binary is packed from
source alone and a working copy's leftovers never reach the image.

## `start.sh`

**Registers with the organization, runs one job, unregisters and exits.** It reads:

| Variable | What |
|---|---|
| `AZP_URL` | the organization, as in `https://dev.azure.com/MofonCode` |
| `AZP_POOL` | the agent pool the factory's pipeline names under `pool:` |
| `AZP_TOKEN` | a personal access token with **Agent Pools (Read & manage)**, and nothing more |
| `AZP_AGENT_NAME` | optional; `sdlc-factory-<container hostname>` when unset |

A missing variable stops it with a sentence naming it. **The token registers the agent and the job is not
handed it**: the script unsets it before the agent starts. It is not a secret from the job, though: a job
holding the Docker socket can reach the host's engine and so the container's environment, which is why the
token is scoped to agent pools.

**The Claude token is not here.** It is a secret variable on the pipeline, given to each job.

## `sdlc-factory-agent.service`

**Runs the image on a Linux host as a service: started on boot, a fresh container for every job.** The
container exits after its one job, and systemd starts a new one ten seconds later.

On the host, from a checkout of this repository at the release to run:

```sh
docker build -f factory/Dockerfile -t ai-sdlc-factory-agent .
sudo install -m 600 /dev/null /etc/sdlc-factory-agent.env     # then put the four variables in it
sudo cp factory/sdlc-factory-agent.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now sdlc-factory-agent
```

The environment file is one `NAME=value` per line, readable by root only. The unit mounts the host's
Docker socket and adds the socket's group to the container, so the `agent` user can use it.

**What the socket costs.** Whoever can use a Docker socket controls that engine, so a job on this agent can
do anything the host's Docker can. Run it on a machine that exists for this, such as a build VM, and **not
on a person's own computer**, and never under a person's own account (D-96).

**Upgrading** is building the image again from the new release and restarting the service:
`sudo systemctl restart sdlc-factory-agent`. The image is built where it runs. There is no registry until a
second machine needs one.

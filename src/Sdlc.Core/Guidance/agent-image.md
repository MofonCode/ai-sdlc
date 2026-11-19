---
name: agent-image
files: "sdlc-factory.yml, azure-pipelines.factory.yml, agent-image/"
---

# Keeping an agent's image current

How the framework would keep a self-hosted agent's image current where the solution has not said otherwise.
**Where the solution already moves its agents another way, or a decision or a constraint says how, that stands.**

## What is not opinion

**A new sdlc release never needs an agent rebuilt.** Every run the generated pipelines start restores the release its
branch pins, and installs it when the agent carries another. The `sdlc` in an image only saves that install when it
matches. Taking a release is the upgrade pipeline's (D-201): it moves the repository, and the next run uses the new pin.

**No credential is written into a repository**, here or anywhere: a key, a token, a password. A pipeline's secret
store holds what a run needs, and what only one machine needs stays on that machine.

## What an image is rebuilt for

What lives in the image itself: Claude Code's version, browsers a test suite drives, system packages, an SDK. Those
move when the solution moves them, so the change that moves them is what asks for the rebuild.

- **The image's definition is in the repository**, under `agent-image/`: the Dockerfile and the scripts below. One
  that lives only on the agent's machine is lost with it, and nobody else can rebuild it.
- **Pin what the image carries to what the solution pins**, read from the same file — a browser version from the
  package that drives it — so the two cannot drift.
- **An image belongs to the agent, not to one solution.** Where one agent serves several solutions, its image carries
  what each needs; where two need what one image cannot hold, they need two agents.

## Push or pull

- **An agent a pipeline can reach without running on it** — a cloud pool, a registry image — is rebuilt by a job in a
  pipeline of the solution's own, started by a person.
- **An agent that runs the jobs is never rebuilt by one of its own jobs**: the restart kills the job doing it. It
  rebuilds itself when asked, as below.

## Asking an agent to rebuild itself

The pipeline asks; the machine acts; the pipeline checks.

1. **A folder shared between the host and the agent's container**, such as `/var/lib/agent-image/` mounted into it.
   It is how a job reaches the host, and it needs no credential.
2. **The request.** A job writes the wanted image definition — a commit, a tag, a version — into `request`, and ends.
3. **The watcher.** A file watcher on the host — a systemd path unit on Linux, a scheduled task on Windows — starts
   the update when `request` changes. Nothing runs on a timer and nothing asks the internet. The update:
   - **stops the agent's service first**, so no new job starts;
   - rebuilds the image, restarts the service, and writes `status`: done or failed, and what it built;
   - writes every step to a log named for the day, `yyyy-MM-dd.log`, and deletes every log but today's and
     yesterday's, so a failure just before midnight is still there in the morning.
4. **The check.** A job after the request reads `status` once and passes only when it matches the request; when it
   does not, it fails and attaches the day's log. Re-run that job alone to check again.
   - **The check never waits on the agent it checks.** A job that loops on the only agent keeps it busy, so the
     update never starts. Put a wait that needs no agent between the two — an agentless delay on Azure DevOps, an
     environment's wait timer on GitHub — and the check runs once, on the rebuilt agent.

**The scripts are PowerShell where the machine has it**, bash where it has only bash, and they install PowerShell
where it has neither. One script per machine, not one per shell.

**A person starts it.** The request job runs when somebody runs it, never on a schedule: a rebuild is done on purpose.

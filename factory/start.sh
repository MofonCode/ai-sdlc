#!/bin/bash
#
# The factory agent's entrypoint: register with the organization, run one job, exit.
# The service that runs the container starts a fresh one after each exit, so no job sees what the last
# one left behind. factory/README.md describes the environment it reads.
#
# The registration outlives the job, and is removed only when the container is stopped. Between two jobs
# the next container is still starting, and a pool with no agent registered fails a queued job at once
# rather than holding it: MarketSense's merge queued its integration build in that gap and lost it. An
# agent that is registered and offline makes the job wait, and the next container's --replace takes the
# registration over.
set -euo pipefail

: "${AZP_URL:?Set AZP_URL to the organization, as in https://dev.azure.com/MofonCode}"
: "${AZP_POOL:?Set AZP_POOL to the agent pool the factory pipeline names}"
: "${AZP_TOKEN:?Set AZP_TOKEN to a personal access token with Agent Pools (Read & manage)}"
AZP_AGENT_NAME="${AZP_AGENT_NAME:-sdlc-factory-$(hostname)}"

token="$AZP_TOKEN"
unset AZP_TOKEN

cd /azp

unregister() {
  ./config.sh remove --unattended --auth PAT --token "$token" >/dev/null 2>&1 || true
}
trap 'unregister; exit 130' INT
trap 'unregister; exit 143' TERM

./config.sh --unattended \
  --url "$AZP_URL" \
  --auth PAT --token "$token" \
  --pool "$AZP_POOL" \
  --agent "$AZP_AGENT_NAME" \
  --work /azp/_work \
  --replace \
  --acceptTeeEula

# The token registers the agent and nothing else, so the job is not handed it. With the Docker socket
# mounted a job can reach the host's engine, which is why the token is scoped to agent pools only.
env -u AZP_TOKEN ./run.sh --once &
wait $!

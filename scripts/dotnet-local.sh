#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
local_root="${project_root}/.local"

mkdir -p \
  "${local_root}/dotnet-cli" \
  "${local_root}/nuget/packages" \
  "${local_root}/nuget/http-cache" \
  "${local_root}/tmp" \
  "${local_root}/test-results" \
  "${local_root}/test-work"

export DOTNET_CLI_HOME="${local_root}/dotnet-cli"
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NUGET_PACKAGES="${local_root}/nuget/packages"
export NUGET_HTTP_CACHE_PATH="${local_root}/nuget/http-cache"
export TMPDIR="${local_root}/tmp"
export EMBY_EXTERNAL_PLAYER_TEST_ROOT="${local_root}/test-work"

exec dotnet "$@"

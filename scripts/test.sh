#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"

exec "${script_dir}/dotnet-local.sh" test \
  "${project_root}/Emby.ExternalPlayer.sln" \
  --configuration Release \
  --results-directory "${project_root}/.local/test-results" \
  "$@"


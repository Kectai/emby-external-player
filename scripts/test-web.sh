#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"

node "${project_root}/tests/web/external-player.test.mjs"
exec node "${project_root}/tests/web/config-localization.test.mjs"

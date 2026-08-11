#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"

: "${EMBY_INTEGRATION_BASE:?Set EMBY_INTEGRATION_BASE to an isolated, non-8096 loopback Emby instance.}"
: "${EMBY_INTEGRATION_PROGRAMDATA:?Set EMBY_INTEGRATION_PROGRAMDATA to the isolated project-local data directory.}"
: "${EMBY_INTEGRATION_DASHBOARD_APP:?Set EMBY_INTEGRATION_DASHBOARD_APP to the isolated dashboard-ui/app.js fixture.}"

case "${EMBY_INTEGRATION_PROGRAMDATA}" in
    "${project_root}"/.local/*) ;;
    *)
        echo "Refusing to test: program data must be inside ${project_root}/.local." >&2
        exit 2
        ;;
esac

case "${EMBY_INTEGRATION_DASHBOARD_APP}" in
    "${project_root}"/.local/*) ;;
    *)
        echo "Refusing to test: dashboard app.js must be inside ${project_root}/.local." >&2
        exit 2
        ;;
esac

exec node "${project_root}/tests/integration/isolated-emby.test.mjs"

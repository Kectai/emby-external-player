#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
failed=0

check_tracked_content() {
    local description="$1"
    local pattern="$2"
    local matches
    local grep_status=0

    matches="$(git -C "${project_root}" grep -nI -E -e "${pattern}" -- . \
        ':(exclude)scripts/check-privacy.sh')" || grep_status=$?
    if (( grep_status > 1 )); then
        echo "Privacy check could not scan tracked content: ${description}" >&2
        exit 2
    fi
    if [[ -n "${matches}" ]]; then
        echo "Privacy check failed: ${description}" >&2
        echo "${matches}" >&2
        failed=1
    fi
}

check_tracked_content "a local home or workspace path is tracked" \
    '(/Users/[^/[:space:]]+/|/home/[^/[:space:]]+/|[A-Za-z]:\\Users\\[^\\[:space:]]+\\|/Volumes/[^/[:space:]]+/)'
check_tracked_content "an email address is tracked" \
    '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
check_tracked_content "a private-key block is tracked" \
    '-----BEGIN ([A-Z ]+ )?PRIVATE KEY-----'
check_tracked_content "a token-shaped value is tracked" \
    '(github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9-]{10,}|sk_live_[A-Za-z0-9]{16,}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,})'
check_tracked_content "a literal credential assignment is tracked" \
    '(password|passwd|client_secret|access_token|refresh_token)[[:space:]]*[:=][[:space:]]*["'"'][^"'"']{8,}["'"']'
check_tracked_content "a URL with embedded credentials is tracked" \
    'https?://[^/[:space:]@]+:[^/[:space:]@]+@'

sensitive_files="$(git -C "${project_root}" ls-files | rg -n -i \
    '(^|/)(\.env(\.[^/]*)?|id_rsa|id_ed25519|credentials(\.[^/]*)?|secrets?(\.[^/]*)?|tokens?(\.[^/]*)?|[^/]+\.(pem|key|p12|pfx|jks|keystore|mobileprovision|log|sqlite|db))$' | \
    rg -v -i '(^|/)\.env\.example$' || true)"
if [[ -n "${sensitive_files}" ]]; then
    echo "Privacy check failed: a sensitive-looking file is tracked" >&2
    echo "${sensitive_files}" >&2
    failed=1
fi

if (( failed != 0 )); then
    exit 1
fi

echo "Privacy check passed."

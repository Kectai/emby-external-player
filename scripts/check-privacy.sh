#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
failed=0

check_repository_content() {
    local description="$1"
    local pattern="$2"
    local relative_path
    local matches
    local grep_status

    while IFS= read -r -d '' relative_path; do
        if [[ -L "${project_root}/${relative_path}" ]]; then
            echo "Privacy check failed: symbolic links are not allowed (${relative_path})" >&2
            failed=1
            continue
        fi
        [[ "${relative_path}" == "scripts/check-privacy.sh" ]] && continue
        [[ -f "${project_root}/${relative_path}" ]] || continue
        grep_status=0
        matches="$(rg -nI -e "${pattern}" -- "${project_root}/${relative_path}")" || grep_status=$?
        if (( grep_status > 1 )); then
            echo "Privacy check could not scan repository content: ${description}" >&2
            exit 2
        fi
        if [[ -n "${matches}" ]]; then
            echo "Privacy check failed: ${description}" >&2
            echo "${relative_path}:${matches}" >&2
            failed=1
        fi
    done < <(git -C "${project_root}" ls-files --cached --others --exclude-standard -z)
}

check_repository_content "a local home or workspace path is present" \
    '(/Users/[^/[:space:]]+/|/home/[^/[:space:]]+/|[A-Za-z]:\\Users\\[^\\[:space:]]+\\|/Volumes/[^/[:space:]]+/)'
check_repository_content "an email address is present" \
    '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
check_repository_content "a private-key block is present" \
    '-----BEGIN ([A-Z ]+ )?PRIVATE KEY-----'
check_repository_content "a token-shaped value is present" \
    '(github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9-]{10,}|sk_live_[A-Za-z0-9]{16,}|AIza[0-9A-Za-z_-]{35}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,})'
check_repository_content "a literal credential assignment is present" \
    '(password|passwd|client_secret|access_token|refresh_token)[[:space:]]*[:=][[:space:]]*["'"'][^"'"']{8,}["'"']'
check_repository_content "a URL with embedded credentials is present" \
    'https?://[^/[:space:]@]+:[^/[:space:]@]+@'

sensitive_files="$(git -C "${project_root}" ls-files --cached --others --exclude-standard | rg -n -i \
    '(^|/)(\.env(\.[^/]*)?|id_rsa|id_ed25519|credentials(\.[^/]*)?|secrets?(\.[^/]*)?|tokens?(\.[^/]*)?|[^/]+\.(pem|key|p12|pfx|jks|keystore|mobileprovision|log|sqlite|db))$' | \
    rg -v -i '(^|/)\.env\.example$' || true)"
if [[ -n "${sensitive_files}" ]]; then
    echo "Privacy check failed: a sensitive-looking file is present" >&2
    echo "${sensitive_files}" >&2
    failed=1
fi

if (( failed != 0 )); then
    exit 1
fi

echo "Privacy check passed."

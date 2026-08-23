#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
dll_path="${project_root}/.local/build/bin/Release/netstandard2.1/Emby.ExternalPlayer.dll"
web_js="${project_root}/src/Emby.ExternalPlayer/Resources/external-player.js"
web_language_js="${project_root}/src/Emby.ExternalPlayer/Resources/external-player-language.js"
web_css="${project_root}/src/Emby.ExternalPlayer/Resources/external-player.css"

"${script_dir}/check-privacy.sh"
"${script_dir}/test.sh"

dll_bytes="$(wc -c < "${dll_path}" | tr -d ' ')"
web_bytes="$(( $(wc -c < "${web_js}") + $(wc -c < "${web_language_js}") + $(wc -c < "${web_css}") ))"

if (( dll_bytes > 1048576 )); then
    echo "DLL exceeds the 1 MiB release limit: ${dll_bytes} bytes" >&2
    exit 1
fi

if (( web_bytes > 81920 )); then
    echo "Embedded Web resources exceed the 80 KiB release limit: ${web_bytes} bytes" >&2
    exit 1
fi

if rg -n 'local-test-only|integration-admin' "${project_root}/src"; then
    echo "A test credential was found in production source." >&2
    exit 1
fi

if ! rg -q '<TargetFramework>netstandard2\.1</TargetFramework>' \
    "${project_root}/src/Emby.ExternalPlayer/Emby.ExternalPlayer.csproj"; then
    echo "The plugin target framework is not netstandard2.1." >&2
    exit 1
fi

if ! rg -q '<PackageVersion Include="MediaBrowser\.Server\.Core" Version="4\.9\.1\.80" />' \
    "${project_root}/Directory.Packages.props"; then
    echo "The Emby SDK baseline must remain at the lowest supported server version, 4.9.1.80." >&2
    exit 1
fi

git -C "${project_root}" diff --check
echo "Verification passed: DLL=${dll_bytes} bytes, Web=${web_bytes} bytes."

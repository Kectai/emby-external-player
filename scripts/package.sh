#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
version="1.4.5"
package_name="Emby.ExternalPlayer-${version}"
staging_parent="${project_root}/.local/package"
staging_dir="${staging_parent}/${package_name}"
artifacts_dir="${project_root}/artifacts"
archive="${artifacts_dir}/${package_name}.zip"

"${script_dir}/verify.sh"

mkdir -p "${staging_dir}/docs" "${artifacts_dir}"
cp "${project_root}/.local/build/bin/Release/netstandard2.1/Emby.ExternalPlayer.dll" "${staging_dir}/"
cp "${project_root}/README.md" "${project_root}/LICENSE" "${staging_dir}/"
cp "${project_root}/docs/INSTALL.md" \
   "${project_root}/docs/CLIENT_HANDLERS.md" \
   "${project_root}/docs/SECURITY.md" \
   "${project_root}/docs/COMPATIBILITY.md" \
   "${project_root}/docs/TESTING.md" \
   "${project_root}/docs/DESIGN.md" \
   "${staging_dir}/docs/"

(cd "${staging_parent}" && zip -q -r -FS "${archive}" "${package_name}")
(cd "${artifacts_dir}" && shasum -a 256 "${package_name}.zip" > "${package_name}.zip.sha256")
unzip -tq "${archive}"
echo "Created ${archive}"

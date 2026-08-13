#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "${project_root}/src/Emby.ExternalPlayer/Emby.ExternalPlayer.csproj" | head -n 1)"
if [[ -z "${version}" ]]; then
    echo "Unable to read the package version from Emby.ExternalPlayer.csproj." >&2
    exit 1
fi
package_name="Emby.ExternalPlayer-${version}"
local_root="${project_root}/.local"
mkdir -p "${local_root}"
staging_parent="$(mktemp -d "${local_root}/package.XXXXXX")"
trap 'rm -rf -- "${staging_parent}"' EXIT
staging_dir="${staging_parent}/${package_name}"
artifacts_dir="${project_root}/artifacts"
archive="${artifacts_dir}/${package_name}.zip"
archive_tmp="${staging_parent}/${package_name}.zip"

"${script_dir}/verify.sh"

mkdir -p "${staging_dir}/docs" "${artifacts_dir}"
cp "${project_root}/.local/build/bin/Release/netstandard2.1/Emby.ExternalPlayer.dll" "${staging_dir}/"
cp "${project_root}/README.md" "${project_root}/LICENSE" "${staging_dir}/"
COPYFILE_DISABLE=1 cp -R "${project_root}/docs/images" "${staging_dir}/docs/"
cp "${project_root}/docs/INSTALL.md" \
   "${project_root}/docs/CLIENT_HANDLERS.md" \
   "${project_root}/docs/SECURITY.md" \
   "${project_root}/docs/COMPATIBILITY.md" \
   "${project_root}/docs/TESTING.md" \
   "${project_root}/docs/ARCHITECTURE.md" \
   "${staging_dir}/docs/"

chmod -R u=rwX,go=rX "${staging_dir}"
(cd "${staging_parent}" && COPYFILE_DISABLE=1 zip -X -q -r "${archive_tmp}" "${package_name}")
mv -f "${archive_tmp}" "${archive}"
(cd "${artifacts_dir}" && shasum -a 256 "${package_name}.zip" > "${package_name}.zip.sha256")
unzip -tq "${archive}"
if zipinfo -v "${archive}" | rg -q 'Unix UID/GID'; then
    echo "Package contains local Unix UID/GID metadata." >&2
    exit 1
fi
echo "Created ${archive}"

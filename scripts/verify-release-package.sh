#!/usr/bin/env bash
set -euo pipefail

archive="${1:-}"
rid="${2:-}"
if [[ ! -f "$archive" || -z "$rid" ]]; then
    echo "Usage: $0 <archive> <runtime-identifier>" >&2
    exit 2
fi

checksum="$archive.sha256"
if [[ ! -f "$checksum" ]]; then
    echo "Release checksum is missing: $checksum" >&2
    exit 1
fi

archive_directory="$(cd "$(dirname "$archive")" && pwd)"
checksum_name="$(basename "$checksum")"
if command -v sha256sum >/dev/null 2>&1; then
    (cd "$archive_directory" && sha256sum -c "$checksum_name")
else
    (cd "$archive_directory" && shasum -a 256 -c "$checksum_name")
fi

extract_root="$(mktemp -d)"
cleanup() { rm -r "$extract_root"; }
trap cleanup EXIT

case "$archive" in
    *.zip)
        if command -v powershell >/dev/null 2>&1 && command -v cygpath >/dev/null 2>&1; then
            windows_archive="$(cygpath -w "$archive")"
            windows_extract="$(cygpath -w "$extract_root")"
            powershell -NoProfile -Command "Expand-Archive -Path '$windows_archive' -DestinationPath '$windows_extract' -Force"
        elif command -v unzip >/dev/null 2>&1; then
            unzip -q "$archive" -d "$extract_root"
        else
            echo "Extracting zip packages requires unzip or PowerShell Expand-Archive." >&2
            exit 1
        fi
        ;;
    *.tar.gz) tar -xzf "$archive" -C "$extract_root" ;;
    *) echo "Unsupported release archive: $archive" >&2; exit 2 ;;
esac

package_root="$(find "$extract_root" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
test -n "$package_root"
test -f "$package_root/README.md"
test -f "$package_root/LICENSE"
test -f "$package_root/RELEASE_NOTES.md"

if find "$package_root" -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.pdb' \) | grep -q .; then
    echo "Release contains source, project, or debug-symbol files." >&2
    exit 1
fi

if find "$package_root" -type d \( -name history -o -name common -o -name localisation \) | grep -q .; then
    echo "Release contains game installation directories." >&2
    exit 1
fi

case "$rid" in
    osx-*) executable="$package_root/Oxide.app/Contents/MacOS/Oxide" ;;
    win-*) executable="$package_root/app/Oxide.exe" ;;
    linux-*) executable="$package_root/app/Oxide" ;;
    *) echo "Unsupported runtime identifier: $rid" >&2; exit 2 ;;
esac

test -f "$executable"
if [[ "$rid" != win-* ]]; then
    test -x "$executable"
fi

host_rid=""
case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) host_rid="osx-arm64" ;;
    Darwin-x86_64) host_rid="osx-x64" ;;
    Linux-x86_64) host_rid="linux-x64" ;;
    MINGW*-x86_64|MSYS*-x86_64) host_rid="win-x64" ;;
esac

if [[ "$host_rid" == "$rid" ]]; then
    version_output="$(PATH=/usr/bin:/bin "$executable" --version)"
    [[ "$version_output" == Oxide\ * ]]
fi

echo "Release package verification passed for $rid."

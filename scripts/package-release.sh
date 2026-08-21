#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

rid="${1:-}"
version="${2:-0.1.0-dev}"
output_root="${3:-artifacts/releases}"

case "$rid" in
    win-x64|linux-x64|osx-x64|osx-arm64) ;;
    *)
        echo "Usage: $0 <win-x64|linux-x64|osx-x64|osx-arm64> [version] [output-directory]" >&2
        exit 2
        ;;
esac

package_name="oxide-${version}-${rid}"
bundle_version="${version%%-*}"
work_root="$(mktemp -d)"
publish_root="$work_root/publish"
package_root="$work_root/$package_name"
cleanup() { rm -r "$work_root"; }
trap cleanup EXIT

dotnet restore Oxide.App/Oxide.App.csproj --runtime "$rid"
dotnet publish Oxide.App/Oxide.App.csproj \
    --configuration Release \
    --runtime "$rid" \
    --self-contained true \
    --no-restore \
    --output "$publish_root" \
    -p:Version="$version" \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

mkdir -p "$package_root"
cp README.md LICENSE RELEASE_NOTES.md "$package_root/"

if [[ "$rid" == osx-* ]]; then
    app_root="$package_root/Oxide.app/Contents"
    mkdir -p "$app_root/MacOS" "$app_root/Resources"
    cp -R "$publish_root"/. "$app_root/MacOS/"
    cat > "$app_root/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>Oxide</string>
  <key>CFBundleDisplayName</key><string>Oxide</string>
  <key>CFBundleIdentifier</key><string>dev.oxide.ide</string>
  <key>CFBundleVersion</key><string>${bundle_version}</string>
  <key>CFBundleShortVersionString</key><string>${bundle_version}</string>
  <key>CFBundleExecutable</key><string>Oxide</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
EOF
    chmod +x "$app_root/MacOS/Oxide"
else
    mkdir -p "$package_root/app"
    cp -R "$publish_root"/. "$package_root/app/"
    if [[ "$rid" == linux-* ]]; then
        chmod +x "$package_root/app/Oxide"
    fi
fi

find "$package_root" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete
mkdir -p "$output_root"
output_root="$(cd "$output_root" && pwd)"

if [[ "$rid" == linux-* ]]; then
    archive="$output_root/$package_name.tar.gz"
    rm -f "$archive" "$archive.sha256"
    tar -czf "$archive" -C "$work_root" "$package_name"
else
    archive="$output_root/$package_name.zip"
    rm -f "$archive" "$archive.sha256"
    if command -v zip >/dev/null 2>&1; then
        (cd "$work_root" && zip -qry "$archive" "$package_name")
    elif command -v powershell >/dev/null 2>&1 && command -v cygpath >/dev/null 2>&1; then
        windows_package="$(cygpath -w "$package_root")"
        windows_archive="$(cygpath -w "$archive")"
        powershell -NoProfile -Command "Compress-Archive -Path '$windows_package' -DestinationPath '$windows_archive' -Force"
    else
        echo "Creating zip packages requires zip or PowerShell Compress-Archive." >&2
        exit 1
    fi
fi

if command -v sha256sum >/dev/null 2>&1; then
    (cd "$output_root" && sha256sum "$(basename "$archive")") > "$archive.sha256"
else
    (cd "$output_root" && shasum -a 256 "$(basename "$archive")") > "$archive.sha256"
fi

echo "$archive"

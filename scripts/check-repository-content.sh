#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

forbidden=0
while IFS= read -r tracked_path; do
    case "$tracked_path" in
        history/*|common/*|map/*|localisation/*|events/*|gfx/*|sound/*|music/*|interface/*)
            echo "Forbidden game-installation path is tracked: $tracked_path" >&2
            forbidden=1
            ;;
        artifacts/*)
            echo "Generated artifact is tracked: $tracked_path" >&2
            forbidden=1
            ;;
    esac
done < <(git ls-files)

while IFS= read -r large_path; do
    echo "Unexpected file larger than 10 MiB: $large_path" >&2
    forbidden=1
done < <(find . -type f -size +10M \
    -not -path './.git/*' \
    -not -path '*/bin/*' \
    -not -path '*/obj/*' \
    -not -path './artifacts/*')

if (( forbidden != 0 )); then
    echo "Repository content safeguard failed." >&2
    exit 1
fi

echo "Repository content safeguard passed."

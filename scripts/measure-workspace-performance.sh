#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

game_root="${1:-Oxide.Tests/Fixtures/Corpus/game}"
mod_root="${2:-Oxide.Tests/Fixtures/Corpus/mod}"
iterations="${3:-5}"

if ! [[ "$iterations" =~ ^[1-9][0-9]*$ ]]; then
    echo "Iterations must be a positive integer." >&2
    exit 2
fi

mkdir -p artifacts/performance
dotnet build Oxide.CorpusSummary/Oxide.CorpusSummary.csproj --configuration Release --no-restore

for iteration in $(seq 1 "$iterations"); do
    arguments=(
        --game-root "$game_root"
        --name "Workspace performance iteration $iteration"
        --output "artifacts/performance/workspace-$iteration.json"
    )
    if [[ -n "$mod_root" ]]; then
        arguments+=(--mod-root "$mod_root")
    fi

    dotnet run --project Oxide.CorpusSummary/Oxide.CorpusSummary.csproj \
        --configuration Release --no-build -- "${arguments[@]}" > /dev/null
done

echo "Wrote $iterations workspace measurements to artifacts/performance/."

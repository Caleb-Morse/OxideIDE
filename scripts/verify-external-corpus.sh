#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if (( $# < 1 || $# > 2 )); then
    echo "Usage: scripts/verify-external-corpus.sh <game-root> [mod-root]" >&2
    exit 2
fi

game_root="$1"
mod_root="${2:-}"
if [[ ! -d "$game_root" ]]; then
    echo "Game root does not exist: $game_root" >&2
    exit 2
fi
if [[ -n "$mod_root" && ! -d "$mod_root" ]]; then
    echo "Mod root does not exist: $mod_root" >&2
    exit 2
fi

dotnet restore Oxide.sln
dotnet build Oxide.sln --configuration Release --no-restore
OXIDE_HOI4_CORPUS_ROOT="$game_root" \
    dotnet test Oxide.sln --configuration Release --no-build --no-restore --filter 'Category=ExternalCorpus'

mkdir -p artifacts
summary_arguments=(
    --game-root "$game_root"
    --name "External HOI4 corpus"
    --output artifacts/external-corpus-summary.json
)
if [[ -n "$mod_root" ]]; then
    summary_arguments+=(--mod-root "$mod_root")
fi

dotnet run --project Oxide.CorpusSummary/Oxide.CorpusSummary.csproj \
    --configuration Release --no-build -- "${summary_arguments[@]}" > /dev/null

echo "External corpus verification passed."

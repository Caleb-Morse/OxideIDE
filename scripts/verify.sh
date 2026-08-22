#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

bash scripts/check-repository-content.sh
dotnet restore Oxide.sln
dotnet build Oxide.sln --configuration Debug --no-restore
dotnet build Oxide.sln --configuration Release --no-restore
dotnet test Oxide.sln --configuration Debug --no-build --no-restore --filter 'Category!=ExternalCorpus'
dotnet format Oxide.sln --verify-no-changes --no-restore

mkdir -p artifacts
dotnet run --project Oxide.CorpusSummary/Oxide.CorpusSummary.csproj \
    --configuration Release --no-build -- \
    --game-root Oxide.Tests/Fixtures/Corpus/game \
    --mod-root Oxide.Tests/Fixtures/Corpus/mod \
    --name "Synthetic corpus" \
    --output artifacts/synthetic-corpus-summary.json > /dev/null

echo "Oxide verification passed."

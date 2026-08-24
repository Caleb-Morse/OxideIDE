using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;

namespace Oxide.Core.Semantics.Snapshots;

public sealed record SemanticDeclarationInventory(
    ImmutableArray<DeclarationInventoryItem<StateDeclaration>> States,
    ImmutableArray<DeclarationInventoryItem<CountryTagDeclaration>> Countries,
    ImmutableArray<DeclarationInventoryItem<StrategicRegionDeclaration>> StrategicRegions,
    ImmutableArray<DeclarationInventoryItem<LocalisationDeclaration>> Localisations,
    ImmutableArray<SemanticDiagnosticInventoryItem> Diagnostics)
{
    public static SemanticDeclarationInventory Empty { get; } = new([], [], [], [], []);
}

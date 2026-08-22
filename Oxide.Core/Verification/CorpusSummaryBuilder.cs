using System.Collections.Immutable;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Verification;

public static class CorpusSummaryBuilder
{
    public static CorpusSummary Build(WorkspaceSnapshot snapshot, TimeSpan totalLoadDuration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (totalLoadDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLoadDuration));
        }

        var syntaxDiagnostics = snapshot.Documents
            .Where(document => document.SyntaxTree is not null)
            .SelectMany(document => document.SyntaxTree!.Diagnostics)
            .ToArray();
        var references = snapshot.Semantics.States.Values
            .SelectMany(state => state.Owner is null ? state.Cores : state.Cores.Insert(0, state.Owner))
            .Select(reference => reference.Resolution)
            .ToArray();

        return new CorpusSummary(
            snapshot.Configuration.DisplayName,
            snapshot.Version,
            snapshot.Documents.Length,
            snapshot.Documents.Count(document => document.LoadStatus is DocumentLoadStatus.Loaded),
            snapshot.Documents.Count(document => document.LoadStatus is DocumentLoadStatus.Failed),
            syntaxDiagnostics.Length,
            CountByCode(syntaxDiagnostics.Select(diagnostic => diagnostic.Code)),
            CountByCode(snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)),
            snapshot.Semantics.StateDeclarations.Length,
            snapshot.Semantics.States.Count,
            snapshot.Semantics.CountryDeclarations.Length,
            snapshot.Semantics.Countries.Count,
            snapshot.Semantics.Diagnostics.Length,
            CountByCode(snapshot.Semantics.Diagnostics.Select(diagnostic => diagnostic.Code)),
            new ReferenceResolutionCounts(
                references.Length,
                references.Count(reference => reference is ResolvedCountry),
                references.Count(reference => reference is MissingCountry),
                references.Count(reference => reference is AmbiguousCountry),
                references.Count(reference => reference is InvalidCountry)),
            totalLoadDuration.TotalMilliseconds);
    }

    private static ImmutableSortedDictionary<string, int> CountByCode(IEnumerable<string> codes) =>
        codes.GroupBy(code => code, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

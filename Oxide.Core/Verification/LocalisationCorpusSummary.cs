using System.Collections.Immutable;

namespace Oxide.Core.Verification;

public sealed record LocalisationCorpusSummary(
    int FilesDiscovered,
    int DocumentsLoaded,
    int DocumentsFailed,
    int SyntaxDiagnosticCount,
    ImmutableSortedDictionary<string, int> SyntaxDiagnosticsByCode,
    int SemanticDiagnosticCount,
    ImmutableSortedDictionary<string, int> SemanticDiagnosticsByCode,
    ImmutableArray<string> LanguagesDiscovered,
    ImmutableSortedDictionary<string, int> DeclarationsByLanguage,
    int DeclarationCount,
    int UniqueIdentityCount,
    int DuplicateIdentityCount,
    int AmbiguousEntryCount,
    int DeclarationsWithValidProvenance,
    string RequestedLanguage,
    string EffectiveLanguage,
    bool EnglishFallbackEnabled,
    LocalisationResolutionCounts StateNames,
    LocalisationResolutionCounts CountryNames,
    LocalisationResolutionCounts StrategicRegionNames,
    double NameProjectionMilliseconds,
    double NameProjectionsPerSecond,
    long ManagedMemoryBytesAtReport);

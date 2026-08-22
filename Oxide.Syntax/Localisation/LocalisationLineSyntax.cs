using Oxide.Syntax.Text;

namespace Oxide.Syntax.Localisation;

public abstract record LocalisationLineSyntax(LocalisationLineKind Kind, TextSpan FullSpan);

public sealed record LocalisationBlankLineSyntax(TextSpan FullSpan)
    : LocalisationLineSyntax(LocalisationLineKind.Blank, FullSpan);

public sealed record LocalisationCommentLineSyntax(TextSpan FullSpan)
    : LocalisationLineSyntax(LocalisationLineKind.Comment, FullSpan);

public sealed record LocalisationLanguageHeaderSyntax(
    string Language,
    TextSpan LanguageSpan,
    TextSpan FullSpan)
    : LocalisationLineSyntax(LocalisationLineKind.LanguageHeader, FullSpan);

public sealed record LocalisationEntrySyntax(
    string? Language,
    string Key,
    int? Version,
    string Value,
    TextSpan KeySpan,
    TextSpan? VersionSpan,
    TextSpan QuotedValueSpan,
    TextSpan ValueSpan,
    TextSpan FullSpan)
    : LocalisationLineSyntax(LocalisationLineKind.Entry, FullSpan);

public sealed record LocalisationUnknownLineSyntax(TextSpan FullSpan)
    : LocalisationLineSyntax(LocalisationLineKind.Unknown, FullSpan);

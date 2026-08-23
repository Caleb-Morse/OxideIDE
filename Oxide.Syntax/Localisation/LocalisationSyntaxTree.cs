using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Localisation;

public sealed class LocalisationSyntaxTree
{
    internal LocalisationSyntaxTree(
        SourceText source,
        ImmutableArray<LocalisationLineSyntax> lines,
        ImmutableArray<SyntaxDiagnostic> diagnostics)
    {
        Source = source;
        Lines = lines;
        Diagnostics = diagnostics;
    }

    public SourceText Source { get; }

    public ImmutableArray<LocalisationLineSyntax> Lines { get; }

    public ImmutableArray<LocalisationEntrySyntax> Entries =>
        Lines.OfType<LocalisationEntrySyntax>().ToImmutableArray();

    public ImmutableArray<LocalisationLanguageHeaderSyntax> LanguageHeaders =>
        Lines.OfType<LocalisationLanguageHeaderSyntax>().ToImmutableArray();

    public ImmutableArray<SyntaxDiagnostic> Diagnostics { get; }

    public string ToFullString() => Source.Text;

    public ReadOnlyMemory<byte> GetOriginalBytes() => Source.GetOriginalBytes();
}

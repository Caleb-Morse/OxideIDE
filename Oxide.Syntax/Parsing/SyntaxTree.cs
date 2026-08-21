using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Parsing;

public sealed class SyntaxTree
{
    internal SyntaxTree(
        SourceText source,
        ImmutableArray<SyntaxToken> tokens,
        DocumentSyntax root,
        ImmutableArray<SyntaxDiagnostic> diagnostics)
    {
        Source = source;
        Tokens = tokens;
        Root = root;
        Diagnostics = diagnostics;
    }

    public SourceText Source { get; }

    public ImmutableArray<SyntaxToken> Tokens { get; }

    public DocumentSyntax Root { get; }

    public ImmutableArray<SyntaxDiagnostic> Diagnostics { get; }

    public string ToFullString() => string.Concat(
        Tokens
            .Where(token => token.Kind is not SyntaxKind.EndOfFileToken && !token.IsMissing)
            .Select(token => token.Text));

    public ReadOnlyMemory<byte> GetOriginalBytes() => Source.GetOriginalBytes();
}

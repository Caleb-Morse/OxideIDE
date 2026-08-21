using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Syntax.Lexing;

public sealed record LexerResult(
    ImmutableArray<SyntaxToken> Tokens,
    ImmutableArray<SyntaxDiagnostic> Diagnostics);

using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Editing;

public sealed record TextChange
{
    public TextChange(TextSpan span, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        Span = span;
        Replacement = replacement;
    }

    public TextSpan Span { get; }

    public string Replacement { get; }

    public int LengthDelta => Replacement.Length - Span.Length;
}

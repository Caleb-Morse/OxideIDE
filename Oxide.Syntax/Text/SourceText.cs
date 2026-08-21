using System.Collections.Immutable;
using System.Text;

namespace Oxide.Syntax.Text;

public sealed class SourceText
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private readonly ImmutableArray<byte> originalBytes;
    private readonly ImmutableArray<int> lineStarts;

    private SourceText(string text, SourceEncoding encoding, ImmutableArray<byte> originalBytes)
    {
        Text = text;
        Encoding = encoding;
        this.originalBytes = originalBytes;
        lineStarts = FindLineStarts(text);
        Newlines = DetectNewlines(text);
        HasFinalNewline = text.EndsWith('\n') || text.EndsWith('\r');
    }

    public string Text { get; }

    public int Length => Text.Length;

    public SourceEncoding Encoding { get; }

    public NewlineKind Newlines { get; }

    public bool HasFinalNewline { get; }

    public int LineCount => lineStarts.Length;

    public char this[int index] => Text[index];

    public static SourceText From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new SourceText(text, SourceEncoding.Utf8, ImmutableArray<byte>.Empty);
    }

    public static SourceText FromBytes(ReadOnlySpan<byte> bytes)
    {
        var hasBom = bytes.StartsWith(Utf8Bom);
        var content = hasBom ? bytes[Utf8Bom.Length..] : bytes;
        var text = StrictUtf8.GetString(content);

        return new SourceText(
            text,
            hasBom ? SourceEncoding.Utf8WithBom : SourceEncoding.Utf8,
            ImmutableArray.Create(bytes.ToArray()));
    }

    public static SourceText Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromBytes(File.ReadAllBytes(path));
    }

    public string GetText(TextSpan span)
    {
        ValidateSpan(span);
        return Text.Substring(span.Start, span.Length);
    }

    public ReadOnlyMemory<byte> GetOriginalBytes()
    {
        if (!originalBytes.IsDefaultOrEmpty)
        {
            return originalBytes.AsMemory();
        }

        var content = StrictUtf8.GetBytes(Text);
        if (Encoding is SourceEncoding.Utf8)
        {
            return content;
        }

        var bytes = new byte[Utf8Bom.Length + content.Length];
        Utf8Bom.CopyTo(bytes, 0);
        content.CopyTo(bytes, Utf8Bom.Length);
        return bytes;
    }

    public TextPosition GetPosition(int offset)
    {
        if ((uint)offset > (uint)Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var line = lineStarts.BinarySearch(offset);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return new TextPosition(line, offset - lineStarts[line]);
    }

    private void ValidateSpan(TextSpan span)
    {
        if (span.End > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(span));
        }
    }

    private static ImmutableArray<int> FindLineStarts(string text)
    {
        var starts = ImmutableArray.CreateBuilder<int>();
        starts.Add(0);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                starts.Add(index + 1);
            }
            else if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToImmutable();
    }

    private static NewlineKind DetectNewlines(string text)
    {
        var sawLf = false;
        var sawCrLf = false;
        var sawCr = false;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    sawCrLf = true;
                    index++;
                }
                else
                {
                    sawCr = true;
                }
            }
            else if (text[index] == '\n')
            {
                sawLf = true;
            }
        }

        var kinds = (sawLf ? 1 : 0) + (sawCrLf ? 1 : 0) + (sawCr ? 1 : 0);
        if (kinds > 1)
        {
            return NewlineKind.Mixed;
        }

        if (sawCrLf)
        {
            return NewlineKind.CarriageReturnLineFeed;
        }

        if (sawLf)
        {
            return NewlineKind.LineFeed;
        }

        return sawCr ? NewlineKind.CarriageReturn : NewlineKind.None;
    }
}

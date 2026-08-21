using System.Text;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Syntax;

public sealed class SourceTextTests
{
    [Fact]
    public void FromBytes_preserves_utf8_bom_and_original_bytes()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("state = 1\r\n")];

        var source = SourceText.FromBytes(bytes);

        Assert.Equal(SourceEncoding.Utf8WithBom, source.Encoding);
        Assert.Equal(NewlineKind.CarriageReturnLineFeed, source.Newlines);
        Assert.True(source.HasFinalNewline);
        Assert.Equal(bytes, source.GetOriginalBytes().ToArray());
    }

    [Fact]
    public void FromBytes_rejects_invalid_utf8()
    {
        byte[] bytes = [0xC3, 0x28];

        Assert.Throws<DecoderFallbackException>(() => SourceText.FromBytes(bytes));
    }

    [Fact]
    public void Source_tracks_mixed_newlines_and_line_positions()
    {
        var source = SourceText.From("a\r\nb\nc\rd");

        Assert.Equal(NewlineKind.Mixed, source.Newlines);
        Assert.Equal(4, source.LineCount);
        Assert.Equal(new TextPosition(2, 0), source.GetPosition(5));
        Assert.False(source.HasFinalNewline);
    }
}

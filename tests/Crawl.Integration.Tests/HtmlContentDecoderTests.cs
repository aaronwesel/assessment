using System.Text;
using Crawl.Infrastructure.Http;

namespace Crawl.Integration.Tests;

public class HtmlContentDecoderTests
{
    [Fact]
    public void LooksLikeHtml_DetectsDoctypeAndHtmlTag()
    {
        Assert.True(HtmlContentDecoder.LooksLikeHtml(Encoding.UTF8.GetBytes("<!DOCTYPE html><html></html>")));
        Assert.True(HtmlContentDecoder.LooksLikeHtml(Encoding.UTF8.GetBytes("  \n<HTML lang=en>")));
        Assert.False(HtmlContentDecoder.LooksLikeHtml(Encoding.UTF8.GetBytes("%PDF-1.4")));
        Assert.False(HtmlContentDecoder.LooksLikeHtml(Encoding.UTF8.GetBytes("{\"a\":1}")));
    }

    [Fact]
    public void IsHtmlMediaType_AcceptsHtmlAndXhtml()
    {
        Assert.True(HtmlContentDecoder.IsHtmlMediaType("text/html"));
        Assert.True(HtmlContentDecoder.IsHtmlMediaType("application/xhtml+xml"));
        Assert.False(HtmlContentDecoder.IsHtmlMediaType("application/json"));
        Assert.False(HtmlContentDecoder.IsHtmlMediaType(null));
    }

    [Fact]
    public void Decode_UsesCharsetFromContentType()
    {
        var body = Encoding.GetEncoding("ISO-8859-1").GetBytes("café");
        var text = HtmlContentDecoder.Decode(body, "text/html; charset=iso-8859-1");
        Assert.Contains("café", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_UsesMetaCharsetWhenHeaderMissing()
    {
        var html = "<html><head><meta charset=\"utf-8\"></head><body>ok</body></html>";
        var text = HtmlContentDecoder.Decode(Encoding.UTF8.GetBytes(html), null);
        Assert.Contains("ok", text, StringComparison.Ordinal);
    }
}

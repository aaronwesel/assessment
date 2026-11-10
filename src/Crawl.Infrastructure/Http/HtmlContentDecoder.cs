using System.Text;
using System.Text.RegularExpressions;

namespace Crawl.Infrastructure.Http;

/// <summary>
/// Charset detection + HTML-ish sniffing when Content-Type is missing or wrong.
/// </summary>
public static partial class HtmlContentDecoder
{
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static bool LooksLikeHtml(ReadOnlySpan<byte> prefix)
    {
        // Skip BOM / leading whitespace.
        var i = 0;
        if (prefix.Length >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF)
            i = 3;
        while (i < prefix.Length && (prefix[i] is 0x09 or 0x0A or 0x0D or 0x20))
            i++;

        if (i >= prefix.Length || prefix[i] != (byte)'<')
            return false;

        var slice = prefix[i..];
        return StartsWithAsciiIgnoreCase(slice, "<!doctype html")
               || StartsWithAsciiIgnoreCase(slice, "<html")
               || StartsWithAsciiIgnoreCase(slice, "<head")
               || StartsWithAsciiIgnoreCase(slice, "<body")
               || StartsWithAsciiIgnoreCase(slice, "<!--");
    }

    public static bool IsHtmlMediaType(string? mediaType) =>
        mediaType is not null
        && (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase));

    public static string Decode(byte[] body, string? contentTypeHeader)
    {
        var encoding = ResolveEncoding(body, contentTypeHeader);
        return encoding.GetString(body);
    }

    private static Encoding ResolveEncoding(byte[] body, string? contentTypeHeader)
    {
        var fromHeader = CharsetFromContentType(contentTypeHeader);
        if (fromHeader is not null)
            return fromHeader;

        // Peek a UTF-8 decode of the head for <meta charset>.
        var peekLen = Math.Min(body.Length, 4096);
        var peek = Utf8Strict.GetString(body, 0, peekLen);
        var meta = MetaCharsetRegex().Match(peek);
        if (meta.Success)
        {
            var name = meta.Groups[1].Success ? meta.Groups[1].Value : meta.Groups[2].Value;
            if (TryGetEncoding(name, out var enc))
                return enc;
        }

        return Encoding.UTF8;
    }

    private static Encoding? CharsetFromContentType(string? contentTypeHeader)
    {
        if (string.IsNullOrWhiteSpace(contentTypeHeader))
            return null;

        const string marker = "charset=";
        var idx = contentTypeHeader.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var raw = contentTypeHeader[(idx + marker.Length)..].Trim().Trim('"', '\'');
        var end = raw.IndexOfAny([';', ' ']);
        if (end >= 0)
            raw = raw[..end];

        return TryGetEncoding(raw, out var enc) ? enc : null;
    }

    private static bool TryGetEncoding(string name, out Encoding encoding)
    {
        try
        {
            encoding = Encoding.GetEncoding(name);
            return true;
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
            return false;
        }
    }

    private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<byte> data, string ascii)
    {
        if (data.Length < ascii.Length)
            return false;
        for (var i = 0; i < ascii.Length; i++)
        {
            var b = data[i];
            var c = (byte)ascii[i];
            if (b == c)
                continue;
            // ASCII case fold
            if (c is >= (byte)'a' and <= (byte)'z' && b == c - 32)
                continue;
            if (c is >= (byte)'A' and <= (byte)'Z' && b == c + 32)
                continue;
            return false;
        }

        return true;
    }

    [GeneratedRegex(
        @"<meta\s+[^>]*charset\s*=\s*[""']?([a-zA-Z0-9_\-]+)[""']?|<meta\s+[^>]*http-equiv\s*=\s*[""']?content-type[""']?[^>]*content\s*=\s*[""'][^""']*charset=([a-zA-Z0-9_\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaCharsetRegex();
}

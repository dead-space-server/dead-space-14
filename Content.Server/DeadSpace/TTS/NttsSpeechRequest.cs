// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;

namespace Content.Server.DeadSpace.TTS;

public static class NttsSpeechRequest
{
    private static readonly Regex Laughter = new(@"\b(?:ха){2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string PrepareText(string ssml)
    {
        var text = XElement.Parse(ssml).Value;
        return Laughter.Replace(text, match => string.Join("-", Enumerable.Repeat("ха", match.Length / 2))).Trim();
    }

    public static HttpRequestMessage Create(string url, string token, string speaker, string ssml)
    {
        // NTTS accepts plain text. XML parsing also restores escaped punctuation and preserves literal angle brackets.
        var text = PrepareText(ssml);
        var separator = url.Contains('?') ? "&" : "?";
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{url}{separator}speaker={Uri.EscapeDataString(speaker)}&text={Uri.EscapeDataString(text)}&ext=ogg");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("DeadSpace14/1.0");
        return request;
    }
}

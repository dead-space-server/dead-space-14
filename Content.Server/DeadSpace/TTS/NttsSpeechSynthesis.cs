// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Content.Server.DeadSpace.TTS;

public static class NttsSpeechSynthesis
{
    private static readonly Regex Parts = new(@"([^,.;:!?…]+)([,.;:!?…]*)", RegexOptions.Compiled);
    public readonly record struct Part(string Text, double Pause);

    public static List<Part> Split(string ssml)
    {
        var result = new List<Part>();
        foreach (Match match in Parts.Matches(NttsSpeechRequest.PrepareText(ssml)))
        {
            var words = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(words))
                continue;
            var ending = match.Groups[2].Value;
            var pause = ending.Contains('…') || ending.Count(c => c == '.') > 1 ? 0.65
                : ending.IndexOfAny(['.', '!', '?']) >= 0 ? 0.4 : 0.2;
            var part = new Part(words + ending, pause);
            // Short standalone chunks destabilize voices such as Alyx. Keep enough spoken context together.
            if (result.Count > 0 && result[^1].Text.Count(char.IsLetter) < 32 || result.Count == 6)
                result[^1] = new Part(result[^1].Text + " " + part.Text, pause);
            else
                result.Add(part);
        }
        if (result.Count > 1 && result[^1].Text.Count(char.IsLetter) < 32)
        {
            result[^2] = new Part(result[^2].Text + " " + result[^1].Text, result[^1].Pause);
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }

    public static async Task<byte[]> GenerateAsync(HttpClient client, string url, string token, string speaker,
        string ssml, CancellationToken cancellation, bool pauses = true)
    {
        var parts = pauses ? Split(ssml) : [new Part(NttsSpeechRequest.PrepareText(ssml), 0)];
        if (parts.Count == 0)
            throw new InvalidDataException("No spoken text in NTTS request");
        var chunks = new List<(byte[] Data, double Pause)>();
        foreach (var part in parts)
        {
            for (var attempt = 0; ; attempt++)
            {
                using var request = NttsSpeechRequest.Create(url, token, speaker, new XElement("speak", part.Text).ToString());
                using var response = await client.SendAsync(request, cancellation);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
                {
                    var retry = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                    await Task.Delay(retry, cancellation);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync(cancellation);
                if (data.Length < 4 || data[0] != 'O' || data[1] != 'g' || data[2] != 'g' || data[3] != 'S')
                    throw new InvalidDataException("NTTS returned invalid Ogg audio");
                chunks.Add((data, part.Pause));
                break;
            }
        }
        // Keep one compressed stream: client playback, radio cues, echoes and the cache retain their existing behavior.
        return chunks.Count == 1 ? chunks[0].Data
            : await Task.Run(() => NttsAudioJoiner.Join(chunks, cancellation), cancellation);
    }
}

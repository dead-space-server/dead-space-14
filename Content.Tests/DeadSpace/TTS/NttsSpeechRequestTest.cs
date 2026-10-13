// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using System.Linq;
using System.Net.Http;
using Content.Server.DeadSpace.TTS;
using Content.Shared.Preferences;
using Content.Shared.DeadSpace.TTS;
using NUnit.Framework;

namespace Content.Tests.DeadSpace.TTS;

[TestFixture]
public sealed class NttsSpeechRequestTest
{
    [TestCase("Привет. Пока...", "Привет. Пока...")]
    [TestCase("Привет… Ты здесь?! Да!", "Привет… Ты здесь?! Да!")]
    [TestCase("Да, ... конечно?", "Да, ... конечно?")]
    [TestCase("Вопрос? Ответ!", "Вопрос? Ответ!")]
    [TestCase("Тест &amp; + &lt;слово&gt;", "Тест & + <слово>")]
    [TestCase("К сожалению, нет.", "К сожалению, нет.")]
    [TestCase("Хахахаха!", "ха-ха-ха-ха!")]
    public void RequestPreservesPunctuationAndIntonation(string ssmlText, string expected)
    {
        using var request = NttsSpeechRequest.Create("https://example.invalid/tts?version=1", "test-token",
            "speaker + &", $"<speak><prosody rate=\"fast\">{ssmlText}</prosody></speak>");
        var query = request.RequestUri!.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(request.Headers.Authorization.Parameter, Is.EqualTo("test-token"));
            Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo("DeadSpace14/1.0"));
            Assert.That(query["text"], Is.EqualTo(expected));
            Assert.That(query["speaker"], Is.EqualTo("speaker + &"));
            Assert.That(query["ext"], Is.EqualTo("ogg"));
            Assert.That(query["version"], Is.EqualTo("1"));
        });
    }

    [Test]
    public void EditingProfilePreservesBothProviderSelections()
    {
        var original = new HumanoidCharacterProfile { Voice = "Eugene" }.WithTtsVoice(TTSVoices.Ntts, "NTTSKleiner");
        var renamed = original.WithName("Новое имя");
        var nttsChanged = renamed.WithTtsVoice(TTSVoices.Ntts, "NTTSPapich");
        var sileroChanged = nttsChanged.WithVoice("Noble");
        Assert.Multiple(() =>
        {
            Assert.That(renamed.TtsVoices[TTSVoices.Ntts], Is.EqualTo(original.TtsVoices[TTSVoices.Ntts]));
            Assert.That(nttsChanged.Voice, Is.EqualTo(original.Voice));
            Assert.That(sileroChanged.TtsVoices[TTSVoices.Ntts], Is.EqualTo(nttsChanged.TtsVoices[TTSVoices.Ntts]));
            Assert.That(original.TtsVoices[TTSVoices.Ntts], Is.EqualTo("NTTSKleiner"));
            Assert.That(original.MemberwiseEquals(original.WithTtsVoice(TTSVoices.Ntts, "NTTSPapich")), Is.False);
            Assert.That(original.MemberwiseEquals(original.WithVoice("Noble")), Is.False);
        });
    }
}

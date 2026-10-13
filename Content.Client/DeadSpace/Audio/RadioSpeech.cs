// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.Audio;

/// <summary>Original synthesized radio squelch, loaded through the engine's raw-audio API.</summary>
internal static class RadioSpeech
{
    internal const int SampleRate = 24000;
    internal const float FilterStrength = 1.2f;
    internal const float OpeningDuration = 0.14f;

    internal static short[] CreateCue(bool closing)
    {
        var output = new short[(int) (SampleRate * (closing ? 0.12f : OpeningDuration))];
        WriteCue(output, SampleRate, closing ? 1900f : 1450f, closing ? 1150f : 1900f);
        return output;
    }

    private static void WriteCue(Span<short> output, int sampleRate, float first, float second)
    {
        var toneLength = (int) (sampleRate * 0.045f);
        var gap = (int) (sampleRate * 0.015f);
        for (var tone = 0; tone < 2; tone++)
        for (var i = 0; i < toneLength; i++)
        {
            var index = tone * (toneLength + gap) + i;
            if (index >= output.Length)
                break;
            var envelope = MathF.Min(1f, MathF.Min(i, toneLength - 1 - i) / (sampleRate * 0.005f));
            var wave = MathF.Sin(MathF.Tau * (tone == 0 ? first : second) * i / sampleRate);
            output[index] = (short) (wave * envelope * 0.12f * short.MaxValue);
        }
    }
}

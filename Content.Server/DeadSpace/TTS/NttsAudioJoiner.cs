// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.IO;
using System.Linq;
using System.Threading;
using NVorbis;
using OggVorbisEncoder;

namespace Content.Server.DeadSpace.TTS;

public static class NttsAudioJoiner
{
    public static byte[] Join(IReadOnlyList<(byte[] Data, double Pause)> chunks, CancellationToken cancellation = default)
    {
        var samples = new List<float>();
        var rate = 0;
        var channels = 0;
        var buffer = new float[4096];
        for (var i = 0; i < chunks.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            using var input = new MemoryStream(chunks[i].Data, false);
            using var reader = new VorbisReader(input, false);
            reader.Initialize();
            if (i == 0)
            {
                rate = reader.SampleRate;
                channels = reader.Channels;
            }
            if (reader.SampleRate != rate || reader.Channels != channels || channels is < 1 or > 2)
                throw new InvalidDataException("Inconsistent NTTS audio formats");
            var remaining = reader.TotalSamples * channels;
            if (remaining <= 0 || remaining + samples.Count > rate * channels * 120)
                throw new InvalidDataException("Empty or excessive NTTS audio");
            // VorbisPizza returns frames, while its buffer contains interleaved channel values.
            // Limit reads to declared length, as in the engine loader, to avoid malformed-file decoder loops.
            while (remaining > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = reader.ReadSamples(buffer.AsSpan(0, (int) Math.Min(buffer.Length, remaining))) * channels;
                if (read == 0)
                    break;
                samples.AddRange(buffer.Take(read));
                remaining -= read;
            }
            if (i < chunks.Count - 1)
                samples.AddRange(new float[(int) Math.Round(rate * chunks[i].Pause) * channels]);
        }
        var planar = new float[channels][];
        var frames = samples.Count / channels;
        for (var channel = 0; channel < channels; channel++)
        {
            planar[channel] = new float[frames];
            for (var frame = 0; frame < frames; frame++)
                planar[channel][frame] = samples[frame * channels + channel];
        }
        return Encode(planar, rate, cancellation);
    }

    public static byte[] Encode(float[][] samples, int sampleRate, CancellationToken cancellation = default)
    {
        using var output = new MemoryStream();
        var info = VorbisInfo.InitVariableBitRate(samples.Length, sampleRate, 0.9f);
        var stream = new OggStream(1);
        stream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        stream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
        stream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        WritePages(stream, output, true);
        var encoder = ProcessingState.Create(info);
        // Encoder 1.2.2 starts its first window in the input buffer: supply the pre-roll so no speech is discarded.
        var preRoll = info.CodecSetup.BlockSizes[1] / 2;
        encoder.WriteData(Enumerable.Range(0, samples.Length).Select(_ => new float[preRoll]).ToArray(), preRoll);
        for (var offset = 0; offset < samples[0].Length; offset += 512)
        {
            cancellation.ThrowIfCancellationRequested();
            encoder.WriteData(samples, Math.Min(512, samples[0].Length - offset), offset);
            while (!stream.Finished && encoder.PacketOut(out var packet))
            {
                stream.PacketIn(packet);
                WritePages(stream, output, false);
            }
        }
        encoder.WriteEndOfStream();
        while (!stream.Finished && encoder.PacketOut(out var packet))
        {
            stream.PacketIn(packet);
            WritePages(stream, output, false);
        }
        WritePages(stream, output, true);
        return output.ToArray();
    }

    private static void WritePages(OggStream stream, Stream output, bool force)
    {
        while (stream.PageOut(out var page, force))
        {
            output.Write(page.Header);
            output.Write(page.Body);
        }
    }
}

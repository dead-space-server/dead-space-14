// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Content.Client.DeadSpace.Audio.NowPlaying;

public static class VorbisCommentMetadata
{
    private const int PageHeaderSize = 27;
    private const int CommentHeaderSize = 7;
    private const int CommentPacketIndex = 1;
    private const byte CommentPacketType = 0x03;

    private static readonly byte[] OggSignature = { (byte) 'O', (byte) 'g', (byte) 'g', (byte) 'S' };

    private static readonly string[] TitleKeys = { "TITLE", "NAME" };

    private static readonly string[] ArtistKeys = { "ARTIST", "AUTHOR", "PERFORMER", "COMPOSER" };

    private static readonly string[] PictureKeys = { "METADATA_BLOCK_PICTURE", "COVERART" };

    public static bool TryReadMetadata(
        ReadOnlySpan<byte> data,
        out string? title,
        out string? artist,
        out byte[]? cover)
    {
        title = null;
        artist = null;
        cover = null;

        var packet = FindCommentPacket(data);
        if (packet == null || packet.Length <= CommentHeaderSize || packet[0] != CommentPacketType)
            return false;

        var index = CommentHeaderSize;

        if (!TryReadUInt32(packet, ref index, out var vendorLength) || vendorLength > packet.Length - index)
            return false;

        index += (int) vendorLength;

        if (!TryReadUInt32(packet, ref index, out var commentCount) || commentCount > 4096)
            return false;

        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0u; i < commentCount; i++)
        {
            if (!TryReadUInt32(packet, ref index, out var length) || length > packet.Length - index)
                break;

            var entry = Encoding.UTF8.GetString(packet, index, (int) length);
            index += (int) length;

            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
                continue;

            var key = entry[..separator].Trim();
            var value = entry[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            tags.TryAdd(key, value);
        }

        title = PickFirst(tags, TitleKeys);
        artist = PickFirst(tags, ArtistKeys);

        foreach (var key in PictureKeys)
        {
            if (!tags.TryGetValue(key, out var encoded))
                continue;

            cover = key == "COVERART"
                ? TryDecodeBase64(encoded)
                : TryDecodeFlacPicture(encoded);

            if (cover != null)
                break;
        }

        return title != null || artist != null || cover != null;
    }

    private static byte[]? FindCommentPacket(ReadOnlySpan<byte> data)
    {
        var index = 0;

        if (!data.StartsWith(OggSignature))
        {
            index = data.IndexOf(OggSignature);
            if (index < 0)
                return null;
        }

        var packetNumber = 0;
        uint? streamSerial = null;

        using var packet = new MemoryStream();

        while (index + PageHeaderSize <= data.Length)
        {
            if (!data.Slice(index, OggSignature.Length).SequenceEqual(OggSignature))
                return null;

            var pageSerial = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(index + 14, sizeof(uint)));
            var segmentCount = data[index + 26];
            var lacingOffset = index + PageHeaderSize;

            if (lacingOffset + segmentCount > data.Length)
                return null;

            var payloadOffset = lacingOffset + segmentCount;

            var payloadLength = 0;
            for (var i = 0; i < segmentCount; i++)
                payloadLength += data[lacingOffset + i];

            if (payloadOffset + payloadLength > data.Length)
                return null;

            if (streamSerial == null)
            {
                streamSerial = pageSerial;
            }
            else if (pageSerial != streamSerial)
            {
                index = payloadOffset + payloadLength;
                continue;
            }

            var offset = payloadOffset;
            for (var i = 0; i < segmentCount; i++)
            {
                var segmentLength = data[lacingOffset + i];
                packet.Write(data.Slice(offset, segmentLength));
                offset += segmentLength;

                if (segmentLength == byte.MaxValue)
                    continue;

                if (packetNumber == CommentPacketIndex)
                    return packet.ToArray();

                packetNumber++;
                packet.SetLength(0);
            }

            index = payloadOffset + payloadLength;
        }

        return null;
    }

    private static string? PickFirst(Dictionary<string, string> tags, string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var value))
                return value;
        }

        return null;
    }

    private static byte[]? TryDecodeFlacPicture(string value)
    {
        var block = TryDecodeBase64(value);
        if (block == null)
            return null;

        var index = 0;

        // тип картинки, MIME, описание
        if (!TryReadUInt32BigEndian(block, ref index, out _))
            return null;

        if (!TryReadUInt32BigEndian(block, ref index, out var mimeLength) || mimeLength > block.Length - index)
            return null;

        index += (int) mimeLength;

        if (!TryReadUInt32BigEndian(block, ref index, out var descriptionLength) ||
            descriptionLength > block.Length - index)
        {
            return null;
        }

        index += (int) descriptionLength;

        // ширина, высота, глубина цвета, количество цветов
        if (index + 16 > block.Length)
            return null;

        index += 16;

        if (!TryReadUInt32BigEndian(block, ref index, out var dataLength) ||
            dataLength == 0 ||
            dataLength > block.Length - index)
        {
            return null;
        }

        var image = new byte[(int) dataLength];
        Array.Copy(block, index, image, 0, (int) dataLength);
        return image;
    }

    private static byte[]? TryDecodeBase64(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 0 ? null : bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryReadUInt32(byte[] data, ref int index, out uint value)
    {
        value = 0;

        if (index < 0 || index + sizeof(uint) > data.Length)
            return false;

        value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index, sizeof(uint)));
        index += sizeof(uint);
        return true;
    }

    private static bool TryReadUInt32BigEndian(byte[] data, ref int index, out uint value)
    {
        value = 0;

        if (index < 0 || index + sizeof(uint) > data.Length)
            return false;

        value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(index, sizeof(uint)));
        index += sizeof(uint);
        return true;
    }
}

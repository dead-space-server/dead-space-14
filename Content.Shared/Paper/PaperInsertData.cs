// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

[Serializable, NetSerializable]
public sealed class PaperInsertDataRequestMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class PaperInsertDataResponseMessage : BoundUserInterfaceMessage
{
    public readonly string? StationName;

    public readonly string CharacterName;

    public readonly string? CharacterJob;

    public readonly List<CrewManifestEntry> Manifest;

    public readonly PaperInsertManifestStatus ManifestStatus;

    public PaperInsertDataResponseMessage(
        string? stationName,
        string characterName,
        string? characterJob,
        List<CrewManifestEntry> manifest,
        PaperInsertManifestStatus manifestStatus)
    {
        StationName = stationName;
        CharacterName = characterName;
        CharacterJob = characterJob;
        Manifest = manifest;
        ManifestStatus = manifestStatus;
    }
}

[Serializable, NetSerializable]
public enum PaperInsertManifestStatus : byte
{
    Available,
    RequiresPda,
    Unavailable,
}

using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Ports.Jukebox;

[Serializable, NetSerializable]
public sealed class JukeboxStopRequest : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxRepeatToggled(bool newState) : BoundUserInterfaceMessage
{
    public bool NewState { get; } = newState;
}

[Serializable, NetSerializable]
public sealed class JukeboxEjectRequest : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum JukeboxUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum TapeCreatorUIKey : byte
{
    Key
}

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState]
public sealed partial class WhiteJukeboxComponent : Component
{
    public static string JukeboxContainerName = "jukebox_tapes";
    public static string JukeboxDefaultSongsName = "jukebox_default_tapes";

    [ViewVariables(VVAccess.ReadOnly)]
    public Container TapeContainer = default!;

    [DataField]
    public List<string> DefaultTapes = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public Container DefaultSongsContainer = default!;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool Playing { get; set; } = true;

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float Volume { get; set; }

    [DataField]
    public float MaxAudioRange { get; set; } = 20f;

    [DataField]
    public float RolloffFactor { get; set; } = 0.3f;

    [AutoNetworkedField]
    public PlayingSongData? PlayingSongData { get; set; }
}

[Serializable, NetSerializable]
public sealed class PlayingSongData
{
    public ResPath? SongPath;
    public TimeSpan StartedAt;
    public TimeSpan? EndsAt;
    public string? SongName;
    public float PlaybackPosition;
    public float ActualSongLengthSeconds;
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class JukeboxSong
{
    [DataField]
    public string? SongName;

    [DataField("path")]
    public ResPath? SongPath;
}

[Serializable, NetSerializable]
public sealed class JukeboxRequestSongPlay : EntityEventArgs
{
    public string? SongName { get; set; }

    public ResPath? SongPath { get; set; }

    public NetEntity? Jukebox { get; set; }

    public float SongDuration { get; set; }
}

[Serializable, NetSerializable]
public sealed class JukeboxRequestStop : EntityEventArgs
{
    public NetEntity? JukeboxUid { get; set; }
}

[Serializable, NetSerializable]
public sealed class JukeboxStopPlaying : EntityEventArgs
{
    public NetEntity? JukeboxUid { get; set; }
}

public sealed class JukeboxSongUploadRequest
{
    public string SongName = string.Empty;
    public byte[] SongBytes = Array.Empty<byte>();
    public NetEntity TapeCreatorUid = default!;
}

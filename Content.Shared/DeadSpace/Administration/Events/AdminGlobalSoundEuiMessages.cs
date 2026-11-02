// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Administration.Events;

[Serializable, NetSerializable]
public sealed class AdminGlobalSoundEuiState : EuiStateBase
{
    public string CurrentPath;
    public List<AdminGlobalSoundEntry> Entries;
    public string? CurrentTrack;
    public bool Paused;
    public string? Error;
    public List<AdminGlobalSoundQueueEntry> Queue;

    public AdminGlobalSoundEuiState(
        string currentPath,
        List<AdminGlobalSoundEntry> entries,
        string? currentTrack,
        bool paused,
        string? error,
        List<AdminGlobalSoundQueueEntry> queue)
    {
        CurrentPath = currentPath;
        Entries = entries;
        CurrentTrack = currentTrack;
        Paused = paused;
        Error = error;
        Queue = queue;
    }
}

[Serializable, NetSerializable]
public readonly record struct AdminGlobalSoundEntry(string Name, string Path, bool IsDirectory);

[Serializable, NetSerializable]
public readonly record struct AdminGlobalSoundQueueEntry(string Path);

public static class AdminGlobalSoundEuiMessage
{
    [Serializable, NetSerializable]
    public sealed class Browse : EuiMessageBase
    {
        public string Path = default!;
    }

    [Serializable, NetSerializable]
    public sealed class Play : EuiMessageBase
    {
        public string Path = default!;
        public int Volume;
        public string CKey = "";
    }

    [Serializable, NetSerializable]
    public sealed class Control : EuiMessageBase
    {
        public AdminGlobalSoundControl Action;
    }

    [Serializable, NetSerializable]
    public sealed class QueueAdd : EuiMessageBase
    {
        public string Path = default!;
        public int Volume;
        public string CKey = "";
    }

    [Serializable, NetSerializable]
    public sealed class QueueRemove : EuiMessageBase
    {
        public int Index;
    }

    [Serializable, NetSerializable]
    public sealed class QueueClear : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class QueueSkip : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class QueueStop : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class QueueFolder : EuiMessageBase
    {
        public string Path = default!;
        public int Volume;
        public string CKey = "";
        public bool Shuffle;
    }
}

[Serializable, NetSerializable]
public enum AdminGlobalSoundControl : byte
{
    Pause,
    Resume,
    Stop,
    FadeOut,
}

[Serializable, NetSerializable]
public sealed class AdminSoundPlaybackControlEvent : EntityEventArgs
{
    public int StreamId;
    public AdminGlobalSoundControl Action;
    public float FadeDuration;

    public AdminSoundPlaybackControlEvent(int streamId, AdminGlobalSoundControl action, float fadeDuration = 0f)
    {
        StreamId = streamId;
        Action = action;
        FadeDuration = fadeDuration;
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public sealed class LavalandWellWaveTimerEvent : EntityEventArgs
{
    public int Seconds;
    public bool Warning;
    public LavalandWellWaveTimerEvent() { }
    public LavalandWellWaveTimerEvent(int seconds, bool warning)
    {
        Seconds = seconds;
        Warning = warning;
    }
}

[Serializable, NetSerializable]
public sealed class LavalandWellWaveTimerHideEvent : EntityEventArgs;

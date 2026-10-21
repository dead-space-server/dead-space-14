// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Megafauna went into absolute rage: a red, static-ridden announcement across the screen.</summary>
[Serializable, NetSerializable]
public sealed class LavalandBossRageAnnouncementEvent(string text, float duration) : EntityEventArgs
{
    public readonly string Text = text;
    public readonly float Duration = duration;
}

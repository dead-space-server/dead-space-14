// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>A flash of lightning over the whole sky: the screen goes white for a moment and fades back.</summary>
[Serializable, NetSerializable]
public sealed class LavalandSkyFlashEvent(float duration, float strength) : EntityEventArgs
{
    public readonly float Duration = duration;

    /// <summary>Opacity of the white at its brightest, 0-1.</summary>
    public readonly float Strength = strength;
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Which of the pair is shown: Cain (red, the dash to the dagger) or Abel (blue, the ring of ice).</summary>
[Serializable, NetSerializable]
public enum LavalandCainAndAbelVisuals : byte
{
    Cain,
}

[Serializable, NetSerializable]
public enum LavalandCainAndAbelLayers : byte
{
    Base,
}

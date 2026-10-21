// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>The mad miner rose again as a necromorph: its claws show.</summary>
[Serializable, NetSerializable]
public enum LavalandMadMinerVisuals : byte
{
    Necromorph,
}

[Serializable, NetSerializable]
public enum LavalandMadMinerLayers : byte
{
    ClawLeft,
    ClawRight,
}

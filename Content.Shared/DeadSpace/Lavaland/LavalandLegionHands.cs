// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandLegionHandVisuals : byte
{
    Pose,
}

/// <summary>What a hand of the Legion looks like right now.</summary>
[Serializable, NetSerializable]
public enum LavalandLegionHandPose : byte
{
    /// <summary>Open palm: floating, slamming the floor, grabbing.</summary>
    Open,
    /// <summary>Clenched and drawn back for a punch.</summary>
    Fist,
    /// <summary>Knuckles first: the punch landing.</summary>
    Punch,
    /// <summary>Palm spread wide: holding and hurling something at a player.</summary>
    Throw,
}

public enum LavalandLegionHandLayers : byte
{
    Base,
}

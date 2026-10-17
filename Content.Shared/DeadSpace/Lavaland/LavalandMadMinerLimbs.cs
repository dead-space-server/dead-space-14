// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>The limbs of the mad miner's one-piece sprite, the character's own left and right.</summary>
[Flags, Serializable, NetSerializable]
public enum LavalandMadMinerLimb : byte
{
    None = 0,
    ArmRight = 1 << 0,
    ArmLeft = 1 << 1,
    LegRight = 1 << 2,
    LegLeft = 1 << 3,
}

/// <summary>
/// The mad miner risen as a necromorph: these limbs are gone from it. Its sprite is one piece, so the client cuts
/// them out of it with a shader instead of hiding body layers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandMadMinerLimbsComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandMadMinerLimb Severed;
}

/// <summary>A limb torn off the mad miner: its sprite again, with everything but that limb cut away.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandMadMinerSeveredLimbComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandMadMinerLimb Limb = LavalandMadMinerLimb.ArmRight;
}

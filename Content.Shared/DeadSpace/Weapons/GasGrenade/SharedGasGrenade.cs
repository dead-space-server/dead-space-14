// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Weapons.GasGrenade;

[Serializable, NetSerializable]
public enum GasGrenadeMode : byte
{
    Mix,

    Spray,
}

[Serializable, NetSerializable]
public enum GasGrenadeVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum GasGrenadeVisualState : byte
{
    MixIdle,

    SprayIdle,

    Releasing,
}

using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaRespawnCapsuleComponent : Component
{
    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [DataField]
    public float RespawnTime = 5f;

    [ViewVariables]
    public float Timer = 0f;

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}

[Serializable, NetSerializable]
public enum NinjaRespawnCapsuleVisuals : byte
{
    Full
}
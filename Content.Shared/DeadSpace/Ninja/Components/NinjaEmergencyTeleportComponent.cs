using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaEmergencyTeleportComponent : Component
{
    [DataField]
    public EntProtoId TeleportAction = "NinjaEmergencyTeleportAction";

    [DataField, AutoNetworkedField]
    public EntityUid? TeleportActionEntity;

    [DataField, AutoNetworkedField]
    public Vector2 TeleportRadius = new(10f, 15f);

    [DataField, AutoNetworkedField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public float EnergyCost = 120f;
}

public sealed partial class NinjaEmergencyTeleportEvent : InstantActionEvent;
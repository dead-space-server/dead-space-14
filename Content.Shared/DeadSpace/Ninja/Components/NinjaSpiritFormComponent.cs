using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class NinjaSpiritFormComponent : Component
{
    [DataField]
    public EntProtoId SpiritFormAction = "NinjaSpiritFormAction";

    [DataField, AutoNetworkedField]
    public EntityUid? SpiritFormActionEntity;

    [DataField]
    public float EnergyDrainPercent = 0.25f;

    [DataField, AutoNetworkedField]
    public bool SpiritFormActive;

    [DataField]
    public SoundSpecifier? ActivateSound;

    [DataField]
    public SoundSpecifier? DeactivateSound;

    [DataField]
    public Color SpiritFormColor = Color.FromHex("#00d400");

    public float DrainAccumulator;
}

public sealed partial class NinjaSpiritFormEvent : InstantActionEvent;
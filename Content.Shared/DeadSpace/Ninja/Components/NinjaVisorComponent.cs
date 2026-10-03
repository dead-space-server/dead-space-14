using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class NinjaVisorComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaVisorMode Mode = NinjaVisorMode.Protective;

    [DataField]
    public EntProtoId CycleAction = "ActionCycleNinjaVisor";

    [DataField, AutoNetworkedField]
    public EntityUid? CycleActionEntity;

    [DataField]
    public SoundSpecifier? CycleSound = null;

    [DataField]
    public SoundSpecifier? ThermalActivateSound = null;

    [DataField]
    public SoundSpecifier? ThermalActivateOffSound = null;

    [DataField]
    public bool ThermalAnimation = true;

    [DataField]
    public bool ThermalUseShader = true;

    [DataField]
    public Color? NightVisionColor = null;

    [DataField]
    public SoundSpecifier? NightVisionActivateSound = null;

    [DataField]
    public bool NightVisionAnimation = true;

    [DataField]
    public float? NightVisionDesaturation = null;

    [ViewVariables(VVAccess.ReadOnly)]
    public NinjaVisorMode? AppliedEffect;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? AppliedWearer;
}

public enum NinjaVisorMode : byte
{
    Protective,
    Thermal,
    NightVision,
}

public sealed partial class CycleNinjaVisorActionEvent : InstantActionEvent { }
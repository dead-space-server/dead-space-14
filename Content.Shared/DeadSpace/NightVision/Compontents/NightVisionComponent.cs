using Content.Shared.Actions;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Components.NightVision;

[RegisterComponent, NetworkedComponent]
public sealed partial class NightVisionComponent : Component
{
    [DataField]
    public EntProtoId ActionToggleNightVision = "ActionToggleNightVision";

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public EntityUid? ActionToggleNightVisionEntity;

    [DataField]
    public bool IsNightVision;

    /// <description>
    /// Used to ensure that this doesn't break with sandbox or admin tools.
    /// This is not "enabled/disabled".
    /// </description>
    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool LightSetup = false;

    /// <description>
    /// Gives an extra frame of nighyvision to reenable light manager during
    /// </description>
    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool GraceFrame = false;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Color Color = new Color(80f / 255f, 220f / 255f, 70f / 255f, 0.2f);

    public NightVisionComponent(Color? color = null)
    {
        Color = color ?? new Color(80f / 255f, 220f / 255f, 70f / 255f, 0.2f);
    }

}

public sealed partial class ToggleNightVisionActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed class NightVisionComponentState : ComponentState
{
    public bool IsNightVision;

    public NightVisionComponentState(bool isNightVision)
    {
        IsNightVision = isNightVision;
    }
}

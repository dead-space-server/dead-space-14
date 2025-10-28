using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.NightVision;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class NightVisionComponent : Component
{
    [DataField]
    public EntProtoId ActionToggleNightVision = "ActionToggleNightVision";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionToggleNightVisionEntity;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    [AutoNetworkedField]
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

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Color Color = new Color(80f / 255f, 220f / 255f, 70f / 255f, 0.2f);
}

public sealed partial class ToggleNightVisionActionEvent : InstantActionEvent { }

/// <summary>
///     Raised directed at an entity to see whether the entity is currently have night vision or not.
/// </summary>
public sealed class CanNightVisionAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public bool NightVision => Cancelled;
    public SlotFlags TargetSlots => SlotFlags.EYES | SlotFlags.MASK | SlotFlags.HEAD;
}
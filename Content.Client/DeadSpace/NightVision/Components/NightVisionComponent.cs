using Content.Shared.DeadSpace.NightVision;
using Robust.Shared.GameStates;

namespace Content.Client.DeadSpace.Components.NightVision;

[NetworkedComponent]
public sealed partial class NightVisionComponent : SharedNightVisionComponent
{
    [DataField]
    public bool IsNightVision;

    /// <description>
    ///     Used to ensure that this doesn't break with sandbox or admin tools.
    ///     This is not "enabled/disabled".
    /// </description>
    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool LightSetup = false;

    /// <description>
    ///     Gives an extra frame of nighyvision to reenable light manager during
    /// </description>
    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool GraceFrame = false;

}

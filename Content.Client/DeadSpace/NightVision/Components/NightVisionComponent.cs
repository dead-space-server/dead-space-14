using Content.Shared.DeadSpace.NightVision;

namespace Content.Client.DeadSpace.Components.NightVision;

[RegisterComponent]
public sealed partial class NightVisionComponent : SharedNightVisionComponent
{
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

    /// <description>
    ///     Время до обновления состояния
    /// </description>
    [ViewVariables(VVAccess.ReadOnly)]
    public uint ClientLastToggleTick;

    /// <description>
    ///     Время активации
    /// </description>
    [ViewVariables(VVAccess.ReadOnly)]
    public uint ServerLastToggleTick;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool IsToggled = false;

}

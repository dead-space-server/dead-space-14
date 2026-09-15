using Robust.Shared.GameStates;

namespace Content.Shared.Power.Components;

[NetworkedComponent]
public abstract partial class SharedApcPowerReceiverComponent : Component
{
    /// <summary>
    /// If true, this entity either doesn't need power, or is currently receiving the power it needs.
    /// </summary>
    [ViewVariables]
    public bool Powered;

    /// <summary>
    /// When false, causes this to appear powered even if not receiving power from an Apc.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] // DS14
    public virtual bool NeedsPower { get; set; } = true;

    /// <summary>
    /// When true, causes this to never appear powered.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)] // DS14
    public virtual bool PowerDisabled { get; set; }

    /// <summary>
    /// Amount of power this needs from an APC to function, in watts.
    /// </summary>
    public abstract float Load { get; set; } // DS14
}

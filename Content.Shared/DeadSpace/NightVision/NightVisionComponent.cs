using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.NightVision;

[NetworkedComponent]
public abstract partial class SharedNightVisionComponent : Component
{
    [DataField]
    public bool IsNightVision;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Color Color = new Color(80f / 255f, 220f / 255f, 70f / 255f, 0.2f);
}

public sealed partial class ToggleNightVisionActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed class NightVisionComponentState : ComponentState
{
    public Color Color;
    public bool IsNightVision;

    public NightVisionComponentState(Color color, bool isNightVision)
    {
        Color = color;
        IsNightVision = isNightVision;
    }
}

using Content.Shared.Charges.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Charges.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedChargesSystem))]
[AutoGenerateComponentState]
public sealed partial class LimitedChargesComponent : Component
{
    /// DeadSpace
    public LimitedChargesComponent(int max, int count)
    {
        Charges = count;
        MaxCharges = max;
    }

    /// <summary>
    /// The maximum number of charges
    /// </summary>
    [DataField("maxCharges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public int MaxCharges = 3;

    /// <summary>
    /// The current number of charges
    /// </summary>
    [DataField("charges"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public int Charges = 3;
}

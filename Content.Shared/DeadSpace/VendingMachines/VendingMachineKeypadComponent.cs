namespace Content.Shared.DeadSpace.VendingMachines;

[RegisterComponent]
public sealed partial class VendingMachineKeypadComponent : Component
{
    [DataField]
    public int Columns = 4;

    [DataField]
    public int SpiralCapacity = 8;
}

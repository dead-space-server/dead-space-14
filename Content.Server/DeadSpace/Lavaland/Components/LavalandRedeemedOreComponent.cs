namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandRedeemedOreComponent : Component
{
    [DataField]
    public int ProcessedUnits;

    [DataField]
    public int CreditedUnits;

    [DataField]
    public int OwedUnits;
}

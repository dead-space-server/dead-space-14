namespace Content.Server.DeadSpace.MutilatedCorpse;

/// <summary>
/// This is used for changes the character's name to unknown if there is a lot of damage
/// </summary>
[RegisterComponent]
public sealed partial class MutilatedCorpseComponent : Component
{
    //What type of damage will change the character's name
    [DataField]
    public string TypeDamage = "Slash";

    //How much damage of this type is required to change the name
    [DataField]
    public int AmountDamageForMutilated = 200;

    [DataField]
    public LocId LocIdChangedName = "copy-loc-SalvageHumanCorpse";

    public string RealName = string.Empty;

    public string ChangedName = string.Empty;
}

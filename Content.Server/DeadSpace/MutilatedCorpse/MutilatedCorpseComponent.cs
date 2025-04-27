using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MutilatedCorpse;

/// <summary>
/// This is used for changes the character's name to unknown if there is a lot of damage
/// </summary>
[RegisterComponent]
public sealed partial class MutilatedCorpseComponent : Component
{
    /// <summary>
    /// What type of damage will change the character's name
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Slash";

    /// <summary>
    /// How much damage of this type is required to change the name
    /// </summary>
    [DataField]
    public int AmountDamageForMutilated = 200;

    [DataField]
    public LocId LocIdChangedName = "copy-loc-SalvageHumanCorpse";

    public string RealName = string.Empty;

    public string ChangedName = string.Empty;
}

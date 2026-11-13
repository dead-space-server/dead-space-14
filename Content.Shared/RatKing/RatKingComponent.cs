using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.RatKing;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedRatKingSystem))]
[AutoGenerateComponentState]
public sealed partial class RatKingComponent : Component
{
    [DataField("actionRaiseArmy")]
    public string ActionRaiseArmy = "ActionRatKingRaiseArmy";

    /// <summary>
    ///     The action for the Raise Army ability
    /// </summary>
    [DataField("actionRaiseArmyEntity")]
    public EntityUid? ActionRaiseArmyEntity;

    /// <summary>
    ///     The amount of hunger one use of Raise Army consumes
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("hungerPerArmyUse", required: true)]
    public float HungerPerArmyUse = 25f;

    /// <summary>
    ///     The entity prototype of the mob that Raise Army summons
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("armyMobSpawnId")]
    public string ArmyMobSpawnId = "MobRatServant";

    [DataField("actionDomain")]
    public string ActionDomain = "ActionRatKingDomain";

    /// <summary>
    ///     The action for the Domain ability
    /// </summary>
    [DataField("actionDomainEntity")]
    public EntityUid? ActionDomainEntity;

    /// <summary>
    ///     The amount of hunger one use of Domain consumes
    /// </summary>
    [DataField("hungerPerDomainUse", required: true), ViewVariables(VVAccess.ReadWrite)]
    public float HungerPerDomainUse = 50f;

    /// <summary>
    ///     How many moles of ammonia are released after one us of Domain
    /// </summary>
    [DataField("molesAmmoniaPerDomain"), ViewVariables(VVAccess.ReadWrite)]
    public float MolesAmmoniaPerDomain = 200f;

    /// <summary>
    /// The current order that the Rat King assigned.
    /// </summary>
    [DataField("currentOrders"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public RatKingOrderType CurrentOrder = RatKingOrderType.Follow;

    /// <summary>
    /// The servants that the rat king is currently controlling
    /// </summary>
    [DataField("servants")]
    public HashSet<EntityUid> Servants = new();

    [DataField("actionOrderStay")]
    public string ActionOrderStay = "ActionRatKingOrderStay";

    [DataField("actionOrderStayEntity")]
    public EntityUid? ActionOrderStayEntity;

    [DataField("actionOrderFollow")]
    public string ActionOrderFollow = "ActionRatKingOrderFollow";

    [DataField("actionOrderFollowEntity")]
    public EntityUid? ActionOrderFollowEntity;

    [DataField("actionOrderCheeseEm")]
    public string ActionOrderCheeseEm = "ActionRatKingOrderCheeseEm";

    [DataField("actionOrderCheeseEmEntity")]
    public EntityUid? ActionOrderCheeseEmEntity;

    [DataField("actionOrderLoose")]
    public string ActionOrderLoose = "ActionRatKingOrderLoose";

    [DataField("actionOrderLooseEntity")]
    public EntityUid? ActionOrderLooseEntity;

    /// <summary>
    /// A dictionary with an order type to the corresponding callout dataset.
    /// </summary>
    [DataField("orderCallouts")]
    public Dictionary<RatKingOrderType, string> OrderCallouts = new()
    {
        { RatKingOrderType.Stay, "RatKingCommandStay" },
        { RatKingOrderType.Follow, "RatKingCommandFollow" },
        { RatKingOrderType.CheeseEm, "RatKingCommandCheeseEm" },
        { RatKingOrderType.Loose, "RatKingCommandLoose" }
    };

    // DS14-necro-start
    [DataField][ViewVariables(VVAccess.ReadOnly)]
    public bool NeedHunger = true;
    // DS14-necro-end
}

[Serializable, NetSerializable]
public enum RatKingOrderType : byte
{
    Stay,
    Follow,
    CheeseEm,
    Loose
}

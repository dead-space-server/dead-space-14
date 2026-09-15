using Content.Shared.Objectives;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Thief.Prototypes;

/// <summary>
/// DS14: Defines a type of goods that the ВорПРО program can request from a thief.
/// Uses <see cref="StealTargetGroupPrototype"/> groups taken from the old thief objectives.
/// </summary>
[Prototype]
public sealed partial class ThiefRequestGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The steal target group that items must belong to in order to be sold.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StealTargetGroupPrototype> Group;

    /// <summary>
    /// Relative chance for this group to be picked when generating an offer.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Minimum amount of items requested.
    /// </summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>
    /// Maximum amount of items requested.
    /// </summary>
    [DataField]
    public int MaxCount = 1;

    /// <summary>
    /// Payment in dirty credits for a single item of this group.
    /// </summary>
    [DataField]
    public int PricePerItem = 1000;

    /// <summary>
    /// Delivery time limit in minutes. Delivering within the limit grants a bonus,
    /// delivering after it reduces the price (see ThiefPdaProgramComponent).
    /// </summary>
    [DataField]
    public int TimeLimitMinutes = 15;
}

/// <summary>
/// DS14: An item (or bundle of items) that can be bought with dirty credits
/// through the ВорПРО uplink tab.
/// </summary>
[Prototype]
public sealed partial class ThiefListingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name of the listing.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Localized description of the listing. Null means no description (optional).
    /// </summary>
    [DataField]
    public LocId? Description = null;

    /// <summary>
    /// Entities spawned on purchase, in order.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Entities = new();

    /// <summary>
    /// Cost in dirty credits.
    /// </summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>
    /// Localized category name used to group listings in the uplink UI.
    /// </summary>
    [DataField]
    public LocId Category = "thief-program-category-misc";
}

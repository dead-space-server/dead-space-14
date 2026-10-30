// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Store;
using Content.Shared.Store;

namespace Content.Server.Store.Conditions;

/// <summary>
/// A stock shared by every store in the round: bought out from one uplink, the listing is gone from all the others.
/// </summary>
public sealed partial class GlobalLimitedStockCondition : ListingCondition
{
    [DataField(required: true)]
    public int Stock;

    /// <summary>Listings with the same group share one stock; by default each listing has its own.</summary>
    [DataField]
    public string? Group;

    public override bool Condition(ListingConditionArgs args)
    {
        var bought = args.EntityManager.System<GlobalListingStockSystem>().GetBought(Group ?? args.Listing.ID);
        args.Listing.RemainingStock = Math.Max(Stock - bought, 0);
        return args.Listing.RemainingStock > 0;
    }
}

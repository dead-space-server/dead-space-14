// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Store.Conditions;

/// <summary>
/// Blocks a listing if any of the blacklisted listings have already been purchased from this store.
/// </summary>
public sealed partial class PurchasedBlacklistCondition : ListingCondition
{
    [DataField(required: true)]
    public HashSet<ProtoId<ListingPrototype>> Blacklist = new();

    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<StoreComponent>(args.StoreEntity, out var storeComp))
            return false;

        foreach (var blockedListing in Blacklist)
        {
            foreach (var listing in storeComp.FullListingsCatalog)
            {
                if (listing.ID == blockedListing.Id && listing.PurchaseAmount > 0)
                    return false;
            }
        }

        return true;
    }
}

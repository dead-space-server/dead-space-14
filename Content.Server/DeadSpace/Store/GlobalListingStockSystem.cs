// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Store.Conditions;
using Content.Server.Store.Systems;
using Content.Shared.GameTicking;

namespace Content.Server.DeadSpace.Store;

/// <summary>
/// Counts the round's purchases of listings with a <see cref="GlobalLimitedStockCondition"/>, across every store.
/// </summary>
public sealed class GlobalListingStockSystem : EntitySystem
{
    private readonly Dictionary<string, int> _bought = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StoreBuyFinishedEvent>(OnBuyFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public int GetBought(string key)
    {
        return _bought.GetValueOrDefault(key);
    }

    private void OnBuyFinished(ref StoreBuyFinishedEvent ev)
    {
        foreach (var condition in ev.PurchasedItem.Conditions ?? new())
        {
            if (condition is not GlobalLimitedStockCondition global)
                continue;

            var key = global.Group ?? ev.PurchasedItem.ID;
            _bought[key] = GetBought(key) + 1;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _bought.Clear();
    }
}

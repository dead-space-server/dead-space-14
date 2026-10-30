// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.GameTicking;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Store.Conditions;

/// <summary>
/// The listing can't be bought while any of these game rules is running (a reinforcement is called once a round).
/// </summary>
public sealed partial class GameRuleNotActiveCondition : ListingCondition
{
    [DataField(required: true)]
    public List<EntProtoId> Rules = new();

    public override bool Condition(ListingConditionArgs args)
    {
        var ticker = args.EntityManager.System<GameTicker>();
        foreach (var rule in Rules)
        {
            if (ticker.IsGameRuleActive(rule))
                return false;
        }

        return true;
    }
}

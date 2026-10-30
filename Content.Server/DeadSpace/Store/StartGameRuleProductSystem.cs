// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.GameTicking;
using Content.Shared.DeadSpace.Store;

namespace Content.Server.DeadSpace.Store;

/// <summary>
/// Starts the game rule bought from a store (<see cref="StartGameRuleProductEvent"/>). The store has already checked the
/// listing's conditions and taken the price on the server; a rule already running is not started twice.
/// </summary>
public sealed class StartGameRuleProductSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StartGameRuleProductEvent>(OnPurchased);
    }

    private void OnPurchased(StartGameRuleProductEvent ev)
    {
        if (_ticker.IsGameRuleActive(ev.Rule))
            return;

        _ticker.StartGameRule(ev.Rule);
    }
}

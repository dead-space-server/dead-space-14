// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Medical;
using Content.Shared.DeadSpace.AntiAlcohol;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AntiAlcohol;

public sealed class AntiAlcoholSystem : EntitySystem
{
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecuteEntityEffectEvent<AntiAlcoholImplantEffect>>(OnExecuteAntiAlcoholEffect);
    }

    private void OnExecuteAntiAlcoholEffect(ref ExecuteEntityEffectEvent<AntiAlcoholImplantEffect> args)
    {
        if (args.Args is not EntityEffectReagentArgs reagentArgs)
            return;

        if (!TryComp(reagentArgs.TargetEntity, out AntiAlcoholWatcherComponent? watcher))
            return;

        if (reagentArgs.Reagent is not { } reagent || reagentArgs.Source is not { } solution)
            return;

        var reagentId = reagent.ID;
        var ethanolAmount = solution.GetTotalPrototypeQuantity(reagentId);
        var threshold = FixedPoint2.New(watcher.Threshold);
        if (ethanolAmount < threshold)
            return;

        if (_timing.CurTime < watcher.NextAllowedVomitAt || !_random.Prob(watcher.Probability))
            return;

        watcher.NextAllowedVomitAt = _timing.CurTime + TimeSpan.FromSeconds(watcher.CooldownSeconds);
        var target = reagentArgs.TargetEntity;
        var delay = TimeSpan.FromSeconds(0.5);
        Timer.Spawn(delay, () =>
        {
            try
            {
                if (Deleted(target))
                    return;

                _vomit.Vomit(target);
            }
            catch (Exception e)
            {
                Logger.WarningS("anti_alcohol", $"Failed to force vomit on entity {target}: {e}");
            }
        });
    }
}

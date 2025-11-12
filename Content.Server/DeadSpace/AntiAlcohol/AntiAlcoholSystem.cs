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

        if (reagentArgs.Reagent is not { } reagent)
            return;

        if (reagent.ID != watcher.EthanolId)
            return;

        if (reagentArgs.Source is not { } solution)
            return;

        var ethanolAmount = solution.GetTotalPrototypeQuantity(watcher.EthanolId);
        var threshold = FixedPoint2.New(watcher.Threshold);
        if (ethanolAmount <= FixedPoint2.Zero || ethanolAmount < threshold)
            return;

        reagentArgs.Scale = FixedPoint2.Zero;
        solution.RemoveReagent(watcher.EthanolId, ethanolAmount);

        if (_timing.CurTime < watcher.NextAllowedVomitAt)
            return;

        if (!_random.Prob(watcher.Probability))
            return;

        _vomit.Vomit(reagentArgs.TargetEntity);
        watcher.NextAllowedVomitAt = _timing.CurTime + TimeSpan.FromSeconds(watcher.CooldownSeconds);
    }
}

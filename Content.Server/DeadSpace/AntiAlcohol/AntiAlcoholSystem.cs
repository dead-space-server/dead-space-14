using System;
using System.Collections.Generic;
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
    private readonly List<(EntityUid Target, TimeSpan ExecuteAt)> _pendingVomit = new();
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

        if (!_random.Prob(watcher.Probability))
            return;

        var target = reagentArgs.TargetEntity;


        var delay = TimeSpan.FromSeconds(watcher.VomitDelaySeconds);
        var executeAt = _timing.CurTime + delay;


        foreach (var pending in _pendingVomit)
        {
            if (pending.Target == target)
                return;
        }

        _pendingVomit.Add((target, executeAt));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingVomit.Count == 0)
            return;

        var now = _timing.CurTime;

        for (int i = _pendingVomit.Count - 1; i >= 0; i--)
        {
            var item = _pendingVomit[i];

            if (now < item.ExecuteAt)
                continue;

            _pendingVomit.RemoveAt(i);

            if (Deleted(item.Target))
                continue;

            _vomit.Vomit(item.Target);
        }
    }
}

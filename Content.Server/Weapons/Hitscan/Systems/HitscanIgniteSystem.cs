using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Random;

namespace Content.Server.Weapons.Hitscan.Systems;

public sealed class HitscanIgniteSystem : EntitySystem // DS14 - pre-v288 event/IoC style
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!; // DS14 - pre-v288 IoC
    [Dependency] private readonly FlammableSystem _flammable = default!; // DS14 - pre-v288 IoC

    //The hitscan has hit the target, rolls a chance to ignite and ignite if it succeeds.
    // DS14-Start - pre-v288 explicit event subscription
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanIgniteComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }
    // DS14-End

    private void OnHitscanHit(Entity<HitscanIgniteComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        //If the roll succeeds, the target is set on fire.
        var target = args.Data.HitEntity;
        var stackAmount = ent.Comp.FireStacks;

        if (target == null)
            return;

        //Rolls a chance for the laser to ignite the target. Cancels if it fails.
        if (!_robustRandom.Prob(ent.Comp.IgniteChance))
            return;

        if (TryComp<FlammableComponent>(target.Value, out var flamcmp))
            _flammable.AdjustFireStacks(target.Value, stackAmount, flamcmp, true);
    }

}

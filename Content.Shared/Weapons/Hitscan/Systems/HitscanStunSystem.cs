using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanStunSystem : EntitySystem
{
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanStaminaDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanStaminaDamageComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        _stamina.TakeStaminaDamage(args.Data.HitEntity.Value, hitscan.Comp.StaminaDamage, source: args.Data.Shooter ?? args.Data.Gun);

        // DS14-start: allow stamina-only hitscans to reuse normal impact effects without duplicating damage hitscans.
        if (HasComp<HitscanBasicDamageComponent>(hitscan))
            return;

        var damageEvent = new HitscanDamageDealtEvent
        {
            Target = args.Data.HitEntity.Value,
            DamageDealt = new DamageSpecifier(),
            Data = args.Data,
        };

        RaiseLocalEvent(hitscan, ref damageEvent);
        // DS14-end
    }
}

// DS14: Temperature and fire effects for hitscan weapons
using Content.Server.Atmos.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server.Weapons.Hitscan;

public sealed partial class HitscanFireSystem : EntitySystem
{
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicEffectsComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanBasicEffectsComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var hitEntity = args.Data.HitEntity.Value;

        if (ent.Comp.Temperature != 0f)
        {
            var heatAmount = ent.Comp.Temperature * 1000f * 1.5f;
            _temperature.ChangeHeat(hitEntity, heatAmount);
        }

        if (ent.Comp.FireStacks > 0f &&
            TryComp<FlammableComponent>(hitEntity, out var flammable))
        {
            _flammable.AdjustFireStacks(hitEntity, ent.Comp.FireStacks, flammable, ignite: true);
            var igniter = args.Data.Shooter ?? args.Data.Gun;
            _flammable.Ignite(hitEntity, igniter, flammable);
        }
    }
}

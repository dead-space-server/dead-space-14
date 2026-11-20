using Content.Server.Temperature.Systems;
using Content.Shared.DeadSpace.Temperature;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.DeadSpace.Temperature;

public sealed partial class ChangeTemperatureOnMeleeHitSystem : EntitySystem
{
    [Dependency] private TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangeTemperatureOnMeleeHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<ChangeTemperatureOnMeleeHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
            _temperature.ChangeHeat(target, ent.Comp.Heat, ent.Comp.IgnoreHeatResistance);
    }
}

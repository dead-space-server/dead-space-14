// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Every blow of lavaland fauna also hits structures (<see cref="LavalandFaunaComponent.StructuralDamage"/>).</summary>
public sealed class LavalandFaunaStructuralSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandFaunaComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<LavalandFaunaComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.StructuralDamage <= 0f || args.HitEntities.Count == 0)
            return;

        args.BonusDamage += new DamageSpecifier
        {
            DamageDict = new() { { "Structural", FixedPoint2.New(ent.Comp.StructuralDamage) } },
        };
    }
}

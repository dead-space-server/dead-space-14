// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Systems;

public sealed class ProjectileHostilesOnlySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileHostilesOnlyComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<ProjectileHostilesOnlyComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !HasComp<MobStateComponent>(args.OtherEntity) ||
            !TryComp<ProjectileComponent>(ent, out var projectile) ||
            !TryComp<NpcFactionMemberComponent>(projectile.Shooter, out var shooter))
        {
            return;
        }

        if (TryComp<NpcFactionMemberComponent>(args.OtherEntity, out var other) &&
            shooter.HostileFactions.Overlaps(other.Factions))
        {
            return;
        }

        args.Cancelled = true;
    }
}

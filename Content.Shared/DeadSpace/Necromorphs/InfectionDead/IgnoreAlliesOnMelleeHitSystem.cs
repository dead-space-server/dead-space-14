// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT


using Content.Shared.Weapons.Melee.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;

namespace Content.Shared.DeadSpace.Necromorphs.InfectionDead;

public sealed partial class IgnoreAlliesOnMelleeHitSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _npcFactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgnoreAlliesOnMelleeHitComponent, MeleeHitEvent>(OnMeleeHit);
    }


    private void OnMeleeHit(EntityUid uid, IgnoreAlliesOnMelleeHitComponent component, MeleeHitEvent args)
    {
        foreach (var entity in args.HitEntities)
        {
            if (args.User == entity)
                continue;

            if (_npcFactionSystem.IsEntityFriendly(uid, entity))
            {
                args.Handled = true;
                continue;
            }
        }
    }

}

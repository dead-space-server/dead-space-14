using Content.Shared.Actions;
using Content.Shared.DeadSpace.Abilities.ExplosionAbility.Components;
using Content.Shared.DeadSpace.Abilities.ExplosionAbility;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server.DeadSpace.Abilities.ExplosionAbility;

public sealed partial class ExplosionAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionAbilityComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ExplosionAbilityComponent, ExplosionAbilityActionEvent>(Explosive);
        SubscribeLocalEvent<ExplosionAbilityComponent, MobStateChangedEvent>(OnState);
    }

    private void OnState(EntityUid uid, ExplosionAbilityComponent comp, MobStateChangedEvent args)
    {
        if (_mobState.IsDead(uid))
            _explosion.QueueExplosion(uid, comp.TypeId, comp.TotalIntensity, 1f, comp.MaxTileIntensity, 1f, int.MaxValue, true, null, true);

    }
    private void OnComponentInit(EntityUid uid, ExplosionAbilityComponent comp, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref comp.ExplosionAbilityActionEntity, comp.ExplosionAbilityAction, uid);
    }

    private void Explosive(EntityUid uid, ExplosionAbilityComponent comp, ExplosionAbilityActionEvent args)
    {
        if (args.Handled)
            return;

        _explosion.QueueExplosion(uid, comp.TypeId, comp.TotalIntensity, 1f, comp.MaxTileIntensity, 1f, int.MaxValue, true, null, true);

        comp.Explosions++;
        args.Handled = true;

        if (comp.NumberExplosions == 0)
            return;

        if (comp.NumberExplosions <= comp.Explosions)
            _actionsSystem.RemoveAction(uid, comp.ExplosionAbilityActionEntity);
    }


}

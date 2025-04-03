using Content.Server.DeadSpace.Armutant.Abilities.Components.GunAbilities;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;

namespace Content.Server.DeadSpace.Armutant.Abilities.Gun;

public sealed class GunSmokeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunSmokeComponent, GunSmokeActionEvent>(OnGunBallAction);
        SubscribeLocalEvent<GunSmokeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<GunSmokeComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, GunSmokeComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionGunSmokeEntity, component.ActionToggleGunSmoke, uid);
    }

    private void OnComponentShutdown(EntityUid uid, GunSmokeComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionGunSmokeEntity);
    }
    private void OnGunBallAction(Entity<GunSmokeComponent> ent, ref GunSmokeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var effect = Spawn(ent.Comp.SmokePrototype, Transform(ent).Coordinates);
    }
}



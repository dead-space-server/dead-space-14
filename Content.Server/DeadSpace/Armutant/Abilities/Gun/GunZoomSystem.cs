using System.Numerics;
using Content.Server.DeadSpace.Armutant.Abilities.Components.GunAbilities;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Movement.Systems;

namespace Content.Server.DeadSpace.Armutant.Abilities.Gun;

public sealed class GunZoomSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunZoomComponent, GunZoomActionEvent>(OnZoomAction);
        SubscribeLocalEvent<GunZoomComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<GunZoomComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, GunZoomComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionGunZoomEntity, component.ActionToggleGunZoom, uid);
    }

    private void OnComponentShutdown(EntityUid uid, GunZoomComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionGunZoomEntity);
        _eye.ResetZoom(uid);
        component.Offset = Vector2.Zero;
    }

    private void OnZoomAction(Entity<GunZoomComponent> uid, ref GunZoomActionEvent args)
    {
        args.Handled = true;

        uid.Comp.Enabled = !uid.Comp.Enabled;

        if (uid.Comp.Enabled)
        {
            _eye.SetMaxZoom(uid, uid.Comp.Zoom);
            _eye.SetZoom(uid, uid.Comp.Zoom);
        }
        else
        {
            _eye.ResetZoom(uid);
            uid.Comp.Offset = Vector2.Zero;
        }
    }
}

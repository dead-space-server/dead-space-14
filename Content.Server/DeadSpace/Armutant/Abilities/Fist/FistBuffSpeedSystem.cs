using Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Armutant.Abilities.Fist;

public sealed class FistBuffSpeedSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<FistBuffSpeedComponent, FistBuffSpeedToggleEvent>(OnBuffSpeedActive);
        SubscribeLocalEvent<FistBuffSpeedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<FistBuffSpeedComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, FistBuffSpeedComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionFistBuffSpeedEntity, component.ActionToggleFistBuffSpeed, uid);
    }

    private void OnComponentShutdown(EntityUid uid, FistBuffSpeedComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionFistBuffSpeedEntity);
    }

    private void OnBuffSpeedActive(EntityUid uid, FistBuffSpeedComponent component, ref FistBuffSpeedToggleEvent args)
    {
        _speed.ChangeBaseSpeed(uid, 3f, 5.5f, 20f);
        _popup.PopupEntity(Loc.GetString("speed-buff-start"), uid, uid);

        Timer.Spawn(TimeSpan.FromSeconds(15), () =>
        {
            _speed.ChangeBaseSpeed(uid, 2.5f, 4.5f, 20f);
            _popup.PopupEntity(Loc.GetString("speed-buff-end"), uid, uid);
        });

        args.Handled = true;
    }
}

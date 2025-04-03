using Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Content.Shared.Coordinates;
using Robust.Shared.Network;
using Content.Server.Beam;

namespace Content.Server.DeadSpace.Armutant.Abilities.Fist;

public sealed class FistStunAroundSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly BeamSystem _beamSystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<FistStunAroundComponent, FistStunAroundToggleEvent>(OnFistStunAroundAction);
        SubscribeLocalEvent<FistStunAroundComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<FistStunAroundComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, FistStunAroundComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionFistStunAroundEntity, component.ActionToggleFistStunAround, uid);
    }

    private void OnComponentShutdown(EntityUid uid, FistStunAroundComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionFistStunAroundEntity);
    }

    private void OnFistStunAroundAction(Entity<FistStunAroundComponent> uid, ref FistStunAroundToggleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _beamSystem.TryCreateBeam(uid, args.Target, "TentacleArmsHand");

        _stun.TryParalyze(args.Target, uid.Comp.StunTime, true);

        _audio.PlayPvs(uid.Comp.Sound, uid);

        if (_net.IsServer)
            SpawnAttachedTo(uid.Comp.AroundEffect, args.Target.ToCoordinates());
    }
}

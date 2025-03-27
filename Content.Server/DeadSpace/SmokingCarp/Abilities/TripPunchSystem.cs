using Content.Server.DeadSpace.SmokingCarp.Abilities.Components;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.SmokingCarp;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Server.DeadSpace.SmokingCarp.Abilities;
public sealed class TripPunchSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;

    private readonly HashSet<EntityUid> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TripPunchComponent, TripPunchCarpEvent>(OnTripPunchAction);
        SubscribeLocalEvent<TripPunchComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<TripPunchComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, TripPunchComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionTripPunchCarpEntity, component.ActionTripPunchCarpAtack, uid);
    }

    private void OnComponentShutdown(EntityUid uid, TripPunchComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionTripPunchCarpEntity);
    }

    private void OnTripPunchAction(EntityUid uid, TripPunchComponent component, TripPunchCarpEvent args)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || _mobState.IsDead(uid))
            return;

        args.Handled = true;

        _receivers.Clear();

        foreach (var entity in _entityLookup.GetEntitiesInRange(xform.Coordinates, component.Range))
        {
            if (entity == uid)
                continue;

            if (HasComp<TripPunchComponent>(entity))
                continue;

            if (!HasComp<MobStateComponent>(entity))
                continue;

            _receivers.Add(entity);
        }

        if (_net.IsServer)
            _audio.PlayPvs(component.Sound, uid);

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryParalyze(receiver, component.ParalyzeTime, true);
        }

        if (_net.IsServer && component.SelfEffect is not null)
            SpawnAttachedTo(component.SelfEffect, Transform(uid).Coordinates);
    }

}

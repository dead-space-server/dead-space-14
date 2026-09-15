using System.Numerics; //DS14
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Movement.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Weapons.Misc;
using Content.Shared.Gravity; //DS14
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map; //DS14
using Robust.Shared.Network;
using Robust.Shared.Physics; //DS14
using Robust.Shared.Timing;

namespace Content.Shared.Chasm;

/// <summary>
///     Handles making entities fall into chasms when stepped on.
/// </summary>
public sealed class ChasmSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!; // DS14 - pre-v288 engine
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGrapplingGunSystem _grapple = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!; //DS14
    [Dependency] private readonly EntityLookupSystem _lookup = default!; //DS14
    [Dependency] private readonly SharedTransformSystem _transform = default!; //DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmComponent, ComponentStartup>(OnChasmStartup); //DS14
        SubscribeLocalEvent<ChasmComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    //DS14-start
    private void OnChasmStartup(EntityUid uid, ChasmComponent component, ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        var xform = Transform(uid);

        if (!xform.Anchored)
            return;

        var worldPos = _transform.GetWorldPosition(uid);
        var box = Box2.CenteredAround(worldPos, new Vector2(0.9f, 0.9f));

        foreach (var entity in _lookup.GetEntitiesIntersecting(xform.MapID, box,
                     LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (entity == uid)
                continue;

            if (_grapple.IsEntityHooked(entity))
                continue;

            if (_gravity.IsWeightless(entity))
                continue;

            StartFalling(uid, component, entity);
        }
    }
    //DS14-end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // don't predict queuedels on client
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ChasmFallingComponent>();
        while (query.MoveNext(out var uid, out var chasm))
        {
            if (_timing.CurTime < chasm.NextDeletionTime)
                continue;

            QueueDel(uid);
        }
    }

    private void OnStepTriggered(EntityUid uid, ChasmComponent component, ref StepTriggeredOffEvent args)
    {
        // already doomed
        if (HasComp<ChasmFallingComponent>(args.Tripper))
            return;

        StartFalling(uid, component, args.Tripper);
    }

    public void StartFalling(
        EntityUid chasm,
        ChasmComponent component,
        EntityUid tripper,
        bool playSound = true,
        bool playEmote = true)
    {
        if (HasComp<ChasmFallingComponent>(tripper))
            return;

        var attempt = new ChasmFallingAttemptEvent(tripper, chasm);
        RaiseLocalEvent(tripper, attempt, true);

        if (attempt.Cancelled)
            return;

        var falling = AddComp<ChasmFallingComponent>(tripper);

        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        if (playSound && _net.IsServer)
            _audio.PlayPvs(component.FallingSound, chasm);

        if (playEmote && component.Emote is { } emote)
            _chat.TryEmoteWithChat(tripper, emote);
    }

    private void OnStepTriggerAttempt(EntityUid uid, ChasmComponent component, ref StepTriggerAttemptEvent args)
    {
        if (_grapple.IsEntityHooked(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        //DS14-start
        if (_gravity.IsWeightless(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }
        //DS14-end

        args.Continue = true;
    }

    private void OnUpdateCanMove(EntityUid uid, ChasmFallingComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
}

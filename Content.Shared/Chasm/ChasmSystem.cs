<<<<<<< ours
﻿using System.Numerics; //DS14
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Ghost;
||||||| base
﻿using Content.Shared.ActionBlocker;
=======
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
>>>>>>> theirs
using Content.Shared.Movement.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Weapons.Misc;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Content.Shared.Gravity; //DS14
// DS14-start
using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.DeadSpace.Chasm;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
// DS14-end
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map; //DS14
using Robust.Shared.Physics; //DS14
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Chasm;

/// <summary>
/// Handles making entities fall into chasms when stepped on.
/// </summary>
public sealed partial class ChasmSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!; // DS14 - pre-v288 engine
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGrapplingGunSystem _grapple = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!; //DS14
    [Dependency] private readonly EntityLookupSystem _lookup = default!; //DS14
    [Dependency] private readonly SharedTransformSystem _transform = default!; //DS14
    [Dependency] private readonly InventorySystem _inventory = default!; //DS14
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!; //DS14
||||||| base
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGrapplingGunSystem _grapple = default!;
=======
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedGrapplingGunSystem _grapple = default!;
>>>>>>> theirs

<<<<<<< ours
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmComponent, ComponentStartup>(OnChasmStartup); //DS14
        SubscribeLocalEvent<ChasmComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<ChasmHoverGraceComponent, MobStateChangedEvent>(OnHoverGraceMobStateChanged); //DS14
    }
||||||| base
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }
=======
    [Dependency] private EntityQuery<ChasmComponent> _chasmQuery;
    [Dependency] private EntityQuery<ChasmFallingComponent> _chasmFallingQuery;
>>>>>>> theirs

<<<<<<< ours
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

            // The step trigger picks it up once the grace runs out.
            if (TryHoldOffFall(component, entity))
                continue;

            StartFalling(uid, component, entity);
        }
    }
    //DS14-end

||||||| base
=======
    /// <inheritdoc />
>>>>>>> theirs
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

<<<<<<< ours
        // don't predict queuedels on client
        if (_net.IsClient)
            return;

        UpdateHovering(); // DS14

||||||| base
        // don't predict queuedels on client
        if (_net.IsClient)
            return;

=======
>>>>>>> theirs
        var query = EntityQueryEnumerator<ChasmFallingComponent>();
        while (query.MoveNext(out var uid, out var chasm))
        {
            if (_timing.CurTime < chasm.NextDeletionTime)
                continue;

            var chasmEvent = new EntityCompletedFallingIntoChasmEvent((uid, chasm));
            RaiseLocalEvent(chasm.FallingInto, ref chasmEvent);
            if (_chasmQuery.TryComp(chasm.FallingInto, out var chasmComp))
            {
                var tripperEvent = new CompletedFallingIntoChasmEvent((chasm.FallingInto, chasmComp));
                RaiseLocalEvent(uid, ref tripperEvent);
            }
            else
            {
                DebugTools.Assert($"{ToPrettyString(chasm.FallingInto)} is missing {nameof(ChasmComponent)}");
            }

            QueueDel(uid);
        }
    }

    #region Event Handlers
    [SubscribeLocalEvent]
    private void OnStepTriggered(Entity<ChasmComponent> entity, ref StepTriggeredOffEvent args)
    {
        // already doomed
        if (_chasmFallingQuery.HasComp(args.Tripper))
            return;

<<<<<<< ours
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

        // DS14-start: startup checks also reach this path without a step trigger.
        if (HasComp<GhostComponent>(tripper))
            return;

        // A chasm beneath another grid must not swallow entities standing on that grid.
        if (Transform(tripper).GridUid is { } grid && grid != Transform(chasm).GridUid)
            return;
        // DS14-end

        var attempt = new ChasmFallingAttemptEvent(tripper, chasm);
        RaiseLocalEvent(tripper, attempt, true);

        if (attempt.Cancelled)
            return;

        var falling = AddComp<ChasmFallingComponent>(tripper);
||||||| base
        StartFalling(uid, component, args.Tripper);
    }

    public void StartFalling(EntityUid chasm, ChasmComponent component, EntityUid tripper, bool playSound = true)
    {
        var falling = AddComp<ChasmFallingComponent>(tripper);
=======
        // Check the white-/blacklists and inform on rejection.
        if (!(entity.Comp.Whitelist == null && entity.Comp.Blacklist == null ||
              _whitelist.CheckBoth(args.Tripper, entity.Comp.Blacklist, entity.Comp.Whitelist)))
        {
            var rejected = new FallerRejectedByChasmEvent(args.Tripper);
            RaiseLocalEvent(entity, ref rejected);
            return;
        }
>>>>>>> theirs

        // Give an opportunity to cancel the fall for whatever reason.
        var checkEvent = new EntityStartFallingAttemptEvent(args.Tripper);
        RaiseLocalEvent(entity, ref checkEvent);
        if (checkEvent.Cancelled)
            return;

<<<<<<< ours
        if (playSound && _net.IsServer)
            _audio.PlayPvs(component.FallingSound, chasm);

        if (playEmote && component.Emote is { } emote)
            _chat.TryEmoteWithChat(tripper, emote);
||||||| base
        if (playSound)
            _audio.PlayPredicted(component.FallingSound, chasm, tripper);
=======
        StartFalling(entity.AsNullable(), args.Tripper);
>>>>>>> theirs
    }

    [SubscribeLocalEvent]
    private void OnStepTriggerAttempt(Entity<ChasmComponent> entity, ref StepTriggerAttemptEvent args)
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

        // Cancelled, the step trigger tries again every tick: it falls in once the grace is over.
        if (TryHoldOffFall(component, args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        // Carried over by someone who walks on air: let go of it - or stunned - and it drops in on the next tick.
        if (IsPulledOverChasm(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }
        //DS14-end

        args.Continue = true;
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ChasmComponent> entity, ref ComponentShutdown args)
    {
        var e = EntityQueryEnumerator<ChasmFallingComponent>();
        while (e.MoveNext(out var fallingEnt, out var falling))
        {
            if (falling.FallingInto != entity.Owner)
                continue;

            RemCompDeferred<ChasmFallingComponent>(fallingEnt);
        }
    }

    [SubscribeLocalEvent]
    private static void OnUpdateCanMove(Entity<ChasmFallingComponent> entity, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
<<<<<<< ours

    // DS14-start
    /// <summary>Off every chasm this long, the next one starts a fresh grace.</summary>
    private static readonly TimeSpan HoverGraceGap = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Anti-gravity clothing keeps one over a chasm only while standing. Gone down, one gets
    /// <see cref="ChasmComponent.CritHoverGrace"/> to reach solid ground or get back up first: in pre-crit or crit,
    /// or lying down for any reason in clothing with <see cref="AntiGravityChasmHoldComponent.Timer"/>. Clothing with
    /// that component and no timer holds its living wearer up for good.
    /// </summary>
    private bool TryHoldOffFall(ChasmComponent chasm, EntityUid tripper)
    {
        if (!TryComp<MobStateComponent>(tripper, out var mobState) ||
            mobState.CurrentState == MobState.Dead ||
            GetAntiGravityClothing(tripper) is not { } clothing)
        {
            return false;
        }

        var hold = CompOrNull<AntiGravityChasmHoldComponent>(clothing);
        if (hold is { Timer: false })
            return true;

        // The clothing's own timer (shorter in the deep night), or the chasm's for anything else in crit.
        var duration = hold != null
            ? IsDeepNight(tripper) ? hold.DeepNightGrace : hold.Grace
            : chasm.CritHoverGrace;

        if (duration <= TimeSpan.Zero ||
            hold == null && mobState.CurrentState is not (MobState.PreCritical or MobState.Critical))
        {
            return false;
        }

        return TickHoverGrace(tripper, duration);
    }

    /// <summary>
    /// Counts down the time held over a chasm, starting it (with a warning) on the first tick over one.
    /// True while there is time left.
    /// </summary>
    private bool TickHoverGrace(EntityUid tripper, TimeSpan duration)
    {
        var now = _timing.CurTime;
        if (!TryComp<ChasmHoverGraceComponent>(tripper, out var grace) || now - grace.LastSeen > HoverGraceGap)
        {
            grace = EnsureComp<ChasmHoverGraceComponent>(tripper);
            grace.FallAt = now + duration;
            Dirty(tripper, grace);
            // No popup: the seconds left count down over the player's head, as the deep night's shadows do.
        }

        grace.LastSeen = now;
        return now < grace.FallAt;
    }

    private readonly HashSet<Entity<ChasmComponent>> _hoverChasms = new();

    /// <summary>
    /// Standing in anti-gravity clothing with a timer the wearer is weightless, and a chasm's step trigger leaves the
    /// weightless alone: the countdown over a chasm runs here, and at its end they fall in all the same.
    /// </summary>
    private void UpdateHovering()
    {
        var query = EntityQueryEnumerator<AntiGravityChasmHoldComponent, TransformComponent>();
        while (query.MoveNext(out var item, out var hold, out var itemXform))
        {
            var wearer = itemXform.ParentUid;
            if (!hold.Timer || !wearer.IsValid() || HasComp<ChasmFallingComponent>(wearer) ||
                !_gravity.IsWeightless(wearer) || GetAntiGravityClothing(wearer) != item ||
                !TryComp<MobStateComponent>(wearer, out var mobState) || mobState.CurrentState == MobState.Dead ||
                _grapple.IsEntityHooked(wearer))
            {
                continue;
            }

            _hoverChasms.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(wearer), 0.05f, _hoverChasms);
            Entity<ChasmComponent>? chasm = null;
            foreach (var found in _hoverChasms)
            {
                chasm = found;
                break;
            }

            if (chasm is not { } over)
                continue;

            var duration = IsDeepNight(wearer) ? hold.DeepNightGrace : hold.Grace;
            if (duration > TimeSpan.Zero && !TickHoverGrace(wearer, duration))
                StartFalling(over, over.Comp, wearer);
        }
    }

    /// <summary>
    /// Pulled by someone held over chasms (anti-gravity clothing while standing, a flight potion) who can still act.
    /// </summary>
    private bool IsPulledOverChasm(EntityUid uid)
    {
        if (!TryComp<PullableComponent>(uid, out var pullable) || pullable.Puller is not { } puller ||
            TerminatingOrDeleted(puller) || !_blocker.CanInteract(puller, null))
        {
            return false;
        }

        return _gravity.IsWeightless(puller) ||
               HasComp<LavalandChasmImmuneComponent>(puller) && !HasComp<ChasmFallingComponent>(puller);
    }

    /// <summary>The lavaland's deep night, or the chromite caves' deep dark.</summary>
    private bool IsDeepNight(EntityUid uid)
    {
        var map = Transform(uid).MapUid;
        if (TryComp<LavalandChromiteCavesComponent>(map, out var caves))
            return caves.Mode == LavalandCavesMode.DeepDark;

        return _dayCycle.IsDeepNight(map);
    }

    private EntityUid? GetAntiGravityClothing(EntityUid uid)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(uid, out var slots, SlotFlags.WITHOUT_POCKET))
            return null;

        while (slots.NextItem(out var item))
        {
            if (HasComp<AntiGravityClothingComponent>(item))
                return item;
        }

        return null;
    }

    private void OnHoverGraceMobStateChanged(EntityUid uid, ChasmHoverGraceComponent component, MobStateChangedEvent args)
    {
        // Back on its feet: the boots hold it again, and a later fall starts a fresh grace.
        if (args.NewMobState == MobState.Alive)
            RemCompDeferred(uid, component);
    }
    // DS14-end
||||||| base
=======
    #endregion Event Handlers

    #region Public API
    /// <summary>
    /// Causes <paramref name="tripper"/> to fall into <paramref name="chasm"/>: starts a falling animation, optionally
    /// plays a sound, and eventually deletes <paramref name="tripper"/>.
    /// If <paramref name="chasm"/> does not have a <see cref="ChasmComponent"/> component, does nothing and returns null.
    /// </summary>
    /// <param name="playSound">Whether or not the chasm should play a sound when the entity falls in.</param>
    /// <param name="playEmote">Whether or not <paramref name="tripper"/> should try to emote when falling into the chasm.</param>
    /// <returns>
    /// <paramref name="tripper"/> with its new <see cref="ChasmFallingComponent"/>, if the entity did start falling, null otherwise.
    /// </returns>
    [PublicAPI]
    public Entity<ChasmFallingComponent>? StartFalling(
        Entity<ChasmComponent?> chasm,
        EntityUid tripper,
        bool playSound = true,
        bool playEmote = true
    )
    {
        if (!_chasmQuery.Resolve(chasm, ref chasm.Comp, logMissing: false))
            return null;

        var falling = AddComp<ChasmFallingComponent>(tripper);
        falling.FallingInto = chasm;

        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        if (playSound)
            _audio.PlayPredicted(chasm.Comp.FallingSound, chasm, tripper);

        if (playEmote && chasm.Comp.Emote is { } emote)
            _chat.TryEmoteWithChat(tripper, emote);

        var chasmEvent = new EntityStartedFallingIntoChasmEvent((tripper, falling));
        RaiseLocalEvent(chasm, ref chasmEvent);
        var tripperEvent = new StartedFallingIntoChasmEvent((chasm, chasm.Comp));
        RaiseLocalEvent(tripper, ref tripperEvent);

        Entity<ChasmFallingComponent> ret = (tripper, falling);
        Dirty(ret);
        return ret;
    }

    #endregion Public API
>>>>>>> theirs
}

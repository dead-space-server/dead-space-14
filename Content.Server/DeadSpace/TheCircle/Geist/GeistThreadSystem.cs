// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Hands.Systems;
using Content.Shared.DeadSpace.TheCircle.Geist;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.TheCircle.Geist;

/// <inheritdoc cref="GeistThreadSpoolComponent"/>
public sealed class GeistThreadSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _snapped = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeistThreadSpoolComponent, AfterInteractEvent>(OnSpoolAfterInteract);
        SubscribeLocalEvent<GeistThreadSpoolComponent, UseInHandEvent>(OnSpoolUseInHand);
        SubscribeLocalEvent<GeistThreadSpoolComponent, ComponentShutdown>(OnSpoolShutdown);
        SubscribeLocalEvent<GeistThreadComponent, ComponentShutdown>(OnThreadShutdown);
        SubscribeLocalEvent<GeistThreadComponent, ComponentGetStateAttemptEvent>(OnThreadGetStateAttempt);
        SubscribeLocalEvent<GeistThreadComponent, GetVerbsEvent<AlternativeVerb>>(OnThreadGetVerbs);

        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.UseItemInHand, new PointerInputCmdHandler(OnReelInput, ignoreUp: false), typeof(SharedHandsSystem))
            .Register<GeistThreadSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<GeistThreadSystem>();
        base.Shutdown();
    }

    /// <summary>Tied to an item within reach: the old thread, if any, is let go.</summary>
    private void OnSpoolAfterInteract(Entity<GeistThreadSpoolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == ent.Owner ||
            !HasComp<ItemComponent>(target) || Transform(target).Anchored)
        {
            return;
        }

        args.Handled = true;
        Untie(ent);
        if (TryComp<GeistThreadComponent>(target, out var old))
            Untie(old);

        var thread = EnsureComp<GeistThreadComponent>(target);
        thread.Spool = ent;
        thread.TiedBy = args.User;
        Dirty(target, thread);
        ent.Comp.Target = target;
        _popup.PopupEntity(Loc.GetString("geist-thread-tied", ("item", target)), args.User, args.User);
    }

    /// <summary>The use key itself is read in <see cref="OnReelInput"/>: held down, it reels; here it only does nothing else.</summary>
    private void OnSpoolUseInHand(Entity<GeistThreadSpoolComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.Target != null)
            args.Handled = true;
    }

    /// <summary>While the use key is held with the spool in hand, the item is reeled in; let go, it stops.</summary>
    private bool OnReelInput(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.Session?.AttachedEntity is not { } user)
            return false;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<GeistThreadSpoolComponent>(held, out var spool) || spool.Target is not { } target)
                continue;

            if (args.State == BoundKeyState.Down)
            {
                if (spool.Reeler == null)
                    _popup.PopupEntity(Loc.GetString("geist-thread-reel"), user, user);
                spool.Reeler = user;
            }
            else if (spool.Reeler == user)
            {
                StopReeling(spool, target);
            }

            // The default use in hand still runs; the spool's own UseInHandEvent does nothing.
            return false;
        }

        return false;
    }

    private void OnSpoolShutdown(Entity<GeistThreadSpoolComponent> ent, ref ComponentShutdown args)
    {
        Untie(ent);
    }

    private void OnThreadShutdown(Entity<GeistThreadComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<GeistThreadSpoolComponent>(ent.Comp.Spool, out var spool) || spool.Target != ent.Owner)
            return;

        StopReeling(spool, ent.Owner);
        spool.Target = null;
    }

    /// <summary>Only the one who tied it knows of the thread: nobody else's client gets it.</summary>
    private void OnThreadGetStateAttempt(Entity<GeistThreadComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        // Replays (no player) record it.
        if (args.Player is { } player && player.AttachedEntity != ent.Comp.TiedBy)
            args.Cancelled = true;
    }

    /// <summary>Picked up, the item shows its thread: it can be torn off.</summary>
    private void OnThreadGetVerbs(Entity<GeistThreadComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_hands.IsHolding(args.User, ent.Owner))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("geist-thread-tear-verb"),
            Act = () =>
            {
                if (TerminatingOrDeleted(ent) || !HasComp<GeistThreadComponent>(ent))
                    return;

                RemComp<GeistThreadComponent>(ent);
                _popup.PopupEntity(Loc.GetString("geist-thread-torn", ("item", ent.Owner)), user, user);
            },
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _snapped.Clear();
        var query = EntityQueryEnumerator<GeistThreadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thread, out var xform))
        {
            if (thread.Spool is not { } spoolUid ||
                !TryComp<GeistThreadSpoolComponent>(spoolUid, out var spool) ||
                spool.Target != uid)
            {
                _snapped.Add(uid);
                continue;
            }

            // The thread runs from whoever holds the spool (or the spool itself, set down or put away).
            var holder = _container.TryGetContainingContainer(spoolUid, out var container) &&
                         _hands.IsHolding(container.Owner, spoolUid)
                ? container.Owner
                : (EntityUid?) null;
            var origin = holder ?? spoolUid;

            // Past its reach, or a wall between: it snaps.
            if (!_examine.InRangeUnOccluded(origin, uid, spool.MaxRange))
            {
                if (thread.TiedBy is { } owner && Exists(owner))
                    _popup.PopupEntity(Loc.GetString("geist-thread-snapped"), owner, owner);
                _snapped.Add(uid);
                continue;
            }

            if (spool.Reeler != null)
                Reel(spool, (uid, xform), holder);
        }

        foreach (var uid in _snapped)
        {
            RemComp<GeistThreadComponent>(uid);
        }
    }

    /// <summary>
    /// Drawn through the air toward the reeler, as long as they hold the spool and the key.
    /// Someone else holding it has it yanked out of their hands.
    /// </summary>
    private void Reel(GeistThreadSpoolComponent spool, Entity<TransformComponent> item, EntityUid? holder)
    {
        if (holder is { } puller &&
            puller == spool.Reeler &&
            _container.TryGetContainingContainer(item.Owner, out var container) &&
            container.Owner != puller &&
            _hands.IsHolding(container.Owner, item) &&
            _hands.TryDrop(container.Owner, item.Owner, checkActionBlocker: false))
        {
            _popup.PopupEntity(Loc.GetString("geist-thread-yanked", ("item", item.Owner)), container.Owner, container.Owner, PopupType.MediumCaution);
        }

        if (holder is not { } reeler ||
            reeler != spool.Reeler ||
            _container.IsEntityInContainer(item) ||
            item.Comp.Anchored ||
            HasComp<ThrownItemComponent>(item) ||
            !TryComp<PhysicsComponent>(item, out var body))
        {
            StopReeling(spool, item);
            return;
        }

        var toHolder = _transform.GetWorldPosition(reeler) - _transform.GetWorldPosition(item.Comp);
        var distance = toHolder.Length();
        if (distance <= spool.ReelStop)
        {
            StopReeling(spool, item);
            return;
        }

        // In the air the floor's friction doesn't eat the pull.
        _physics.SetBodyStatus(item, body, BodyStatus.InAir);
        _physics.SetLinearVelocity(item, toHolder / distance * spool.ReelSpeed, body: body);
        _physics.WakeBody(item, body: body);
    }

    private void StopReeling(GeistThreadSpoolComponent spool, EntityUid item)
    {
        if (spool.Reeler == null)
            return;

        spool.Reeler = null;
        if (TerminatingOrDeleted(item) ||
            HasComp<ThrownItemComponent>(item) ||
            !TryComp<PhysicsComponent>(item, out var body) ||
            body.BodyStatus != BodyStatus.InAir)
        {
            return;
        }

        _physics.SetLinearVelocity(item, default, body: body);
        _physics.SetBodyStatus(item, body, BodyStatus.OnGround);
    }

    private void Untie(Entity<GeistThreadSpoolComponent> spool)
    {
        if (spool.Comp.Target is { } target && TryComp<GeistThreadComponent>(target, out var thread) &&
            thread.Spool == spool.Owner)
        {
            RemComp<GeistThreadComponent>(target);
        }

        spool.Comp.Target = null;
        spool.Comp.Reeler = null;
    }

    private void Untie(GeistThreadComponent thread)
    {
        if (TryComp<GeistThreadSpoolComponent>(thread.Spool, out var spool))
            Untie((thread.Spool.Value, spool));
    }
}

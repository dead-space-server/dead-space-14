// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Hands.Systems;
using Content.Shared.DeadSpace.TheCircle.Geist;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

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
        thread.Owner = args.User;
        Dirty(target, thread);
        ent.Comp.Target = target;
        ent.Comp.Reeling = false;
        _popup.PopupEntity(Loc.GetString("geist-thread-tied", ("item", target)), args.User, args.User);
    }

    private void OnSpoolUseInHand(Entity<GeistThreadSpoolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Target == null)
            return;

        args.Handled = true;
        ent.Comp.Reeling = true;
        _popup.PopupEntity(Loc.GetString("geist-thread-reel"), args.User, args.User);
    }

    private void OnSpoolShutdown(Entity<GeistThreadSpoolComponent> ent, ref ComponentShutdown args)
    {
        Untie(ent);
    }

    private void OnThreadShutdown(Entity<GeistThreadComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<GeistThreadSpoolComponent>(ent.Comp.Spool, out var spool) && spool.Target == ent.Owner)
        {
            spool.Target = null;
            spool.Reeling = false;
        }
    }

    /// <summary>Only the one who tied it knows of the thread: nobody else's client gets it.</summary>
    private void OnThreadGetStateAttempt(Entity<GeistThreadComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        // Replays (no player) record it.
        if (args.Player is { } player && player.AttachedEntity != ent.Comp.Owner)
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
                if (thread.Owner is { } owner && Exists(owner))
                    _popup.PopupEntity(Loc.GetString("geist-thread-snapped"), owner, owner);
                _snapped.Add(uid);
                continue;
            }

            if (holder == null ||
                _container.IsEntityInContainer(uid) ||
                xform.Anchored ||
                HasComp<ThrownItemComponent>(uid) ||
                !TryComp<PhysicsComponent>(uid, out var body))
            {
                continue;
            }

            Drag((uid, body), spool, _transform.GetWorldPosition(holder.Value) - _transform.GetWorldPosition(xform));
        }

        foreach (var uid in _snapped)
        {
            RemComp<GeistThreadComponent>(uid);
        }
    }

    /// <summary>Reeled in, it flies to the holder; walked away from, it is dragged after them like a pulled thing.</summary>
    private void Drag(Entity<PhysicsComponent> item, GeistThreadSpoolComponent spool, Vector2 toHolder)
    {
        var distance = toHolder.Length();
        if (distance < 0.01f)
            return;

        var direction = toHolder / distance;
        if (spool.Reeling)
        {
            if (distance <= spool.ReelStop)
            {
                spool.Reeling = false;
                _physics.SetLinearVelocity(item, Vector2.Zero, body: item.Comp);
                return;
            }

            _physics.SetLinearVelocity(item, direction * spool.ReelSpeed, body: item.Comp);
            _physics.WakeBody(item, body: item.Comp);
            return;
        }

        if (distance <= spool.Length)
            return;

        var speed = MathF.Min(spool.DragSpeed, (distance - spool.Length) * spool.DragSpeed);
        _physics.SetLinearVelocity(item, direction * speed, body: item.Comp);
        _physics.WakeBody(item, body: item.Comp);
    }

    private void Untie(Entity<GeistThreadSpoolComponent> spool)
    {
        if (spool.Comp.Target is { } target && TryComp<GeistThreadComponent>(target, out var thread) &&
            thread.Spool == spool.Owner)
        {
            RemComp<GeistThreadComponent>(target);
        }

        spool.Comp.Target = null;
        spool.Comp.Reeling = false;
    }

    private void Untie(GeistThreadComponent thread)
    {
        if (TryComp<GeistThreadSpoolComponent>(thread.Spool, out var spool))
            Untie((thread.Spool.Value, spool));
    }
}

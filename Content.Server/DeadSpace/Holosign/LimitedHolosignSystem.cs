// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Holosign;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Holosign;

public sealed class LimitedHolosignSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimitedHolosignProjectorComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<LimitedHolosignProjectorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<LimitedHolosignProjectorComponent, RemoveAllBarriersDoAfterEvent>(OnRemoveAllDoAfter);
        SubscribeLocalEvent<LimitedHolosignProjectorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnExamine(Entity<LimitedHolosignProjectorComponent> ent, ref ExaminedEvent args)
    {
        PruneBarriers(ent);
        args.PushMarkup(Loc.GetString("limited-holosign-examine",
            ("count", ent.Comp.Barriers.Count),
            ("max", ent.Comp.MaxBarriers)));
    }

    private void OnBeforeInteract(Entity<LimitedHolosignProjectorComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || HasComp<StorageComponent>(args.Target))
            return;

        PruneBarriers(ent);

        // Using the projector on an existing barrier removes it again instead of placing a new one.
        if (args.Target is { } target && ent.Comp.Barriers.Remove(target))
        {
            QueueDel(target);
            args.Handled = true;
            return;
        }

        // Each externally destroyed barrier takes one of the projector's slots on its own recharge timer.
        if (ent.Comp.RechargeTime > TimeSpan.Zero
            && ent.Comp.RechargingSlots.Count > 0
            && _timing.CurTime < GetNextRecharge(ent)
            && ent.Comp.Barriers.Count >= ent.Comp.MaxBarriers - ent.Comp.RechargingSlots.Count)
        {
            var seconds = Math.Ceiling((GetNextRecharge(ent) - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("limited-holosign-recharging", ("seconds", seconds)), ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Barriers.Count >= ent.Comp.MaxBarriers)
        {
            _popup.PopupEntity(Loc.GetString("limited-holosign-full", ("max", ent.Comp.MaxBarriers)), ent, args.User);
            args.Handled = true;
            return;
        }

        var barrier = SpawnAtPosition(ent.Comp.SignProto, args.ClickLocation);
        RemComp<TimedDespawnComponent>(barrier);
        _transform.SetLocalRotation(barrier, Angle.Zero);
        ent.Comp.Barriers.Add(barrier);

        args.Handled = true;
    }

    private void OnGetVerbs(Entity<LimitedHolosignProjectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        PruneBarriers(ent);

        if (ent.Comp.Barriers.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("limited-holosign-remove-all"),
            Act = () => StartRemoveAll(ent, user),
            Priority = 1,
        });
    }

    private void StartRemoveAll(Entity<LimitedHolosignProjectorComponent> ent, EntityUid user)
    {
        if (ent.Comp.Barriers.Count == 0)
            return;

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.RemoveAllDelay,
            new RemoveAllBarriersDoAfterEvent(), eventTarget: ent.Owner, used: ent.Owner)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnRemoveAllDoAfter(Entity<LimitedHolosignProjectorComponent> ent, ref RemoveAllBarriersDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        PruneBarriers(ent);

        var barriers = ent.Comp.Barriers.ToArray();
        ent.Comp.Barriers.Clear();

        foreach (var barrier in barriers)
        {
            if (Exists(barrier))
                QueueDel(barrier);
        }

        _popup.PopupEntity(Loc.GetString("limited-holosign-remove-all-done"), ent, args.User);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        var uid = args.Entity.Owner;

        var query = EntityQueryEnumerator<LimitedHolosignProjectorComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (!comp.Barriers.Remove(uid))
                continue;

            // The barrier was destroyed by something other than the projector itself.
            if (comp.RechargeTime > TimeSpan.Zero)
            {
                // Each destroyed barrier starts its own recharge timer for its own slot
                comp.RechargingSlots.Add(_timing.CurTime + comp.RechargeTime);
            }
            break;
        }
    }

    private void PruneBarriers(Entity<LimitedHolosignProjectorComponent> ent)
    {
        // Slots whose recharge has elapsed are available again on their own.
        if (ent.Comp.RechargeTime > TimeSpan.Zero)
            ent.Comp.RechargingSlots.RemoveAll(time => time <= _timing.CurTime);

        ent.Comp.Barriers.RemoveAll(barrier => !Exists(barrier) || Terminating(barrier));
    }

    /// <summary>
    /// Deadlines of the recharging slots, or <see cref="TimeSpan.MaxValue"/> when none are recharging.
    /// </summary>
    private static TimeSpan GetNextRecharge(Entity<LimitedHolosignProjectorComponent> ent)
    {
        if (ent.Comp.RechargingSlots.Count == 0)
            return TimeSpan.MaxValue;

        var next = ent.Comp.RechargingSlots[0];
        for (var i = 1; i < ent.Comp.RechargingSlots.Count; i++)
        {
            if (ent.Comp.RechargingSlots[i] < next)
                next = ent.Comp.RechargingSlots[i];
        }

        return next;
    }
}

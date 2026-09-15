// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Holosign;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Holosign;

public sealed class LimitedHolosignSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimitedHolosignProjectorComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<LimitedHolosignProjectorComponent, ExaminedEvent>(OnExamine);
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

        if (args.Target is { } target && ent.Comp.Barriers.Remove(target))
        {
            QueueDel(target);
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

    private void PruneBarriers(Entity<LimitedHolosignProjectorComponent> ent)
    {
        ent.Comp.Barriers.RemoveAll(barrier => !Exists(barrier) || Terminating(barrier));
    }
}

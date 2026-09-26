using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public partial class SharedGunSystem
{
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!; // DS14 - pre-v288 IoC

    // DS14-Start - pre-v288 explicit event subscriptions
    private void InitializeTargetFinder()
    {
        SubscribeLocalEvent<TargetFinderHitscanComponent, AttemptHitscanRaycastFiredEvent>(OnHitscanAttempt,
            before: [typeof(HitscanReflectSystem)]); // DS14 - lock the original hit before reflection recursion.
        SubscribeLocalEvent<TargetFinderComponent, MapInitEvent>(OnTargetFinderMapInit);
        SubscribeLocalEvent<TargetFinderComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<TargetFinderComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }
    // DS14-End

    // DS14-start
    public void OnHitscanAttempt(Entity<TargetFinderHitscanComponent> entity, ref AttemptHitscanRaycastFiredEvent args)
    {
        if (!HasComp<TargetFinderComponent>(args.Data.Gun) ||
            TryComp<HitscanReflectComponent>(entity, out var reflect) && reflect.CurrentReflections > 0)
            return;

        UpdateTarget(args.Data.Gun, args.Data.HitEntity);
    }
    // DS14-end

    private void OnTargetFinderMapInit(Entity<TargetFinderComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        var linkedEntities = _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.LinkingPort);

        foreach (var sink in linkedEntities)
        {
            if (!TryComp<TargetAssignComponent>(sink, out var targetAssigner))
                continue;

            ent.Comp.TargetAssigner = sink;
            break;
        }
    }

    private void OnNewLink(Entity<TargetFinderComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<TargetAssignComponent>(args.Sink))
            return;

        ent.Comp.TargetAssigner = args.Sink;
        Dirty(ent);
    }

    private void OnPortDisconnected(Entity<TargetFinderComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort || ent.Comp.TargetAssigner == null)
            return;

        ent.Comp.TargetAssigner = null;
        Dirty(ent);
    }

    private void UpdateTarget(Entity<TargetFinderComponent?> ent, EntityUid? target)
    {
        if (!Resolve(ent, ref ent.Comp, false) || target is null)
            return;

        ent.Comp.Target = target;

        if (ent.Comp.TargetAssigner is null ||
            !TryComp<TargetAssignComponent>(ent.Comp.TargetAssigner, out var targetAssignComp))
            return;

        targetAssignComp.Target = target;
    }
}

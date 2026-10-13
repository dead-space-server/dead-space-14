// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoScannerSystem : SharedNinjaInfoScannerSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly NinjaInfoObjectiveSystem _objectiveSystem = default!;
    [Dependency] private readonly NinjaInfoConsoleSystem _infoConsoleSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoScannerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NinjaInfoScannerComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<NinjaInfoScannerComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<NinjaInfoScannerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NinjaInfoScannerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsScanning)
                continue;

            if (comp.ScanEndTime is { } endTime && curTime < endTime)
                continue;

            FinishScan((uid, comp));
        }
    }

    private void OnInit(
        Entity<NinjaInfoScannerComponent> ent,
        ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(
            ent.Owner,
            ent.Comp.ContainerId);

        UpdateVisualState(ent);
    }

    private void OnShutdown(
        Entity<NinjaInfoScannerComponent> ent,
        ref ComponentShutdown args)
    {
        ent.Comp.ScanEndTime = null;
        ent.Comp.ScanActor = null;
    }

    private void OnContainerInserted(
        Entity<NinjaInfoScannerComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.Owner != ent.Owner ||
            args.Container.ID != ent.Comp.ContainerId)
        {
            return;
        }

        UpdateConsolesUserInterface(ent);
        UpdateVisualState(ent);
    }

    private void OnContainerRemoved(
        Entity<NinjaInfoScannerComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.Owner != ent.Owner ||
            args.Container.ID != ent.Comp.ContainerId)
        {
            return;
        }

        if (ent.Comp.IsScanning)
        {
            FinishScan(ent);
            return;
        }
        UpdateConsolesUserInterface(ent);
        UpdateVisualState(ent);
    }

    public bool TryStartScan(EntityUid scanner, EntityUid actor)
    {
        if (!TryComp<NinjaInfoScannerComponent>(scanner, out var comp))
            return false;

        if (comp.IsScanning)
        {
            Say(scanner, "ninja-info-phrase-scan-busy");
            return false;
        }

        if (!_container.TryGetContainer(scanner, comp.ContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            Say(scanner, "ninja-info-phrase-no-target");
            return false;
        }

        var target = container.ContainedEntities[0];

        if (!_mobState.IsAlive(target))
        {
            Say(scanner, "ninja-info-phrase-not-alive");
            return false;
        }

        StartScan((scanner, comp), target, actor);

        return true;
    }

    public bool TryEjectTarget(EntityUid scanner)
    {
        if (!TryComp<NinjaInfoScannerComponent>(scanner, out var comp))
            return false;

        if (comp.IsScanning)
        {
            Say(scanner, "ninja-info-phrase-scan-busy");
            return false;
        }

        if (!_container.TryGetContainer(scanner, comp.ContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return false;
        }

        _container.Remove(container.ContainedEntities[0], container);

        return true;
    }

    public bool TryTeleportTarget(EntityUid scanner)
    {
        if (!TryComp<NinjaInfoScannerComponent>(scanner, out var comp))
            return false;

        if (comp.IsScanning)
        {
            Say(scanner, "ninja-info-phrase-scan-busy");
            return false;
        }

        if (!_container.TryGetContainer(scanner, comp.ContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return false;
        }

        var target = container.ContainedEntities[0];

        var marker = GetRandomMarker();
        if (marker != null)
        {
            _container.Remove(target, container);
            _transform.SetCoordinates(
                target,
                Transform(marker.Value).Coordinates);
        }
        else
        {
            Say(scanner, "ninja-info-phrase-no-teleport-markers");
        }

        return true;
    }

    private void StartScan(
        Entity<NinjaInfoScannerComponent> ent,
        EntityUid target,
        EntityUid actor)
    {
        ent.Comp.IsScanning = true;
        ent.Comp.ScanningEntity = target;
        ent.Comp.ScanActor = actor;
        ent.Comp.ScanEndTime =
            _timing.CurTime +
            TimeSpan.FromSeconds(ent.Comp.ScanTime);

        UpdateVisualState(ent);

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            var solution = new Solution();
            solution.AddReagent(
                new ReagentQuantity(
                    ent.Comp.ScanReagent,
                    FixedPoint2.New(ent.Comp.ScanReagentAmount)));

            _bloodstream.TryAddToBloodstream((target, bloodstream), solution);
        }

        Say(ent.Owner, "ninja-info-phrase-scan-started");
    }

    private void FinishScan(Entity<NinjaInfoScannerComponent> ent)
    {
        var target = ent.Comp.ScanningEntity;
        var actor = ent.Comp.ScanActor;

        ent.Comp.IsScanning = false;
        ent.Comp.ScanningEntity = null;
        ent.Comp.ScanActor = null;
        ent.Comp.ScanEndTime = null;

        if (target is not { } targetUid || !Exists(targetUid))
        {
            UpdateVisualState(ent);
            return;
        }

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) &&
            container.ContainedEntities.Contains(targetUid) &&
            _mobState.IsAlive(targetUid))
        {
            var result = _objectiveSystem.TryScanEntity(targetUid, actor);

            Say(ent.Owner, result switch
            {
                NinjaInfoScanResult.Priority => "ninja-info-phrase-scan-priority",
                NinjaInfoScanResult.Job => "ninja-info-phrase-scan-success",
                _ => "ninja-info-phrase-scan-fail",
            });
        }

        UpdateVisualState(ent);
    }

    private EntityUid? GetRandomMarker()
    {
        var markers = new List<EntityUid>();

        var query = EntityQueryEnumerator<NinjaInfoTeleportMarkerComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            markers.Add(uid);
        }

        if (markers.Count == 0)
            return null;

        return _random.Pick(markers);
    }

    private void Say(EntityUid? machine, string phrase)
    {
        if (machine is not { } machineUid || !Exists(machineUid))
            return;

        _chatSystem.TrySendInGameICMessage(
            machineUid,
            Loc.GetString(phrase),
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            true
        );
    }

    private void UpdateVisualState(Entity<NinjaInfoScannerComponent> ent)
    {
        var state = GetVisualState(ent);

        _appearance.SetData(ent.Owner, NinjaInfoScannerVisuals.VisualState, state);
    }

    private NinjaInfoScannerVisualState GetVisualState(Entity<NinjaInfoScannerComponent> ent)
    {
        if (ent.Comp.IsScanning)
            return NinjaInfoScannerVisualState.Scan;

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container) &&
            container.ContainedEntities.Count > 0)
        {
            return NinjaInfoScannerVisualState.Closed;
        }

        return NinjaInfoScannerVisualState.Open;
    }

    private void UpdateConsolesUserInterface(Entity<NinjaInfoScannerComponent> ent)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var sourceComp))
            return;

        foreach (var source in _deviceLink.GetLinkedSinks((ent, sourceComp), ent.Comp.LinkingPort))
        {
            _infoConsoleSystem.UpdateUserInterface(source);
        }
    }
}
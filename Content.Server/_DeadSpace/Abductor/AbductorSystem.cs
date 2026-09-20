using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Actions;
using Content.Server.DoAfter;
using Content.Shared.DeadSpace.Abductor;
using Content.Shared.Eye;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttempt);
        Subs.BuiEvents<AbductorHumanObservationConsoleComponent>(AbductorCameraConsoleUIKey.Key, subs => subs.Event<AbductorBeaconChosenBuiMsg>(OnAbductorBeaconChosenBuiMsg));
        InitializeActions();
        InitializeGizmo();
        InitializeConsole();
        InitializeVest();
        InitializeVictim();
        base.Initialize();
    }

    private void OnAbductorBeaconChosenBuiMsg(Entity<AbductorHumanObservationConsoleComponent> ent, ref AbductorBeaconChosenBuiMsg args)
    {
        OnCameraExit(args.Actor);
        if (ent.Comp.RemoteEntityProto != null)
        {
            var beacon = GetEntity(args.Beacon.NetEnt);
            var eye = SpawnAtPosition(ent.Comp.RemoteEntityProto.Value, Transform(beacon).Coordinates);
            ent.Comp.RemoteEntity = GetNetEntity(eye);

            if (TryComp<HandsComponent>(args.Actor, out var handsComponent))
            {
                foreach (var hand in _hands.EnumerateHands((args.Actor, handsComponent)))
                {
                    if (!_hands.TryGetHeldItem((args.Actor, handsComponent), hand, out var held))
                        continue;

                    if (HasComp<UnremoveableComponent>(held))
                        continue;

                    _hands.DoDrop((args.Actor, handsComponent), hand);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Actor, out var virtItem1))
                {
                    EnsureComp<UnremoveableComponent>(virtItem1.Value);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Actor, out var virtItem2))
                {
                    EnsureComp<UnremoveableComponent>(virtItem2.Value);
                }
            }

            var visibility = EnsureComp<VisibilityComponent>(eye);

            Dirty(ent);

            if (TryComp(args.Actor, out EyeComponent? eyeComp))
            {
                _eye.SetVisibilityMask(args.Actor, eyeComp.VisibilityMask | (int) VisibilityFlags.Abductor, eyeComp);
                _eye.SetTarget(args.Actor, eye, eyeComp);
                _eye.SetDrawFov(args.Actor, false);
                _eye.SetRotation(args.Actor, Angle.Zero, eyeComp);
                if (!HasComp<StationAiOverlayComponent>(args.Actor))
                    AddComp<StationAiOverlayComponent>(args.Actor);
                if (!TryComp(eye, out RemoteEyeSourceContainerComponent? remoteEyeSourceContainerComponent))
                {
                    remoteEyeSourceContainerComponent = new RemoteEyeSourceContainerComponent { Actor = args.Actor };
                    AddComp(eye, remoteEyeSourceContainerComponent);
                }
                else
                    remoteEyeSourceContainerComponent.Actor = args.Actor;
                Dirty(eye, remoteEyeSourceContainerComponent);
                Dirty(args.Actor, eyeComp);
            }

            AddActions(args);

            _mover.SetRelay(args.Actor, eye);
        }
    }

    private void OnCameraExit(EntityUid actor)
    {
        if (TryComp<RelayInputMoverComponent>(actor, out var comp)
            && TryComp<AbductorScientistComponent>(actor, out var abductorComp))
        {
            var relay = comp.RelayEntity;
            RemComp<RelayInputMoverComponent>(actor);

            if (abductorComp.Console != null)
                _virtualItem.DeleteInHandsMatching(actor, abductorComp.Console.Value);

            if (TryComp(actor, out EyeComponent? eyeComp))
            {
                if (HasComp<StationAiOverlayComponent>(actor))
                    RemComp<StationAiOverlayComponent>(actor);

                _eye.SetVisibilityMask(actor, eyeComp.VisibilityMask ^ (int) VisibilityFlags.Abductor, eyeComp);
                _eye.SetDrawFov(actor, true);
                _eye.SetTarget(actor, null, eyeComp);
            }
            RemoveActions(actor);
            QueueDel(relay);
        }
    }

    private void OnBeforeActivatableUIOpen(Entity<AbductorHumanObservationConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (!TryComp<AbductorScientistComponent>(args.User, out var abductorComp))
            return;

        abductorComp.Console = ent.Owner;
        var stations = _stationSystem.GetStations();
        var result = new Dictionary<int, StationBeacons>();

        foreach (var station in stations)
        {
            if (_stationSystem.GetLargestGrid(station) is not { } grid
                || !TryComp(station, out MetaDataComponent? stationMetaData))
                continue;

            var mapId = Transform(grid).MapID;

            if (!TryComp<NavMapComponent>(grid, out var navMap))
                continue;

            result.Add(station.Id, new StationBeacons
            {
                Name = stationMetaData.EntityName,
                StationId = station.Id,
                Beacons = new List<SharedNavMapSystem.NavMapBeacon>(navMap.Beacons.Values),
            });
        }

        _uiSystem.SetUiState(ent.Owner, AbductorCameraConsoleUIKey.Key, new AbductorCameraConsoleBuiState { Stations = result });
    }

    private void OnActivatableUIOpenAttempt(Entity<AbductorHumanObservationConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<AbductorScientistComponent>(args.User))
            args.Cancel();
    }
}
using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.Chasm;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tiles;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.AshWalkers;

public sealed class LavaBoatSystem : EntitySystem
{
    private const float ShoreRange = 1.5f;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly VehicleSystem _vehicle = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavaComponent, StepTriggerAttemptEvent>(OnLavaStep);
        SubscribeLocalEvent<LavaBoatComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<LavaBoatComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<LavaBoatComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<LavaBoatComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<LavaBoatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<LavaBoatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<LavaBoatComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<LavaBoatComponent, StorageBeforeOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<LavaBoatComponent, DestructionEventArgs>(OnDestroyed, before: [typeof(SharedEntityStorageSystem)]);
        SubscribeLocalEvent<LavaBoatComponent, StorageAfterOpenEvent>(OnOpened);
        SubscribeLocalEvent<LavaBoatComponent, StorageAfterCloseEvent>(OnClosed);
        SubscribeLocalEvent<LavaBoatComponent, EntityStorageInsertedIntoAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<LavaBoatComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LavaBoatComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BoneOarComponent, GotEquippedHandEvent>(OnOarEquipped);
        SubscribeLocalEvent<BoneOarComponent, GotUnequippedHandEvent>(OnOarUnequipped);
    }

    private void OnLavaStep(Entity<LavaComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<LavaBoatComponent>(args.Tripper) || HasComp<BoneOarComponent>(args.Tripper) ||
            TryComp<BuckleComponent>(args.Tripper, out var buckle) &&
            HasComp<LavaBoatComponent>(buckle.BuckledTo))
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<LavaBoatComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BoneOarComponent>(args.Used) ||
            !TryComp<BuckleComponent>(args.User, out var buckle))
            return;

        args.Handled = true;
        if (buckle.BuckledTo == ent.Owner)
            _buckle.TryUnbuckle(args.User, args.User);
        else
            _buckle.TryBuckle(args.User, args.User, ent.Owner);
    }

    private void OnCanRun(Entity<LavaBoatComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!args.CanRun)
            return;

        if (args.Vehicle.Comp.Operator is not { } driver ||
            !_hands.EnumerateHeld(driver).Any(HasComp<BoneOarComponent>) ||
            TryComp<EntityStorageComponent>(ent, out var storage) && storage.Open ||
            !CanRow(ent))
            args = args with { CanRun = false };
    }

    private bool CanRow(EntityUid boat)
    {
        var xform = Transform(boat);
        if (HasSurface<LavaComponent>(xform.Coordinates))
            return true;

        if (!TryComp<InputMoverComponent>(boat, out var input))
            return false;

        var (walk, sprint) = _mover.GetVelocityInput(input);
        var direction = walk + sprint;
        if (direction.LengthSquared() < 0.0001f)
            return false;

        var worldDirection = Vector2.Normalize(direction);
        if (_configuration.GetCVar(CCVars.RelativeMovement))
            worldDirection = _mover.GetParentGridAngle(input).RotateVec(worldDirection);
        var destination = new MapCoordinates(_transform.GetWorldPosition(xform) + worldDirection, xform.MapID);
        return HasSurface<LavaComponent>(_transform.ToCoordinates(xform.ParentUid, destination));
    }

    private void OnOarEquipped(Entity<BoneOarComponent> ent, ref GotEquippedHandEvent args)
    {
        RefreshDriver(args.User);
    }

    private void OnOarUnequipped(Entity<BoneOarComponent> ent, ref GotUnequippedHandEvent args)
    {
        RefreshDriver(args.User);
    }

    private void RefreshDriver(EntityUid driver)
    {
        if (TryComp<VehicleOperatorComponent>(driver, out var op) &&
            op.Vehicle is { } boat && HasComp<LavaBoatComponent>(boat))
            RefreshMovement(boat);
    }

    private void RefreshMovement(EntityUid boat)
    {
        _vehicle.RefreshCanRun(boat);
        if (TryComp<VehicleComponent>(boat, out var vehicle) && vehicle.Operator is { } driver &&
            !TerminatingOrDeleted(driver))
            _blocker.UpdateCanMove(driver);
    }

    private void OnMoveInput(Entity<LavaBoatComponent> ent, ref MoveInputEvent args)
    {
        RefreshMovement(ent.Owner);
    }

    private void OnMove(Entity<LavaBoatComponent> ent, ref MoveEvent args)
    {
        if (!_timing.ApplyingState)
            RefreshMovement(ent.Owner);
    }

    private void OnOpened(Entity<LavaBoatComponent> ent, ref StorageAfterOpenEvent args)
    {
        RefreshMovement(ent.Owner);
    }

    private void OnClosed(Entity<LavaBoatComponent> ent, ref StorageAfterCloseEvent args)
    {
        RefreshMovement(ent.Owner);
    }

    private void OnInsertAttempt(Entity<LavaBoatComponent> ent, ref EntityStorageInsertedIntoAttemptEvent args)
    {
        if (HasComp<MobStateComponent>(args.ItemToInsert) && !_mobState.IsDead(args.ItemToInsert))
            args.Cancelled = true;
    }

    private void OnExamined(Entity<LavaBoatComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        args.PushMarkup(storage.Open
            ? Loc.GetString("lava-boat-cargo-open")
            : Loc.GetString("lava-boat-cargo-closed", ("count", storage.Contents.Count), ("capacity", storage.Capacity)));
    }

    private void OnOpenAttempt(Entity<LavaBoatComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (IsSafe(Transform(ent).Coordinates) && TryGetShore(ent, out _))
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("lava-boat-cargo-shore"), ent, args.User);
    }

    private void OnBeforeOpen(Entity<LavaBoatComponent> ent, ref StorageBeforeOpenEvent args)
    {
        UnloadOntoShore(ent);
    }

    private void OnDestroyed(Entity<LavaBoatComponent> ent, ref DestructionEventArgs args)
    {
        UnloadOntoShore(ent);
    }

    private void UnloadOntoShore(EntityUid boat)
    {
        if (_timing.ApplyingState || !TryGetShore(boat, out var shore) ||
            !TryComp<EntityStorageComponent>(boat, out var storage))
            return;

        foreach (var cargo in storage.Contents.ContainedEntities.ToArray())
        {
            if (!TerminatingOrDeleted(cargo) && _storage.Remove(cargo, boat, storage))
                _transform.SetCoordinates(cargo, shore);
        }
    }

    private void OnUnstrapAttempt(Entity<LavaBoatComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (TryGetShore(ent, out _))
            return;

        args.Cancelled = true;
        if (args.Popup && args.User is { } user)
            _popup.PopupClient(Loc.GetString("lava-boat-no-shore"), ent, user);
    }

    private void OnStrapped(Entity<LavaBoatComponent> ent, ref StrappedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var extinguish = new ExtinguishEvent();
        RaiseLocalEvent(args.Buckle, ref extinguish);
    }

    private void OnUnstrapped(Entity<LavaBoatComponent> ent, ref UnstrappedEvent args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(args.Buckle) ||
            Transform(ent).MapUid is not { } mapUid || TerminatingOrDeleted(mapUid))
            return;

        if (TryGetShore(ent, out var shore))
            _transform.SetCoordinates(args.Buckle, shore);

        _physics.WakeBody(args.Buckle);
    }

    public bool TryGetShore(EntityUid boat, out EntityCoordinates coordinates)
    {
        coordinates = default;
        var xform = Transform(boat);
        if (xform.GridUid is not { } gridUid || TerminatingOrDeleted(gridUid) ||
            xform.MapUid is not { } mapUid || TerminatingOrDeleted(mapUid) ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var center))
            return false;

        var closest = float.MaxValue;
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            if (!_map.TryGetTileRef(gridUid, grid, center.GridIndices + new Vector2i(x, y), out var tile))
                continue;

            var candidate = _map.ToCenterCoordinates(tile, grid);
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(xform), _transform.ToMapCoordinates(candidate).Position);
            if (distance >= closest || distance > ShoreRange * ShoreRange || !IsSafe(candidate) ||
                _turf.IsTileBlocked(tile, CollisionGroup.MobMask) ||
                !_interaction.InRangeUnobstructed(boat, candidate, range: ShoreRange))
                continue;

            coordinates = candidate;
            closest = distance;
        }

        return closest < float.MaxValue;
    }

    private bool IsSafe(EntityCoordinates coordinates)
    {
        var gridUid = _transform.GetGrid(coordinates);
        return TryComp<MapGridComponent>(gridUid, out var grid) &&
               _map.TryGetTileRef(gridUid.Value, grid, coordinates, out var tile) &&
               !tile.Tile.IsEmpty && !_turf.IsSpace(tile) &&
               !HasSurface<LavaComponent>(coordinates) && !HasSurface<ChasmComponent>(coordinates);
    }

    private bool HasSurface<T>(EntityCoordinates coordinates) where T : IComponent
    {
        var gridUid = _transform.GetGrid(coordinates);
        if (!TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid.Value, grid, coordinates, out var tile))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid.Value, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<T>(uid))
                return true;
        }

        return false;
    }
}

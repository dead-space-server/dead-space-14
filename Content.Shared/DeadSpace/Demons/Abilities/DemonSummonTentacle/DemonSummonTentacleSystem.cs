using Content.Shared.Directions;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Abilities.Demon;

public sealed class DemonTentacleSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<DemonSummonTentacleAction>(OnSummonAction);
    }

    private void OnSummonAction(DemonSummonTentacleAction args)
    {
        if (args.Handled)
            return;

        // TODO: animation

        _popup.PopupPredicted(Loc.GetString("demon-tentacle-ability-use-popup", ("entity", args.Performer)), args.Performer, args.Performer, type: PopupType.SmallCaution);
        _stun.TryAddStunDuration(args.Performer, TimeSpan.FromSeconds(0.8f));

        var coords = args.Target;
        List<EntityCoordinates> spawnPos = new();
        spawnPos.Add(coords);

        var dirs = new List<Direction>();
        dirs.AddRange(args.OffsetDirections);

        // Спавним только одну сущность (основную)
        if (_transform.GetGrid(coords) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        if (!_map.TryGetTileRef(grid, gridComp, coords, out var tileRef) ||
            _turf.IsSpace(tileRef) ||
            _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
        {
            args.Handled = true;
            return;
        }

        if (_net.IsServer)
            Spawn(args.EntityId, coords);

        args.Handled = true;
    }
}
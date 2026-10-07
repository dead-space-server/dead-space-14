using Content.Shared.Sectants;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Sectants;

public sealed class SectantMaleRandomBlinkSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantMaleRandomBlinkActionEvent>(OnBlink);
    }

    private void OnBlink(SectantMaleRandomBlinkActionEvent args)
    {
        var uid = args.Performer;
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid)
            return;
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _xform.GetWorldPosition(uid);

        for (var i = 0; i < 16; i++)
        {
            var angle = _random.NextAngle();
            var dist = _random.NextFloat(2f, 12f);
            var target = origin + angle.ToVec() * dist;
            var tile = new Vector2i((int)MathF.Floor(target.X), (int)MathF.Floor(target.Y));

            if (!_mapSystem.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            _xform.SetWorldPosition(uid, target);
            Spawn("SectantBlinkEffect", _xform.GetMapCoordinates(uid));
            args.Handled = true;
            return;
        }
    }
}

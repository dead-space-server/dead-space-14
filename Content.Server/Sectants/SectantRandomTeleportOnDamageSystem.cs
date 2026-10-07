using Content.Shared.Damage;
using Content.Shared.Damage.Systems;         // ← DamageChangedEvent живёт тут
using Content.Shared.Sectants;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Sectants;

public sealed class SectantRandomTeleportOnDamageSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SectantSystem _sectant = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantRandomTeleportOnDamageComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, SectantRandomTeleportOnDamageComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;
        if (!_sectant.CanTeleport(uid, comp))
            return;

        comp.LastTeleport = _timing.CurTime;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        if (xform.GridUid is not { } gridUid)
            return;
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _xform.GetWorldPosition(uid);

        for (var i = 0; i < 12; i++)
        {
            var angle = _random.NextAngle();
            var dist = _random.NextFloat(comp.MinDistance, comp.Radius);
            var target = origin + angle.ToVec() * dist;
            var tile = new Vector2i((int)MathF.Floor(target.X), (int)MathF.Floor(target.Y));

            if (!_mapSystem.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            _xform.SetWorldPosition(uid, target);
            if (comp.Sound != null)
                _audio.PlayPvs(comp.Sound, uid);
            if (comp.EffectPrototype is { } fx)
                Spawn(fx, _xform.GetMapCoordinates(uid));
            return;
        }
    }
}

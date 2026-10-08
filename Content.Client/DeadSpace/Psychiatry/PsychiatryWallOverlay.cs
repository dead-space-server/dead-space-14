// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.DeadSpace.Psychiatry;

/// <summary>
/// Meat walls are drawn on top of the real wall. The wall sprite itself is never hidden,
/// so leaving the body or finishing treatment cannot leave a tile invisible.
/// </summary>
public sealed class PsychiatryWallOverlay : Overlay
{
    private readonly IEntityManager _ent;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly EntityLookupSystem _lookup;
    private readonly IResourceCache _resources;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly List<Vector2> _draw = new();
    private Texture? _meat;
    private bool _texTried;
    private float _time;

    public PsychiatryWallOverlay(IEntityManager ent, SharedMapSystem map, SharedTransformSystem xform, EntityLookupSystem lookup)
    {
        _ent = ent;
        _map = map;
        _xform = xform;
        _lookup = lookup;
        _resources = IoCManager.Resolve<IResourceCache>();
        ZIndex = 10;
    }

    public void Update(EntityUid subject, SchizophreniaComponent schizo, float frameTime, float radius)
    {
        _time += frameTime;
        _draw.Clear();
        if (schizo.Stage < SchizophreniaStage.Simple)
            return;

        var origin = _xform.GetMapCoordinates(subject);
        if (!_ent.TryGetComponent(subject, out TransformComponent? subjectXform))
            return;
        var gridUid = subjectXform.GridUid;
        MapGridComponent? grid = null;
        if (gridUid != null)
            _ent.TryGetComponent(gridUid.Value, out grid);

        foreach (var uid in _lookup.GetEntitiesInRange(subject, radius))
        {
            if (!_ent.HasComponent<WallComponent>(uid) || !_ent.TryGetComponent(uid, out TransformComponent? xform))
                continue;
            if (xform.MapID != origin.MapId)
                continue;

            if (gridUid == null || grid == null || xform.GridUid != gridUid)
                continue;

            var tile = _map.TileIndicesFor(gridUid.Value, grid, xform.Coordinates);
            if (!PsychiatryPattern.IsMeatWall(tile, schizo.Seed, schizo.Stage))
                continue;

            _draw.Add(_xform.GetWorldPosition(uid));
        }

        EnsureTexture();
    }

    private void EnsureTexture()
    {
        if (_texTried)
            return;
        _texTried = true;

        if (!_resources.TryGetResource<RSIResource>("/Textures/Structures/Walls/meat.rsi", out var rsi))
            return;
        if (!rsi.RSI.TryGetState("full", out var state))
            return;
        _meat = state.GetFrames(RsiDirection.South)[0];
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_draw.Count == 0)
            return;

        var handle = args.WorldHandle;
        // Красный канал тоже дышит. Пульс только по G/B на стене не видно.
        var wave = 0.5f + 0.5f * MathF.Sin(_time * 2.8f);
        var pulse = 0.42f + 0.58f * wave;
        var color = new Color(pulse, pulse * 0.55f, pulse * 0.48f);
        var size = new Vector2(1f, 1f);

        foreach (var pos in _draw)
        {
            var box = Box2.CenteredAround(pos, size);
            if (_meat != null)
                handle.DrawTextureRect(_meat, box, color);
            else
                handle.DrawRect(box, color);
        }
    }

    public void Clear() => _draw.Clear();
}

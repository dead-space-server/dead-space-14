// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Psychiatry;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Psychiatry;

public sealed class PsychiatryFloorOverlay : Overlay
{
    private readonly IEntityManager _ent;
    private readonly SharedMapSystem _map;
    private readonly IResourceCache _resources;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public int Seed;
    public SchizophreniaStage Stage;

    private readonly List<(Vector2 WorldPos, bool IsLava)> _draw = new();
    private Texture? _lavaTex;
    private Texture? _waterTex;
    private bool _texTried;
    private bool _cacheValid;
    private EntityUid _cachedSubject;
    private EntityUid _cachedGrid;
    private Vector2i _cachedTile;
    private int _cachedSeed;
    private SchizophreniaStage _cachedStage;

    public PsychiatryFloorOverlay(IEntityManager ent, SharedMapSystem map)
    {
        _ent = ent;
        _map = map;
        _resources = IoCManager.Resolve<IResourceCache>();
        ZIndex = -5;
    }

    public void UpdateClusters(EntityUid subject, SchizophreniaComponent schizo, float radius = 12f)
    {
        Seed = schizo.Seed;
        Stage = schizo.Stage;
        if (Stage < SchizophreniaStage.Latent)
        {
            _draw.Clear();
            _cacheValid = false;
            return;
        }

        var xform = _ent.GetComponent<TransformComponent>(subject);
        if (xform.GridUid is not { } gridUid || !_ent.TryGetComponent(gridUid, out MapGridComponent? grid))
        {
            _draw.Clear();
            _cacheValid = false;
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (_cacheValid && _cachedSubject == subject && _cachedGrid == gridUid && _cachedTile == tile && _cachedSeed == Seed && _cachedStage == Stage)
            return;

        _cachedSubject = subject;
        _cachedGrid = gridUid;
        _cachedTile = tile;
        _cachedSeed = Seed;
        _cachedStage = Stage;
        _cacheValid = true;
        _draw.Clear();

        EnsureTextures();

        var reach = (int) MathF.Round(radius);

        for (var x = -reach; x <= reach; x++)
        {
            for (var y = -reach; y <= reach; y++)
            {
                var idx = new Vector2i(tile.X + x, tile.Y + y);
                var t = _map.GetTileRef(gridUid, grid, idx);
                if (t.Tile.IsEmpty)
                    continue;

                bool isLava;
                if (PsychiatryPattern.IsLavaFloor(idx, Seed))
                    isLava = true;
                else if (PsychiatryPattern.IsWaterFloor(idx, Seed))
                    isLava = false;
                else
                    continue;

                var world = _map.GridTileToWorldPos(gridUid, grid, idx);
                _draw.Add((world, isLava));
            }
        }
    }

    private void EnsureTextures()
    {
        if (_texTried)
            return;
        _texTried = true;

        if (_resources.TryGetResource<RSIResource>("/Textures/Tiles/Planet/lava.rsi", out var lava))
        {
            if (lava.RSI.TryGetState("lava", out var st) ||
                lava.RSI.TryGetState("full", out st) ||
                lava.RSI.TryGetState("lava0", out st))
                _lavaTex = st.GetFrames(RsiDirection.South)[0];
        }

        if (_resources.TryGetResource<RSIResource>("/Textures/Tiles/Planet/water.rsi", out var water) ||
            _resources.TryGetResource<RSIResource>("/Textures/Tiles/water.rsi", out water))
        {
            if (water.RSI.TryGetState("shoreline_water", out var st) ||
                water.RSI.TryGetState("full", out st) ||
                water.RSI.TryGetState("water", out st))
                _waterTex = st.GetFrames(RsiDirection.South)[0];
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Stage < SchizophreniaStage.Latent || _draw.Count == 0)
            return;

        var handle = args.WorldHandle;
        var size = new Vector2(1f, 1f);

        foreach (var (pos, isLava) in _draw)
        {
            var box = Box2.CenteredAround(pos, size);
            if (isLava)
            {
                if (_lavaTex != null)
                    handle.DrawTextureRect(_lavaTex, box, Color.White);
                else
                    handle.DrawRect(box, new Color(1f, 0.45f, 0.08f));
            }
            else
            {
                if (_waterTex != null)
                    handle.DrawTextureRect(_waterTex, box, Color.White);
                else
                    handle.DrawRect(box, new Color(0.2f, 0.45f, 0.9f, 0.85f));
            }
        }
    }

    public void Clear() => _draw.Clear();
}

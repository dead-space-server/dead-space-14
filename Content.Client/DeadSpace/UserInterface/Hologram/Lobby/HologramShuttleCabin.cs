// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Lobby;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Lobby;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>
/// The shuttle cabin of the "Dead Space" lobby, drawn from game textures: a connected plastitanium wall with a big
/// viewport (the animated lobby sky shows only through it), posters and graffiti, a dark floor with markings, a row of
/// shuttle seats with the player's character buckled into one, and people walking past - other players in the lobby as
/// their own characters, dark extras when there are too few. Dim cabin light, a warm lamp over the player's seat, a
/// glowing viewport, a strong vignette; the picture fades into the page on every side. Integer pixel scale.
/// </summary>
public sealed partial class HologramShuttleCabin : Control
{
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logs = default!;

    /// <summary>Scene size in tiles; a tile is 32 texture pixels.</summary>
    private const int Tiles = 5;
    private const int WallRows = 3;
    private const int SceneTexels = Tiles * 32;

    public const int MaxWalkers = 4;

    // Scene layout in tiles, as approved.
    private const float WindowLeft = 0.4f;
    private const float WindowTop = 0.5f;
    private const float WindowRight = 2.45f;
    private const float WindowBottom = 2.4f;
    private static readonly float[] Seats = [0.95f, 2.0f, 3.05f, 4.1f];
    private const int PlayerSeat = 2;
    private const float SeatTop = 2.8f;

    /// <summary>How long a walker takes to cross the cabin at speed 1, in seconds.</summary>
    private const float WalkPeriod = 18f;

    private static readonly WalkerLane[] Lanes =
    [
        new(true, 3.55f, 0.00f, 1.00f),
        new(false, 4.05f, 0.30f, 1.15f),
        new(true, 4.05f, 0.62f, 0.90f),
        new(false, 3.60f, 0.80f, 1.05f),
    ];

    /// <summary>Lane drawing order: farther (higher) lanes first.</summary>
    private static readonly int[] LaneOrder = [0, 3, 1, 2];

    /// <summary>Extras: station jobs on different species, shown as dark silhouettes.</summary>
    private static readonly (string Species, string Job)[] Extras =
    [
        ("Human", "Scientist"),
        ("Reptilian", "SecurityOfficer"),
        ("Vox", "CargoTechnician"),
        ("Moth", "StationEngineer"),
    ];

    private readonly CabinLayer _back;
    private readonly HologramLobbyBackground _sky;
    private readonly CabinLayer _front;
    private readonly Action<float> _onStrength;
    private readonly Action<bool> _onReducedMotion;
    private readonly ISawmill _log;

    private readonly EntityUid?[] _walkers = new EntityUid?[MaxWalkers];
    private readonly bool[] _walkerIsPlayer = new bool[MaxWalkers];
    private readonly EntityUid?[] _extras = new EntityUid?[MaxWalkers];
    private readonly List<EntityUid> _playerDolls = new();

    private SpriteSystem? _spriteSystem;
    private SharedTransformSystem? _transformSystem;
    private HologramLobbyCrowdSystem? _crowd;
    private EntityUid? _seated;
    private bool _subscribed;
    private bool _texturesLoaded;
    private float _strength = 0.6f;
    private bool _reducedMotion;
    private float _time;

    /// <summary>Screen pixels per texture pixel.</summary>
    private int _scale = 1;

    // Textures.
    private Texture? _floor;
    // Corner pieces of a wall tile with wall below it, and of the bottom row (floor below), in the order SE, NE, NW, SW.
    private readonly Texture?[] _wallInner = new Texture?[4];
    private readonly Texture?[] _wallBottom = new Texture?[4];
    private Texture? _warnLine;
    private Texture? _chair;
    private Texture? _lamp;
    private Texture? _lampGlow;
    private readonly List<(Texture Texture, Vector2 Tile, Color Modulate)> _wallDecor = new();
    private readonly List<(Texture Texture, Vector2 Tile, Color Modulate)> _floorDecor = new();

    // Cached light geometry, rebuilt when the scale or the palette changes.
    private readonly CabinBuffer _shade = new(GridCells * GridCells * 6);
    private readonly CabinBuffer _glow = new(256);
    private readonly CabinBuffer _veil = new(GridCells * GridCells * 6 + 256);
    private int _builtScale = -1;
    private Color _builtPage;
    private Vector2 _builtArea;

    /// <summary>Top-left of the 5x5 core of the scene inside the control, in screen pixels.</summary>
    private Vector2 _origin;

    /// <summary>The whole control in screen pixels: the wall, floor and seats continue to its edges.</summary>
    private Vector2 _area;

    private const int GridCells = 32;

    public HologramShuttleCabin()
    {
        IoCManager.InjectDependencies(this);
        _log = _logs.GetSawmill("lobby.cabin");
        MouseFilter = MouseFilterMode.Ignore;

        _onStrength = value => _strength = Math.Clamp(value, 0f, 1f);
        _onReducedMotion = value => _reducedMotion = value;

        _back = new CabinLayer(this, front: false);
        _sky = new HologramLobbyBackground { RectClipContent = true };
        _front = new CabinLayer(this, front: true);
        AddChild(_back);
        AddChild(_sky);
        AddChild(_front);
    }

    /// <summary>Puts the player's preview doll into their seat; null leaves the seat empty.</summary>
    public void SetSeated(EntityUid? uid)
    {
        _seated = uid;
    }

    #region Lifetime

    protected override void EnteredTree()
    {
        base.EnteredTree();

        if (!_subscribed)
        {
            _subscribed = true;
            _cfg.OnValueChanged(CCCCVars.HologramEffects, _onStrength, true);
            _cfg.OnValueChanged(CCVars.ReducedMotion, _onReducedMotion, true);
            _crowd = _entities.System<HologramLobbyCrowdSystem>();
            _crowd.Updated += OnCrowdUpdated;
        }

        OnCrowdUpdated();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (_subscribed)
        {
            _subscribed = false;
            _cfg.UnsubValueChanged(CCCCVars.HologramEffects, _onStrength);
            _cfg.UnsubValueChanged(CCVars.ReducedMotion, _onReducedMotion);
            if (_crowd != null)
                _crowd.Updated -= OnCrowdUpdated;
        }

        ReleaseDolls();
    }

    /// <summary>Respawns the walkers from the latest lobby snapshot; nothing outside the "Dead Space" style.</summary>
    public void Refresh()
    {
        OnCrowdUpdated();
    }

    /// <summary>Deletes every doll this cabin spawned.</summary>
    public void ReleaseDolls()
    {
        DeletePlayerDolls();
        for (var i = 0; i < _extras.Length; i++)
        {
            if (_extras[i] is { } extra)
                _entities.DeleteEntity(extra);

            _extras[i] = null;
        }

        ClearLanes();
    }

    private void ClearLanes()
    {
        for (var i = 0; i < MaxWalkers; i++)
        {
            _walkers[i] = null;
            _walkerIsPlayer[i] = false;
        }
    }

    private void DeletePlayerDolls()
    {
        foreach (var doll in _playerDolls)
        {
            _entities.DeleteEntity(doll);
        }

        _playerDolls.Clear();
    }

    #endregion

    #region Walkers

    /// <summary>Other players walk past as their own characters; extras fill the empty lanes.</summary>
    private void OnCrowdUpdated()
    {
        DeletePlayerDolls();
        ClearLanes();

        // The cabin stays in the tree under the stock lobby too; it only spawns dolls while it is shown.
        if (!DeadSpaceStylePalette.Hologram)
        {
            ReleaseDolls();
            return;
        }

        var lane = 0;
        if (_crowd?.Latest is { } snapshot)
        {
            foreach (var entry in snapshot.Entries)
            {
                if (lane >= MaxWalkers || _playerDolls.Count >= LobbyCrowdSnapshotEvent.MaxEntries)
                    break;

                if (BuildProfile(entry) is not { } built)
                    continue;

                if (Spawn(built.Profile, built.Job) is not { } doll)
                    continue;

                _playerDolls.Add(doll);
                _walkers[lane] = doll;
                _walkerIsPlayer[lane] = true;
                lane++;
            }
        }

        for (var extra = 0; lane < MaxWalkers && extra < Extras.Length; extra++, lane++)
        {
            _extras[extra] ??= SpawnExtra(extra);
            _walkers[lane] = _extras[extra];
        }
    }

    /// <summary>
    /// A profile for another player's doll, or null when anything in the entry is off: unknown or not round-start
    /// species, unknown job, a sex out of range, too many markings. The look is run through the usual validation.
    /// </summary>
    private (HumanoidCharacterProfile Profile, JobPrototype Job)? BuildProfile(LobbyCrowdEntry entry)
    {
        if (entry.Sex is not (Sex.Male or Sex.Female or Sex.Unsexed)
            || entry.Appearance is not { } appearance
            || appearance.Markings.Count > LobbyCrowdSnapshotEvent.MaxMarkings
            || !_prototypes.TryIndex(entry.Species, out SpeciesPrototype? species)
            || !species.RoundStart
            || !_prototypes.TryIndex(entry.Job, out JobPrototype? job))
        {
            return null;
        }

        var markings = appearance.Markings.Select(marking => marking.MarkingId).ToArray();
        var valid = HumanoidCharacterAppearance.EnsureValid(appearance, species.ID, entry.Sex, markings);
        var profile = HumanoidCharacterProfile.DefaultWithSpecies(species.ID)
            .WithSex(entry.Sex)
            .WithCharacterAppearance(valid);

        return (profile, job);
    }

    private EntityUid? SpawnExtra(int index)
    {
        var (speciesId, jobId) = Extras[index];
        if (!_prototypes.TryIndex(speciesId, out SpeciesPrototype? species)
            || !_prototypes.TryIndex(jobId, out JobPrototype? job))
        {
            return null;
        }

        return Spawn(HumanoidCharacterProfile.DefaultWithSpecies(species.ID), job);
    }

    private EntityUid? Spawn(HumanoidCharacterProfile profile, JobPrototype job)
    {
        try
        {
            return UserInterfaceManager.GetUIController<LobbyUIController>().LoadProfileEntity(profile, job, true);
        }
        catch (Exception e)
        {
            // A broken doll must never take the lobby down with it.
            _log.Error($"Failed to spawn a lobby cabin doll: {e}");
            return null;
        }
    }

    #endregion

    #region Update and layout

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // No walking with reduced motion or with the effects off.
        var speed = _reducedMotion || _strength <= 0f ? 0f : 0.4f + 0.6f * _strength;
        _time += MathF.Min(args.DeltaSeconds, 0.1f) * speed;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var ui = UIScale;
        var pixels = finalSize * ui;

        // The largest integer pixel scale that fits; the scene is a square.
        _scale = Math.Max(1, (int) (MathF.Min(pixels.X, pixels.Y) / SceneTexels));
        var side = _scale * SceneTexels;
        var left = MathF.Floor((pixels.X - side) / 2f);
        var top = MathF.Floor((pixels.Y - side) / 2f);

        _origin = new Vector2(left, top);
        _area = pixels;

        var whole = UIBox2.FromDimensions(Vector2.Zero, finalSize);
        _back.Arrange(whole);
        _front.Arrange(whole);

        var window = WindowPixels();
        _sky.Arrange(new UIBox2(window.Left / ui, window.Top / ui, window.Right / ui, window.Bottom / ui));

        return finalSize;
    }

    private int TileSize => 32 * _scale;

    private UIBox2 WindowPixels()
    {
        var t = TileSize;
        return new UIBox2(
            _origin.X + (int) (t * WindowLeft),
            _origin.Y + (int) (t * WindowTop),
            _origin.X + (int) (t * WindowRight),
            _origin.Y + (int) (t * WindowBottom));
    }

    #endregion

    #region Textures

    private void LoadTextures()
    {
        if (_texturesLoaded)
            return;

        _texturesLoaded = true;

        if (_resources.TryGetResource<TextureResource>(new ResPath("/Textures/Tiles/dark.png"), out var floor))
            _floor = floor.Texture;

        const string wallRsi = "/Textures/Structures/Walls/plastitanium.rsi";
        // Wall on every side but below for the bottom row: SE and SW lose their lower neighbours there.
        LoadWallTile(_wallInner, wallRsi, 7, 7);
        LoadWallTile(_wallBottom, wallRsi, 1, 4);

        _chair = Rsi("/Textures/Structures/Furniture/chairs.rsi", "shuttle", RsiDirection.South);
        _lamp = Rsi("/Textures/Structures/Wallmounts/Lighting/light_small.rsi", "base", RsiDirection.South);
        _lampGlow = Rsi("/Textures/Structures/Wallmounts/Lighting/light_small.rsi", "glow", RsiDirection.South);

        const string posters = "/Textures/Structures/Wallmounts/posters.rsi";
        const string crayon = "/Textures/Effects/crayondecals.rsi";
        AddDecor(_wallDecor, posters, "poster5_legit", 2.75f, 0.45f, Color.White);
        AddDecor(_wallDecor, posters, "poster12_contraband", 4.0f, 0.35f, Color.White);
        AddDecor(_wallDecor, posters, "poster22_legit", 3.55f, 1.2f, Color.White);
        AddDecor(_wallDecor, crayon, "Clandestine", 0.02f, -0.05f, Color.FromHex("#D64034").WithAlpha(0.85f));
        AddDecor(_wallDecor, crayon, "danger", 4.35f, 1.45f, Color.FromHex("#E8C840").WithAlpha(0.8f));
        AddDecor(_wallDecor, crayon, "arrow", 0.15f, 1.85f, Color.FromHex("#78D2FF").WithAlpha(0.75f));
        AddDecor(_wallDecor, crayon, "cat", 1.9f, 0.05f, Color.FromHex("#A0E678").WithAlpha(0.7f));

        const string markings = "/Textures/Decals/markings.rsi";
        _warnLine = Rsi(markings, "warn_line_n", RsiDirection.South);

        AddDecor(_floorDecor, markings, "box", 0f, 4f, Color.White.WithAlpha(0.7f));
        AddDecor(_floorDecor, markings, "arrows", 2f, 4f, Color.White.WithAlpha(0.6f));
        AddDecor(_floorDecor, markings, "delivery", 4f, 4f, Color.White.WithAlpha(0.7f));
    }

    /// <summary>The four corner pieces of one smoothed wall tile; the upper corners always join the wall.</summary>
    private void LoadWallTile(Texture?[] tile, string rsi, int cornerSe, int cornerSw)
    {
        tile[0] = Rsi(rsi, $"plastitanium{cornerSe}", RsiDirection.South);
        tile[1] = Rsi(rsi, "plastitanium7", RsiDirection.East);
        tile[2] = Rsi(rsi, "plastitanium7", RsiDirection.North);
        tile[3] = Rsi(rsi, $"plastitanium{cornerSw}", RsiDirection.West);
    }

    private void AddDecor(List<(Texture, Vector2, Color)> list, string rsi, string state, float x, float y, Color modulate)
    {
        if (Rsi(rsi, state, RsiDirection.South) is { } texture)
            list.Add((texture, new Vector2(x, y), modulate));
    }

    private Texture? Rsi(string path, string state, RsiDirection direction)
    {
        if (!_resources.TryGetResource<RSIResource>(new ResPath(path), out var resource)
            || !resource.RSI.TryGetState(state, out var rsiState))
        {
            return null;
        }

        var dir = rsiState.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : direction;
        return rsiState.GetFrame(dir, 0);
    }

    #endregion

    #region Light geometry

    /// <summary>Page colour: the scene fades into it on every side.</summary>
    private static Color Page => DeadSpaceStylePalette.SurfaceDark.WithAlpha(1f);

    private void EnsureLight()
    {
        var page = Page;
        if (_builtScale == _scale && _builtPage == page && _builtArea == _area)
            return;

        _builtScale = _scale;
        _builtPage = page;
        _builtArea = _area;

        var t = (float) TileSize;
        var w = _area.X;
        var h = _area.Y;
        var top = _origin.Y;
        var seat = SeatCenter();
        var window = WindowPixels();
        var cabinDark = Color.InterpolateBetween(Color.Black, page, 0.4f);

        // Dim cabin: a lamp cone onto the player's seat and the viewport's own glow cut into the shade.
        _shade.Clear();
        _shade.AddGrid(w, h, GridCells, (x, y) =>
        {
            var light = CabinLight(x, y, t, top, seat, window);
            return cabinDark.WithAlpha(0.78f * (1f - light));
        });

        // A faint tint on the glass, then the warm beam from the lamp.
        _glow.Clear();
        var accent = DeadSpaceStylePalette.BorderHeader;
        _glow.AddQuad(window.TopLeft, window.BottomRight, accent.WithAlpha(0.08f));
        var warm = Color.FromHex("#FFECC8");
        var beamBottom = seat.Y + 0.6f * t;
        _glow.AddSoftCone(new Vector2(seat.X, top + 0.2f * t), 0.12f * t, beamBottom, 0.8f * t, warm.WithAlpha(0.14f));

        // The lamp, then the vignette and the fade into the page.
        _veil.Clear();
        _veil.AddGrid(w, h, GridCells, (x, y) =>
        {
            // A strong oval vignette round the seat row...
            var ex = (x - w / 2f) / (w / 2f - 0.3f * t);
            var ey = (y - h / 2f) / (h / 2f - 0.2f * t);
            var e = MathF.Sqrt(ex * ex + ey * ey);
            var vignette = Soft((e - 1f) * MathF.Min(w, h) / 2f + 0.2f * t, 1.6f * t);
            // ...and a fade that reaches the page colour exactly at the border, so there is no edge anywhere.
            var inside = MathF.Max(MathF.Max(0.35f * t - x, x - (w - 0.35f * t)), MathF.Max(0.25f * t - y, y - (h - 0.35f * t)));
            var border = MathF.Min(MathF.Min(x, w - x), MathF.Min(y, h - y));
            var fade = Soft(inside, 0.9f * t) * SmoothStep(0f, 0.6f * t, border);
            return page.WithAlpha(1f - vignette * fade);
        });
    }

    private Vector2 SeatCenter()
    {
        var t = (float) TileSize;
        return _origin + new Vector2(Seats[PlayerSeat] * t + t / 2f, SeatTop * t + t / 2f);
    }

    /// <summary>Cabin light, 0..1: the lamp cone onto the seat and the viewport's own glow.</summary>
    private static float CabinLight(float x, float y, float t, float top, Vector2 seat, UIBox2 window)
    {
        var soft = 0.5f * t;

        // The cone widens down to the seat and fades out gradually below it.
        var coneBottom = seat.Y + t;
        var halfWidth = 0.22f * t + 0.83f * t * Math.Clamp((y - top) / (coneBottom - top), 0f, 1f);
        var fadeOut = 1f - SmoothStep(seat.Y, seat.Y + 1.8f * t, y);
        var cone = 0.92f * Soft(MathF.Abs(x - seat.X) - halfWidth, soft) * fadeOut;

        var wcx = (window.Left + window.Right) / 2f;
        var wcy = (window.Top + window.Bottom + 0.3f * t) / 2f;
        var wx = (x - wcx) / (window.Width / 2f + 0.3f * t);
        var wy = (y - wcy) / (window.Height / 2f + 0.45f * t);
        var glow = 0.39f * Soft((MathF.Sqrt(wx * wx + wy * wy) - 1f) * 0.6f * t, soft);

        return MathF.Max(cone, glow);
    }

    /// <summary>1 well inside, 0 well outside, smooth across <paramref name="softness"/> around the edge.</summary>
    private static float Soft(float outside, float softness)
    {
        return SmoothStep(0f, 1f, 0.5f - outside / softness);
    }

    private static float SmoothStep(float from, float to, float value)
    {
        var k = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return k * k * (3f - 2f * k);
    }

    #endregion

    #region Drawing

    private void DrawBack(IRenderHandle render)
    {
        LoadTextures();
        EnsureLight();

        var handle = render.DrawingHandleScreen;
        var t = (float) TileSize;

        handle.DrawRect(new UIBox2(Vector2.Zero, _area), Page);

        // Tile range that covers the whole control, relative to the core.
        var xMin = -(int) MathF.Ceiling(_origin.X / t);
        var xMax = (int) MathF.Ceiling((_area.X - _origin.X) / t);
        var yMin = -(int) MathF.Ceiling(_origin.Y / t);
        var yMax = (int) MathF.Ceiling((_area.Y - _origin.Y) / t);

        // Floor, the wall above it, the warning line along the wall.
        for (var y = WallRows; y < yMax; y++)
        {
            for (var x = xMin; x < xMax; x++)
            {
                if (_floor != null)
                    handle.DrawTextureRectRegion(_floor, TileBox(x, y), new UIBox2(0, 0, 32, 32));
            }
        }

        for (var y = yMin; y < WallRows; y++)
        {
            var pieces = y == WallRows - 1 ? _wallBottom : _wallInner;
            for (var x = xMin; x < xMax; x++)
            {
                foreach (var piece in pieces)
                {
                    if (piece != null)
                        handle.DrawTextureRect(piece, TileBox(x, y));
                }
            }
        }

        if (_warnLine != null)
        {
            for (var x = xMin; x < xMax; x++)
            {
                handle.DrawTextureRect(_warnLine, TileBox(x, WallRows), Color.White.WithAlpha(0.85f));
            }
        }

        foreach (var (texture, at, modulate) in _floorDecor)
        {
            handle.DrawTextureRect(texture, TileBox(at.X, at.Y), modulate);
        }

        foreach (var (texture, at, modulate) in _wallDecor)
        {
            handle.DrawTextureRect(texture, TileBox(at.X, at.Y), modulate);
        }

        // The viewport frame: dark metal, a lighter inner lip; the sky itself is the child behind the front layer.
        var window = WindowPixels();
        var s = (float) _scale;
        Ring(handle, window, 3f * s, Color.FromHex("#464C54"));
        Ring(handle, window, 1f * s, Color.FromHex("#78808A"));
        handle.DrawRect(window, Page);

        // A row of seats along the whole wall; the player sits in one, in front of its back.
        const float seatStep = 1.05f;
        var firstSeat = (int) MathF.Floor((xMin - Seats[0]) / seatStep);
        var lastSeat = (int) MathF.Ceiling((xMax - Seats[0]) / seatStep);
        for (var i = firstSeat; i <= lastSeat; i++)
        {
            var chair = TileBox(Seats[0] + i * seatStep, SeatTop);
            if (_chair != null)
                handle.DrawTextureRect(_chair, chair);

            if (i == PlayerSeat && _seated is { } seated)
                DrawFigure(render, seated, chair.Center + new Vector2(0f, 2f * s), Direction.South, Color.White);
        }

        // Other players walk past in the cabin light.
        DrawWalkers(render, players: true);

        _shade.Draw(handle);
    }

    private void DrawFront(IRenderHandle render)
    {
        EnsureLight();
        var handle = render.DrawingHandleScreen;

        _glow.Draw(handle);

        // The ceiling lamp over the player's seat, lit.
        var lamp = TileBox(Seats[PlayerSeat], 0f);
        if (_lamp != null)
            handle.DrawTextureRect(_lamp, lamp);
        if (_lampGlow != null)
            handle.DrawTextureRect(_lampGlow, lamp, Color.FromHex("#FFF1D6"));

        // Extras are plain dark silhouettes over the shading.
        DrawWalkers(render, players: false);
        _veil.Draw(handle);
    }

    private void DrawWalkers(IRenderHandle render, bool players)
    {
        var t = (float) TileSize;
        var width = _area.X;
        var moving = !_reducedMotion && _strength > 0f;
        var silhouette = Color.InterpolateBetween(Color.Black, Page, 0.5f).WithAlpha(0.95f);

        foreach (var lane in LaneOrder)
        {
            if (_walkers[lane] is not { } uid || _walkerIsPlayer[lane] != players)
                continue;

            var spec = Lanes[lane];
            var progress = spec.Offset + _time * spec.Speed / WalkPeriod;
            progress -= MathF.Floor(progress);

            var left = spec.East ? -t + (width + t) * progress : width - (width + t) * progress;
            var step = moving && (int) (_time * spec.Speed * 3.7f + spec.Offset * 10f) % 2 == 1;
            var top = _origin.Y + spec.Row * t - (step ? _scale : 0);
            var center = new Vector2(MathF.Round(left / _scale) * _scale + t / 2f, top + t / 2f);

            DrawFigure(render, uid, center, spec.East ? Direction.East : Direction.West, players ? Color.White : silhouette);
        }
    }

    private void DrawFigure(IRenderHandle render, EntityUid uid, Vector2 center, Direction direction, Color modulate)
    {
        if (!_entities.TryGetComponent(uid, out SpriteComponent? sprite)
            || !_entities.TryGetComponent(uid, out TransformComponent? xform))
        {
            return;
        }

        _spriteSystem ??= _entities.System<SpriteSystem>();
        _transformSystem ??= _entities.System<SharedTransformSystem>();
        _spriteSystem.ForceUpdate(uid);

        var world = render.DrawingHandleWorld;
        var old = world.Modulate;
        world.Modulate = old * modulate;
        render.DrawEntity(uid, center, new Vector2(_scale, _scale), Angle.Zero, Angle.Zero, direction, sprite, xform, _transformSystem);
        world.Modulate = old;
    }

    private UIBox2 TileBox(float x, float y)
    {
        var t = TileSize;
        var left = _origin.X + MathF.Round(x * t / _scale) * _scale;
        var top = _origin.Y + MathF.Round(y * t / _scale) * _scale;
        return UIBox2.FromDimensions(left, top, t, t);
    }

    private static void Ring(DrawingHandleScreen handle, UIBox2 inner, float width, Color color)
    {
        var outer = new UIBox2(inner.Left - width, inner.Top - width, inner.Right + width, inner.Bottom + width);
        handle.DrawRect(new UIBox2(outer.Left, outer.Top, outer.Right, inner.Top), color);
        handle.DrawRect(new UIBox2(outer.Left, inner.Bottom, outer.Right, outer.Bottom), color);
        handle.DrawRect(new UIBox2(outer.Left, inner.Top, inner.Left, inner.Bottom), color);
        handle.DrawRect(new UIBox2(inner.Right, inner.Top, outer.Right, inner.Bottom), color);
    }

    #endregion

    private readonly record struct WalkerLane(bool East, float Row, float Offset, float Speed);

    /// <summary>One drawing layer of the scene, behind or in front of the viewport sky.</summary>
    private sealed class CabinLayer : Control
    {
        private readonly HologramShuttleCabin _owner;
        private readonly bool _isFront;

        public CabinLayer(HologramShuttleCabin owner, bool front)
        {
            _owner = owner;
            _isFront = front;
            MouseFilter = MouseFilterMode.Ignore;
            RectClipContent = true;
        }

        protected override void Draw(IRenderHandle renderHandle)
        {
            if (_isFront)
                _owner.DrawFront(renderHandle);
            else
                _owner.DrawBack(renderHandle);
        }
    }

    /// <summary>A preallocated triangle list with per-vertex colours; built on change, drawn every frame.</summary>
    private sealed class CabinBuffer(int capacity)
    {
        private const int FanSegments = 24;

        private readonly DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[capacity];
        private int _count;

        public void Clear()
        {
            _count = 0;
        }

        public void Draw(DrawingHandleScreen handle)
        {
            if (_count > 0)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, _count));
        }

        public void AddQuad(Vector2 topLeft, Vector2 bottomRight, Color color)
        {
            AddQuad(topLeft, new Vector2(bottomRight.X, topLeft.Y), new Vector2(topLeft.X, bottomRight.Y), bottomRight,
                color, color, color, color);
        }

        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            if (_count + 6 > _vertices.Length)
                return;

            // Colours go in as they are: converting them to linear made the fade far darker than the page.
            var la = ca;
            var lb = cb;
            var lc = cc;
            var ld = cd;
            _vertices[_count++] = new DrawVertexUV2DColor(a, la);
            _vertices[_count++] = new DrawVertexUV2DColor(b, lb);
            _vertices[_count++] = new DrawVertexUV2DColor(c, lc);
            _vertices[_count++] = new DrawVertexUV2DColor(b, lb);
            _vertices[_count++] = new DrawVertexUV2DColor(d, ld);
            _vertices[_count++] = new DrawVertexUV2DColor(c, lc);
        }

        /// <summary>A straight band of <paramref name="width"/> from one point to another.</summary>
        public void AddLine(Vector2 from, Vector2 to, float width, Color color)
        {
            var along = to - from;
            if (along.LengthSquared() < 0.01f)
                return;

            var normal = new Vector2(-along.Y, along.X) / along.Length() * (width / 2f);
            AddQuad(from - normal, to - normal, from + normal, to + normal, color, color, color, color);
        }

        /// <summary>A downward cone of light, brightest along its axis and clear at its sides.</summary>
        public void AddSoftCone(Vector2 apex, float topHalf, float bottom, float bottomHalf, Color color)
        {
            var clear = color.WithAlpha(0f);
            var axisTop = apex;
            var axisBottom = new Vector2(apex.X, bottom);
            AddQuad(new Vector2(apex.X - topHalf, apex.Y), axisTop, new Vector2(apex.X - bottomHalf, bottom), axisBottom,
                clear, color, clear, clear);
            AddQuad(axisTop, new Vector2(apex.X + topHalf, apex.Y), axisBottom, new Vector2(apex.X + bottomHalf, bottom),
                color, clear, clear, clear);
        }

        /// <summary>A soft elliptical pool: the colour in the middle, clear at the rim.</summary>
        public void AddFan(Vector2 center, Vector2 radii, Color color)
        {
            if (_count + FanSegments * 3 > _vertices.Length)
                return;

            var inner = color;
            var outer = color.WithAlpha(0f);
            var previous = center + new Vector2(radii.X, 0f);
            for (var i = 1; i <= FanSegments; i++)
            {
                var angle = MathF.Tau * i / FanSegments;
                var next = center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y);
                _vertices[_count++] = new DrawVertexUV2DColor(center, inner);
                _vertices[_count++] = new DrawVertexUV2DColor(previous, outer);
                _vertices[_count++] = new DrawVertexUV2DColor(next, outer);
                previous = next;
            }
        }

        /// <summary>A square grid over the scene, coloured per vertex by <paramref name="color"/>.</summary>
        public void AddGrid(float width, float height, int cells, Func<float, float, Color> color)
        {
            var stepX = width / cells;
            var stepY = height / cells;
            for (var y = 0; y < cells; y++)
            {
                for (var x = 0; x < cells; x++)
                {
                    var x0 = x * stepX;
                    var y0 = y * stepY;
                    var x1 = x0 + stepX;
                    var y1 = y0 + stepY;
                    AddQuad(
                        new Vector2(x0, y0),
                        new Vector2(x1, y0),
                        new Vector2(x0, y1),
                        new Vector2(x1, y1),
                        color(x0, y0),
                        color(x1, y0),
                        color(x0, y1),
                        color(x1, y1));
                }
            }
        }
    }
}

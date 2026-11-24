// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Content.Server.Salvage;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.PlanetSurvey;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class PlanetSurveySystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSalvageSystem _salvage = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SurveyDirectorSystem _director = default!;

    public EntityUid? Planet { get; private set; }

    private EntityUid? _console;
    private TimeSpan? _searchEnds;
    private int _attempts;
    private bool _generating;
    private EntityUid? _prepared;
    private Task<EntityUid?>? _preparing;
    private int _epoch;
    private bool _wantPrepare;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleComponent, SurveyScanMessage>(OnScan);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevel);
    }

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        _epoch++;
        _prepared = null;
        _preparing = null;
        Planet = null;
        _console = null;
        _searchEnds = null;
        _attempts = 0;
        _generating = false;
    }

    private void OnScan(Entity<ShuttleConsoleComponent> ent, ref SurveyScanMessage args)
    {
        StartSearch(ent.Owner);
    }

    public bool TryDiscard(out int people)
    {
        people = 0;
        if (Planet is not { } planet || !Exists(planet))
        {
            Planet = null;
            return true;
        }

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == planet)
                people++;
        }

        if (people > 0)
            return false;

        Del(planet);
        Planet = null;
        return true;
    }

    public void StartSearch(EntityUid console)
    {
        if (Planet is { } planet && Exists(planet))
        {
            _popup.PopupEntity(Loc.GetString("survey-search-known", ("planet", Name(planet))), console, PopupType.Medium);
            return;
        }

        if (_generating)
        {
            _popup.PopupEntity(Loc.GetString("survey-search-mapping"), console, PopupType.Medium);
            return;
        }

        if (_searchEnds is { } ends)
        {
            var left = Math.Max(1, (int) (ends - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("survey-search-busy", ("seconds", left)), console, PopupType.Medium);
            return;
        }

        _console = console;
        _searchEnds = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(30, 91));
        _popup.PopupEntity(Loc.GetString("survey-search-start"), console, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_wantPrepare)
        {
            _wantPrepare = false;
            _ = Prepare();
        }

        if (_searchEnds is not { } ends || _timing.CurTime < ends)
            return;

        _searchEnds = null;
        var console = _console;
        _console = null;

        var chance = Math.Min(0.85f, 0.25f + 0.2f * _attempts);
        _attempts++;

        if (!_random.Prob(chance))
        {
            if (console is { } failed && Exists(failed))
                _popup.PopupEntity(Loc.GetString("survey-search-nothing"), failed, PopupType.Medium);
            return;
        }

        var station = console is { } found && Exists(found)
            ? _station.GetOwningStation(found)
            : null;
        station ??= _station.GetStations().FirstOrDefault();

        _ = Generate(station, null, null);
    }

    private void OnRunLevel(GameRunLevelChangedEvent ev)
    {
        if (ev.New is GameRunLevel.PreRoundLobby or GameRunLevel.InRound)
            _wantPrepare = true;
    }

    private async Task Prepare()
    {
        if (_preparing != null || _prepared is { } ready && Exists(ready) || Planet is { } planet && Exists(planet))
            return;

        var epoch = _epoch;
        _preparing = Build(null, null);
        var map = await _preparing;
        if (epoch != _epoch)
        {
            if (map is { } stale && Exists(stale))
                QueueDel(stale);
            return;
        }

        if (_preparing == null)
            return;

        _preparing = null;
        _prepared = map;
    }

    public async Task<EntityUid?> Generate(EntityUid? station, ProtoId<SurveyWorldPrototype>? forced, SurveyMotive? motive)
    {
        if (_generating || Planet is { } existing && Exists(existing))
            return Planet;

        _generating = true;
        try
        {
            var map = _prepared;
            if (_preparing is { } pending)
            {
                map = await pending;
                _preparing = null;
            }

            _prepared = null;
            if (map is { } spare && (!Exists(spare) ||
                !TryComp<SurveyDirectorComponent>(spare, out var ready) ||
                forced is { } want && ready.World != want ||
                motive is { } aim && ready.Motive != aim))
            {
                if (Exists(spare))
                    QueueDel(spare);
                map = null;
            }

            map ??= await Build(forced, motive);
            if (map is not { } found || !TryComp<SurveyDirectorComponent>(found, out var director))
                return null;

            Reveal((found, director), station);
            return found;
        }
        finally
        {
            _generating = false;
        }
    }

    private void Reveal(Entity<SurveyDirectorComponent> ent, EntityUid? station)
    {
        var map = ent.Owner;
        var director = ent.Comp;
        director.Station = station;
        Planet = map;

        if (TryComp<FTLDestinationComponent>(map, out var dest))
        {
            dest.Enabled = true;
            Dirty(map, dest);
        }

        _director.Wake(ent);

        if (station is not { } home || !Exists(home))
            return;

        var world = _proto.Index(director.World);
        _chat.DispatchStationAnnouncement(home,
            Loc.GetString("survey-found",
                ("planet", Name(map)),
                ("land", Loc.GetString(world.Land)),
                ("motive", Loc.GetString($"survey-motive-{director.Motive.ToString().ToLowerInvariant()}")),
                ("cargo", director.Cargo.Count)),
            Loc.GetString("survey-sender"),
            playDefaultSound: false,
            colorOverride: Color.LightSkyBlue);
    }

    private async Task<EntityUid?> Build(ProtoId<SurveyWorldPrototype>? forced, SurveyMotive? motive)
    {
        EntityUid? mapUid = null;
        try
        {
            var world = forced is { } id ? _proto.Index(id) : PickWorld();
            var seed = _random.Next();
            mapUid = _map.CreateMap(out var mapId, runMapInit: false);
            var map = mapUid.Value;

            _biome.EnsurePlanet(map, _proto.Index(_random.Pick(world.Biomes)), seed, mapLight: world.Light);
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            _atmos.SetMapAtmosphere(map, false, new GasMixture(moles, world.Temperature));

            _meta.SetEntityName(map, _salvage.GetFTLName(_proto.Index(SalvageSystem.PlanetNames), seed));
            var dest = EnsureComp<FTLDestinationComponent>(map);
            dest.Enabled = false;
            dest.RequireCoordinateDisk = false;
            dest.BeaconsOnly = true;
            Dirty(map, dest);

            _map.InitializeMap(mapId);
            _map.SetPaused(map, false);

            var director = EnsureComp<SurveyDirectorComponent>(map);
            director.World = world.ID;
            director.Kind = world.Kind;
            director.Motive = motive ?? _random.Pick(Enum.GetValues<SurveyMotive>());
            director.Dormant = true;

            await _director.Populate((map, director), world, seed);
            return map;
        }
        catch (Exception e)
        {
            Log.Error($"Planet survey generation failed: {e}");
            if (mapUid is { } broken && Exists(broken))
                QueueDel(broken);
            return null;
        }
    }

    private SurveyWorldPrototype PickWorld()
    {
        var worlds = _proto.EnumeratePrototypes<SurveyWorldPrototype>().ToList();
        var total = worlds.Sum(w => w.Weight);
        var roll = _random.NextFloat() * total;
        foreach (var world in worlds)
        {
            roll -= world.Weight;
            if (roll <= 0f)
                return world;
        }

        return worlds[^1];
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneArenaComponent : Component
{
    public EntityUid Caster;
    public EntityUid Grid;
    public readonly List<EntityUid> Challengers = new();
    public readonly Dictionary<EntityUid, EntityCoordinates> Spectators = new();
    public TimeSpan FightStart;
    public TimeSpan End;
    public TimeSpan? CleanupAt;
    public bool Fighting;
}

[RegisterComponent]
public sealed partial class RuneArenaFighterComponent : Component
{
    public EntityUid Arena;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private const int ArenaHalf = 9;
    private const int RingHalf = 5;
    private static readonly EntProtoId ArenaWall = "WallReinforced";
    private static readonly EntProtoId ArenaBarrier = "RuneArenaBarrier";
    private static readonly EntProtoId ArenaLight = "AlwaysPoweredWallLight";
    private static readonly EntProtoId ArenaStash = "CrateGenericSteel";
    private static readonly EntProtoId NewWizardBook = "WizardsGrimoire";
    private static readonly ProtoId<NpcFactionPrototype> WizardFaction = "Wizard";

    private void StartArena(RuneCastContext ctx, RuneArenaEffect effect)
    {
        var caster = ctx.Caster;
        var origin = _transform.GetMapCoordinates(caster);

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("rune-arena-map-name"));
        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = Color.FromHex("#9a7a52");
        Dirty(mapUid, light);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
        moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
        _atmosphere.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        var grid = _mapManager.CreateGridEntity(mapId);
        BuildArena(grid);

        var arena = EnsureComp<RuneArenaComponent>(mapUid);
        arena.Caster = caster;
        arena.Grid = grid.Owner;
        arena.FightStart = Timing.CurTime + TimeSpan.FromSeconds(effect.Countdown);
        arena.End = arena.FightStart + TimeSpan.FromSeconds(effect.Duration);

        var stands = StandTiles();
        var stand = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || player == caster || HasComp<GhostComponent>(player) ||
                !_mobState.IsAlive(player))
                continue;

            var at = _transform.GetMapCoordinates(player);
            if (at.MapId != origin.MapId || (at.Position - origin.Position).Length() > effect.SpectatorRange)
                continue;

            arena.Spectators[player] = Transform(player).Coordinates;
            _transform.SetCoordinates(player, ArenaCoords(grid, stands[stand++ % stands.Count]));
        }

        StashGear(caster);
        _rejuvenate.PerformRejuvenate(caster);
        _transform.SetCoordinates(caster, ArenaCoords(grid, new Vector2i(-3, 0)));
        EnsureComp<RuneArenaFighterComponent>(caster).Arena = mapUid;
        _stun.TryUpdateParalyzeDuration(caster, TimeSpan.FromSeconds(effect.Countdown));

        for (var i = 0; i < effect.Challengers; i++)
        {
            var challenger = Spawn(effect.Challenger, ArenaCoords(grid, new Vector2i(3, i - effect.Challengers / 2)));
            EnsureComp<RuneArenaFighterComponent>(challenger).Arena = mapUid;
            _stun.TryUpdateParalyzeDuration(challenger, TimeSpan.FromSeconds(effect.Countdown));
            arena.Challengers.Add(challenger);
        }

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(caster):user} started a rune arena {ToPrettyString(mapUid):entity}, moved {arena.Spectators.Count} spectators");
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("rune-arena-announce", ("wizard", Name(caster)), ("seconds", (int) effect.Countdown)),
            Loc.GetString("rune-arena-sender"),
            colorOverride: Color.FromHex("#ff6ad5"));
    }

    private void BuildArena(Entity<MapGridComponent> grid)
    {
        if (!_tileDefinitions.TryGetDefinition("FloorBoxing", out var ring) ||
            !_tileDefinitions.TryGetDefinition("FloorWood", out var stands))
            return;

        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -ArenaHalf; x <= ArenaHalf; x++)
        for (var y = -ArenaHalf; y <= ArenaHalf; y++)
        {
            var edge = Math.Max(Math.Abs(x), Math.Abs(y));
            tiles.Add((new Vector2i(x, y), new Tile(edge < RingHalf ? ring.TileId : stands.TileId)));
        }

        _map.SetTiles(grid.Owner, grid.Comp, tiles);

        for (var x = -ArenaHalf; x <= ArenaHalf; x++)
        for (var y = -ArenaHalf; y <= ArenaHalf; y++)
        {
            var edge = Math.Max(Math.Abs(x), Math.Abs(y));
            var at = ArenaCoords(grid, new Vector2i(x, y));
            if (edge == ArenaHalf)
                Spawn(ArenaWall, at);
            else if (edge == RingHalf)
                Spawn(ArenaBarrier, at);
            else if (edge == ArenaHalf - 1 && (x + y) % 4 == 0)
                Spawn(ArenaLight, at);
        }
    }

    private static List<Vector2i> StandTiles()
    {
        var result = new List<Vector2i>();
        for (var x = -ArenaHalf + 2; x <= ArenaHalf - 2; x++)
        {
            result.Add(new Vector2i(x, ArenaHalf - 2));
            result.Add(new Vector2i(x, -ArenaHalf + 2));
        }

        return result;
    }

    private EntityCoordinates ArenaCoords(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return _map.GridTileToLocal(grid.Owner, grid.Comp, tile);
    }

    private void StashGear(EntityUid caster)
    {
        if (!TryComp<RuneCasterComponent>(caster, out var comp) || comp.Home is not { } home)
            return;

        var crate = Spawn(ArenaStash, home);
        _meta.SetEntityName(crate, Loc.GetString("rune-arena-stash-name"));
        foreach (var item in _inventory.GetHandOrInventoryEntities(caster).ToList())
        {
            _container.TryRemoveFromContainer(item, true);
            _entityStorage.Insert(item, crate);
        }
    }

    private void UpdateArena()
    {
        var query = EntityQueryEnumerator<RuneArenaComponent>();
        while (query.MoveNext(out var uid, out var arena))
        {
            if (arena.CleanupAt is { } cleanup)
            {
                if (Timing.CurTime >= cleanup)
                    QueueDel(uid);
                continue;
            }

            if (!arena.Fighting)
            {
                if (Timing.CurTime < arena.FightStart)
                    continue;

                arena.Fighting = true;
                foreach (var challenger in arena.Challengers.ToList())
                {
                    if (TryComp<MindContainerComponent>(challenger, out var mind) && mind.HasMind)
                        continue;

                    arena.Challengers.Remove(challenger);
                    QueueDel(challenger);
                }

                if (arena.Challengers.Count == 0)
                {
                    FinishArena(uid, arena, arena.Caster, "rune-arena-no-challengers");
                    continue;
                }

                foreach (var fighter in arena.Challengers.Append(arena.Caster))
                    _popup.PopupEntity(Loc.GetString("rune-arena-fight"), fighter, fighter, PopupType.LargeCaution);

                continue;
            }

            var casterStanding = InArena(arena.Caster, uid);
            var standing = arena.Challengers.FindAll(c => InArena(c, uid));

            if (Timing.CurTime >= arena.End)
            {
                FinishArena(uid, arena, casterStanding ? arena.Caster : standing.FirstOrDefault(), "rune-arena-timeout");
                continue;
            }

            if (casterStanding && standing.Count == 0)
                FinishArena(uid, arena, arena.Caster, "rune-arena-wizard-wins");
            else if (!casterStanding && standing.Count <= 1)
                FinishArena(uid, arena, standing.FirstOrDefault(), "rune-arena-challenger-wins");
        }
    }

    private bool InArena(EntityUid fighter, EntityUid arena)
    {
        return !TerminatingOrDeleted(fighter) && Transform(fighter).MapUid == arena && !_mobState.IsIncapacitated(fighter);
    }

    private void FinishArena(EntityUid uid, RuneArenaComponent arena, EntityUid winner, string message)
    {
        arena.CleanupAt = Timing.CurTime + TimeSpan.FromSeconds(3);
        if (winner.IsValid())
            _adminLog.Add(LogType.Action, LogImpact.High, $"Rune arena of {ToPrettyString(arena.Caster):user} ended, winner {ToPrettyString(winner):target}");
        else
            _adminLog.Add(LogType.Action, LogImpact.High, $"Rune arena of {ToPrettyString(arena.Caster):user} ended with no winner");

        foreach (var (spectator, back) in arena.Spectators)
        {
            if (!TerminatingOrDeleted(spectator) && Transform(spectator).MapUid == uid)
                _transform.SetCoordinates(spectator, back);
        }

        var home = CompOrNull<RuneCasterComponent>(arena.Caster)?.Home;
        RemComp<RuneArenaFighterComponent>(arena.Caster);

        if (winner.IsValid() && winner != arena.Caster)
        {
            var heir = EnsureComp<RuneCasterComponent>(winner);
            heir.Home = home;
            RemComp<RuneArenaFighterComponent>(winner);
            _faction.AddFaction(winner, WizardFaction.Id);
            _rejuvenate.PerformRejuvenate(winner);
            if (home is { } heirHome)
                _transform.SetCoordinates(winner, heirHome);
            _hands.PickupOrDrop(winner, Spawn(NewWizardBook, Transform(winner).Coordinates));
            arena.Challengers.Remove(winner);
            RemComp<RuneCasterComponent>(arena.Caster);
        }
        else if (winner == arena.Caster)
        {
            _rejuvenate.PerformRejuvenate(arena.Caster);
        }

        if (!TerminatingOrDeleted(arena.Caster) && Transform(arena.Caster).MapUid == uid)
        {
            if (home is { } back)
                _transform.SetCoordinates(arena.Caster, back);
        }

        foreach (var loser in arena.Challengers)
        {
            if (!TerminatingOrDeleted(loser))
                QueueDel(loser);
        }

        arena.Challengers.Clear();
        var name = winner.IsValid() ? Name(winner) : Loc.GetString("rune-arena-nobody");
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(message, ("winner", name)),
            Loc.GetString("rune-arena-sender"),
            colorOverride: Color.FromHex("#ff6ad5"));
    }
}

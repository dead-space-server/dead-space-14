// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Text.Json;
using Content.Server.CartridgeLoader;
using Content.Server.EUI;
using Content.Server.Popups;
using Content.Shared.CartridgeLoader;
using Content.Shared.DeadSpace.PdaArcade;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.PdaArcade;

public sealed class PdaArcadeSystem : EntitySystem
{
    private const int MaxPerGame = 20;
    private static readonly ResPath ScoresPath = new("/pda_arcade_scores.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true };

    [Dependency] private readonly CartridgeLoaderSystem _loader = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly List<PdaArcadeEntry> _scores = new();
    private readonly List<Match> _matches = new();
    private readonly List<Invite> _invites = new();
    private int _nextInvite = 1;
    private int _nextMatch = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PdaArcadeCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<PdaArcadeCartridgeComponent, CartridgeUiReadyEvent>(OnReady);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMob);
        Load();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        for (var i = _matches.Count - 1; i >= 0; i--)
        {
            var match = _matches[i];
            if (match.Over)
                continue;

            if (match.Game != PdaArcadeGame.Pong)
                continue;

            var over = false;
            var hostScore = 0;
            var guestScore = 0;
            PdaTablePlay.Step(match.Game, ref match.Board, match.HostHeld, match.GuestHeld, frameTime, Tune(match.Game), out over, out hostScore, out guestScore);
            if (over)
                Finish(match, hostScore, guestScore);
            else
                Show(match);
        }
    }

    private void OnReady(Entity<PdaArcadeCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        Push(args.Loader, Wearer(args.Loader));
    }

    private void OnMessage(Entity<PdaArcadeCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        switch (args)
        {
            case PdaArcadeScoreMessage score:
                Record(score.Game, MetaName(args.Actor), score.Score);
                Push(GetEntity(score.LoaderUid), args.Actor);
                RefreshOpen();
                break;
            case PdaArcadeNearbyMessage:
                Push(GetEntity(args.LoaderUid), args.Actor);
                break;
            case PdaArcadeInviteMessage invite:
                Offer(args.Actor, invite);
                break;
            case PdaArcadeMoveMessage move:
                Play(args.Actor, move.Move);
                break;
            case PdaArcadeLeaveMessage:
                Leave(args.Actor);
                break;
            case PdaArcadeAgainMessage:
                Again(args.Actor);
                break;
        }
    }

    private void OnMob(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead && ev.NewMobState != MobState.Critical)
            return;

        for (var i = _matches.Count - 1; i >= 0; i--)
        {
            var match = _matches[i];
            if (match.Over || (match.Host != ev.Target && match.Guest != ev.Target))
                continue;

        match.Over = true;
        Drop(match);
        _popup.PopupEntity(Loc.GetString("pda-arcade-interrupted"), ev.Target, PopupType.Medium);
        }
    }

    private void Offer(EntityUid actor, PdaArcadeInviteMessage invite)
    {
        if (!PdaArcadeGame.Together.Contains(invite.Game))
            return;

        if (!TryGetEntity(invite.Target, out var maybe) || maybe == null || maybe == actor)
            return;

        var target = maybe.Value;
        if (!Near(actor, target) || !Alive(target) || !TryComp(target, out ActorComponent? guestActor))
            return;

        if (Busy(actor) || Busy(target))
        {
            _popup.PopupEntity(Loc.GetString("pda-arcade-busy"), actor, actor);
            return;
        }

        var id = _nextInvite++;
        var offer = new Invite(id, invite.Game, actor, target);
        _invites.Add(offer);
        var eui = new PdaInviteEui(this, id, Loc.GetString("pda-arcade-invite-text", ("name", MetaName(actor)), ("game", Loc.GetString("pda-arcade-game-" + invite.Game))));
        offer.Eui = eui;
        _eui.OpenEui(eui, guestActor.PlayerSession);
        _popup.PopupEntity(Loc.GetString("pda-arcade-invited", ("name", MetaName(target))), actor, actor);
    }

    public void AnswerInvite(int id, bool accept)
    {
        Invite? found = null;
        foreach (var invite in _invites)
        {
            if (invite.Id == id)
                found = invite;
        }

        if (found == null)
            return;

        _invites.Remove(found);
        if (!accept || !Near(found.Host, found.Guest) || !Alive(found.Host) || !Alive(found.Guest))
        {
            _popup.PopupEntity(Loc.GetString("pda-arcade-declined"), found.Host, found.Host);
            return;
        }

        var match = new Match(_nextMatch++, found.Game, found.Host, found.Guest, PdaTablePlay.Fresh(found.Game, Tune(found.Game)));
        _matches.Add(match);
        Show(match);
    }

    public void Play(EntityUid actor, string move)
    {
        var match = Find(actor);
        if (match == null)
            return;

        var host = match.Host == actor;
        if ((match.Game == PdaArcadeGame.Chess || match.Game == PdaArcadeGame.Checks) && !match.SidesReady)
        {
            RollSide(match, host);
            return;
        }

        var side = host;
        if (match.Game == PdaArcadeGame.Chess || match.Game == PdaArcadeGame.Checks)
            side = host == match.HostWhite;

        Move(match, side, move);
    }

    private void RollSide(Match match, bool host)
    {
        if (host)
        {
            if (match.HostDie != 0)
                return;

            match.HostDie = Random.Shared.Next(1, 7);
        }
        else
        {
            if (match.GuestDie != 0)
                return;

            match.GuestDie = Random.Shared.Next(1, 7);
        }

        if (match.HostDie == 0 || match.GuestDie == 0)
        {
            Show(match);
            return;
        }

        if (match.HostDie == match.GuestDie)
        {
            match.HostDie = 0;
            match.GuestDie = 0;
            Show(match);
            return;
        }

        match.HostWhite = match.HostDie > match.GuestDie;
        match.SidesReady = true;
        Show(match);
    }

    private IReadOnlyDictionary<string, float> Tune(string game)
    {
        return _prototypes.TryIndex<PdaArcadeGamePrototype>(game, out var proto) ? proto.Numbers : new Dictionary<string, float>();
    }

    public void Again(EntityUid actor)
    {
        var match = Find(actor);
        if (match == null || !match.Over)
            return;

        match.Board = PdaTablePlay.Fresh(match.Game, Tune(match.Game));
        match.Over = false;
        match.SidesReady = false;
        match.HostWhite = true;
        match.HostDie = 0;
        match.GuestDie = 0;
        match.HostHeld = 0;
        match.GuestHeld = 0;
        Show(match);
    }

    public void Leave(EntityUid actor)
    {
        var match = Find(actor);
        if (match == null)
            return;

        match.Over = true;
        Drop(match);
    }

    public void Move(Match match, bool host, string move)
    {
        if (match.Over)
            return;

        if (match.Game == PdaArcadeGame.Pong)
        {
            var held = move == "up" ? 1 : move == "down" ? -1 : 0;
            if (host)
                match.HostHeld = held;
            else
                match.GuestHeld = held;

            return;
        }

        var board = match.Board;
        if (!PdaTablePlay.TryMove(match.Game, ref board, host, move, out _, out var over, out var score))
        {
            Show(match);
            return;
        }

        match.Board = board;
        if (over)
            Finish(match, host ? score : 0, host ? 0 : score);
        else
            Show(match);
    }

    private void Finish(Match match, int hostScore, int guestScore)
    {
        match.Over = true;
        if (hostScore > 0)
            Record(match.Game, MetaName(match.Host), hostScore);

        if (guestScore > 0)
            Record(match.Game, MetaName(match.Guest), guestScore);

        string? winner = null;
        if (hostScore > guestScore)
            winner = MetaName(match.Host);
        else if (guestScore > hostScore)
            winner = MetaName(match.Guest);

        if (winner != null)
        {
            var text = Loc.GetString("pda-arcade-victor", ("name", winner));
            Announce(match.Host, text);
            Announce(match.Guest, text);
        }

        Show(match);
        RefreshOpen();
    }

    private void Announce(EntityUid player, string text)
    {
        if (!player.IsValid())
            return;

        _popup.PopupEntity(text, Pda(player) ?? player, player, PopupType.Large);
    }

    private EntityUid? Pda(EntityUid player)
    {
        if (!TryComp(player, out ContainerManagerComponent? manager))
            return null;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_loader.HasProgram<PdaArcadeCartridgeComponent>(item))
                    return item;
            }
        }

        return null;
    }

    private void Show(Match match)
    {
        PushPlayer(match.Host);
        PushPlayer(match.Guest);
    }

    private void Drop(Match match)
    {
        _matches.Remove(match);
        PushPlayer(match.Host);
        PushPlayer(match.Guest);
    }

    private void PushPlayer(EntityUid player)
    {
        if (!TryComp(player, out ContainerManagerComponent? manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_loader.HasProgram<PdaArcadeCartridgeComponent>(item))
                    Push(item, player);
            }
        }
    }

    private EntityUid? Wearer(EntityUid item)
    {
        var current = item;
        for (var i = 0; i < 6; i++)
        {
            if (!TryComp(current, out TransformComponent? xform) || !xform.ParentUid.IsValid())
                return null;

            current = xform.ParentUid;
            if (HasComp<ActorComponent>(current))
                return current;
        }

        return null;
    }

    private Match? Find(EntityUid actor)
    {
        foreach (var match in _matches)
        {
            if (match.Host == actor || match.Guest == actor)
                return match;
        }

        return null;
    }

    private void Record(string game, string name, int score)
    {
        if (score <= 0 || score > 1000000 || string.IsNullOrEmpty(game))
            return;

        if (string.IsNullOrWhiteSpace(name))
            name = "—";

        _scores.Add(new PdaArcadeEntry(game, name, score));
        Trim(game);
        Save();
    }

    private void Load()
    {
        if (!_resources.UserData.Exists(ScoresPath))
            return;

        try
        {
            using var reader = _resources.UserData.OpenText(ScoresPath);
            var text = reader.ReadToEnd();
            var loaded = JsonSerializer.Deserialize<List<PdaArcadeEntry>>(text, JsonOpts);
            if (loaded == null)
                return;

            _scores.AddRange(loaded);
            var games = new HashSet<string>();
            foreach (var entry in _scores)
                games.Add(entry.Game);

            foreach (var game in games)
                Trim(game);
        }
        catch (Exception)
        {
            _scores.Clear();
        }
    }

    private void Save()
    {
        var text = JsonSerializer.Serialize(_scores, JsonOpts);
        using var writer = _resources.UserData.OpenWriteText(ScoresPath);
        writer.Write(text);
    }

    private void Trim(string game)
    {
        var kept = 0;
        for (var i = _scores.Count - 1; i >= 0; i--)
        {
            if (_scores[i].Game != game)
                continue;

            kept++;
            if (kept <= MaxPerGame)
                continue;

            _scores.RemoveAt(i);
        }
    }

    private void RefreshOpen()
    {
        var query = EntityQueryEnumerator<PdaArcadeCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader)
                continue;

            Push(loader, Wearer(loader));
        }
    }

    private void Push(EntityUid loader, EntityUid? actor)
    {
        var copy = _scores.ToArray();
        Array.Sort(copy, (a, b) => b.Score.CompareTo(a.Score));
        var state = new PdaArcadeUiState { Entries = copy };
        if (actor != null)
        {
            state.Nearby = Nearby(actor.Value);
            FillMatch(state, actor.Value);
        }

        _loader.UpdateCartridgeUiState(loader, state);
    }

    private void FillMatch(PdaArcadeUiState state, EntityUid actor)
    {
        var match = Find(actor);
        if (match == null)
            return;

        var host = match.Host == actor;
        state.InMatch = true;
        state.MatchGame = match.Game;
        state.MatchBoard = PdaTablePlay.View(match.Game, match.Board, host);
        state.MatchYours = Turn(match, host);
        state.MatchOver = match.Over;
        state.Rolling = (match.Game == PdaArcadeGame.Chess || match.Game == PdaArcadeGame.Checks) && !match.SidesReady;
        state.MineBottom = (match.Game == PdaArcadeGame.Chess || match.Game == PdaArcadeGame.Checks) && match.SidesReady && host != match.HostWhite;
        state.YourDie = host ? match.HostDie : match.GuestDie;
        state.TheirDie = host ? match.GuestDie : match.HostDie;
    }

    private static bool Turn(Match match, bool host)
    {
        var board = match.Board;
        if (match.Game == PdaArcadeGame.Cross)
        {
            var xs = 0;
            var os = 0;
            foreach (var ch in board)
            {
                if (ch == 'x')
                    xs++;
                else if (ch == 'o')
                    os++;
            }

            return host ? xs == os : xs == os + 1;
        }

        if (match.Game == PdaArcadeGame.Ships && match.Board.Length > 0)
        {
            var phase = match.Board[0];
            if (phase is 'H' or 'G')
                return host ? phase == 'H' : phase == 'G';

            return host ? phase == 'h' : phase == 'g';
        }

        if ((match.Game == PdaArcadeGame.Chess || match.Game == PdaArcadeGame.Checks) && match.Board.Length > 0)
        {
            if (!match.SidesReady)
                return host ? match.HostDie == 0 : match.GuestDie == 0;

            var white = host == match.HostWhite;
            return white ? match.Board[^1] == 'w' : match.Board[^1] == 'b';
        }

        return match.Game == PdaArcadeGame.Pong;
    }

    private PdaArcadePerson[] Nearby(EntityUid actor)
    {
        var list = new List<PdaArcadePerson>();
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var state, out _))
        {
            if (uid == actor || state.CurrentState != MobState.Alive || !Near(actor, uid))
                continue;

            list.Add(new PdaArcadePerson(GetNetEntity(uid), MetaName(uid)));
            if (list.Count >= 8)
                break;
        }

        return list.ToArray();
    }

    private bool Near(EntityUid a, EntityUid b)
    {
        if (!TryComp(a, out TransformComponent? from) || !TryComp(b, out TransformComponent? to))
            return false;

        return from.Coordinates.TryDistance(EntityManager, to.Coordinates, out var distance) && distance <= 3f;
    }

    private bool Alive(EntityUid uid)
    {
        return TryComp(uid, out MobStateComponent? state) && state.CurrentState == MobState.Alive;
    }

    private bool Busy(EntityUid uid)
    {
        foreach (var match in _matches)
        {
            if (!match.Over && (match.Host == uid || match.Guest == uid))
                return true;
        }

        return false;
    }

    private string MetaName(EntityUid uid)
    {
        return MetaData(uid).EntityName;
    }

    public sealed class Match
    {
        public int Id;
        public string Game;
        public EntityUid Host;
        public EntityUid Guest;
        public string Board;
        public bool HostWhite = true;
        public bool SidesReady;
        public int HostDie;
        public int GuestDie;
        public bool Over;
        public int HostHeld;
        public int GuestHeld;

        public Match(int id, string game, EntityUid host, EntityUid guest, string board)
        {
            Id = id;
            Game = game;
            Host = host;
            Guest = guest;
            Board = board;
        }
    }

    private sealed class Invite
    {
        public int Id;
        public string Game;
        public EntityUid Host;
        public EntityUid Guest;
        public PdaInviteEui? Eui;

        public Invite(int id, string game, EntityUid host, EntityUid guest)
        {
            Id = id;
            Game = game;
            Host = host;
            Guest = guest;
        }
    }
}

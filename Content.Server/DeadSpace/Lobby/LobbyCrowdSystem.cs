// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Shared.DeadSpace.Lobby;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lobby;

/// <summary>
/// Tells every player waiting in the lobby who else waits there (species, sex, look and wanted job of their saved
/// character) and the round line of the lobby, for the "Dead Space" lobby cabin. Rebuilt every couple of seconds and
/// sent only when something changed; a player who just entered the lobby gets it within half a second. Everything comes
/// from server state and saved profiles; the client sends nothing for this.
/// </summary>
public sealed class LobbyCrowdSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IGameMapManager _maps = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const float RebuildInterval = 2f;
    private const float JoinDelay = 0.5f;

    private readonly List<Candidate> _candidates = new();
    private readonly List<Candidate> _sent = new();
    private readonly List<ICommonSession> _lobby = new();
    private readonly HashSet<ICommonSession> _joined = new();

    private float _accumulator;
    private int _sentRound = -1;
    private int _sentPlayers = -1;
    private int _sentReady = -1;
    private string? _sentMap;
    private string? _sentMode;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
    }

    private void OnJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        _joined.Add(ev.PlayerSession);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        var joinDue = _joined.Count > 0 && _accumulator >= JoinDelay;
        if (_accumulator < RebuildInterval && !joinDue)
            return;

        _accumulator = 0f;
        Rebuild(out var roundId, out var players, out var ready, out var map, out var mode);

        var changed = roundId != _sentRound
                      || players != _sentPlayers
                      || ready != _sentReady
                      || map != _sentMap
                      || mode != _sentMode
                      || !SameCandidates();

        foreach (var session in _lobby)
        {
            if (!changed && !_joined.Contains(session))
                continue;

            RaiseNetworkEvent(BuildFor(session.UserId, roundId, players, ready, map, mode), session);
        }

        _joined.Clear();

        if (!changed)
            return;

        _sentRound = roundId;
        _sentPlayers = players;
        _sentReady = ready;
        _sentMap = map;
        _sentMode = mode;
        _sent.Clear();
        _sent.AddRange(_candidates);
    }

    private void Rebuild(out int roundId, out int players, out int ready, out string? map, out string? mode)
    {
        _candidates.Clear();
        _lobby.Clear();
        ready = 0;

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame
                || !_ticker.PlayerGameStatuses.TryGetValue(session.UserId, out var status)
                || status == PlayerGameStatus.JoinedGame)
            {
                continue;
            }

            _lobby.Add(session);
            if (status == PlayerGameStatus.ReadyToPlay)
                ready++;

            // One more than a snapshot carries: the recipient's own character is left out of theirs.
            if (_candidates.Count > LobbyCrowdSnapshotEvent.MaxEntries)
                continue;

            if (!_preferences.TryGetCachedPreferences(session.UserId, out var prefs)
                || prefs.SelectedCharacter is not HumanoidCharacterProfile profile
                || !_prototypes.HasIndex<SpeciesPrototype>(profile.Species))
            {
                continue;
            }

            var job = LobbyTopJob.TryGet(profile, _prototypes, out var top)
                ? new ProtoId<Content.Shared.Roles.JobPrototype>(top.ID)
                : SharedGameTicker.FallbackOverflowJob;

            _candidates.Add(new Candidate(session.UserId, profile.Species, profile.Sex, job, profile.Appearance));
        }

        roundId = _ticker.RoundId;
        players = _players.PlayerCount;
        map = StationName();

        // The same mode title the stock lobby info shows, decoy included.
        var preset = _ticker.Decoy ?? _ticker.CurrentPreset ?? _ticker.Preset;
        mode = preset == null ? null : Loc.GetString(preset.ModeTitle);
    }

    private string? StationName()
    {
        var query = EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out _, out var meta))
        {
            return meta.EntityName;
        }

        return _maps.GetSelectedMap()?.MapName;
    }

    private bool SameCandidates()
    {
        if (_candidates.Count != _sent.Count)
            return false;

        for (var i = 0; i < _candidates.Count; i++)
        {
            // A saved profile is replaced as a whole, so a new appearance object means a new look.
            var a = _candidates[i];
            var b = _sent[i];
            if (a.User != b.User
                || a.Species != b.Species
                || a.Sex != b.Sex
                || a.Job != b.Job
                || !ReferenceEquals(a.Appearance, b.Appearance))
            {
                return false;
            }
        }

        return true;
    }

    private LobbyCrowdSnapshotEvent BuildFor(NetUserId recipient, int roundId, int players, int ready, string? map, string? mode)
    {
        var ev = new LobbyCrowdSnapshotEvent
        {
            RoundId = roundId,
            PlayerCount = players,
            ReadyCount = ready,
            Map = map,
            Mode = mode,
        };

        foreach (var candidate in _candidates)
        {
            if (candidate.User == recipient)
                continue;

            if (ev.Entries.Count >= LobbyCrowdSnapshotEvent.MaxEntries)
                break;

            var appearance = candidate.Appearance;
            if (appearance.Markings.Count > LobbyCrowdSnapshotEvent.MaxMarkings)
                appearance = appearance.WithMarkings(appearance.Markings.GetRange(0, LobbyCrowdSnapshotEvent.MaxMarkings));

            ev.Entries.Add(new LobbyCrowdEntry
            {
                Species = candidate.Species,
                Sex = candidate.Sex,
                Appearance = appearance,
                Job = candidate.Job,
            });
        }

        return ev;
    }

    private readonly record struct Candidate(
        NetUserId User,
        ProtoId<SpeciesPrototype> Species,
        Content.Shared.Humanoid.Sex Sex,
        ProtoId<Content.Shared.Roles.JobPrototype> Job,
        Content.Shared.Humanoid.HumanoidCharacterAppearance Appearance);
}

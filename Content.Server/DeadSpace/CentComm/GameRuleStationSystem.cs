// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.CentComm;

/// <summary>Keeps station-event effects and announcements on the same station map.</summary>
public sealed class GameRuleStationSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public EntityUid? GetTargetStation(EntityUid rule)
    {
        return CompOrNull<GameRuleTargetStationComponent>(rule)?.Station;
    }

    public void EnsureEventTarget(EntityUid rule)
    {
        if (!HasComp<StationEventComponent>(rule) || HasComp<GameRuleTargetStationComponent>(rule))
            return;

        var stations = new List<EntityUid>();
        var query = EntityQueryEnumerator<StationEventEligibleComponent, StationDataComponent>();
        while (query.MoveNext(out var station, out _, out _))
        {
            if (GetStationMap(station) != null)
                stations.Add(station);
        }
        if (stations.Count > 0)
            EnsureComp<GameRuleTargetStationComponent>(rule).Station = _random.Pick(stations);
    }

    public bool IsCentCommRule(EntityUid rule)
    {
        return HasComp<CentCommStationComponent>(GetTargetStation(rule));
    }

    public bool IsTarget(EntityUid rule, EntityUid entity)
    {
        if (!TryComp(entity, out TransformComponent? transform))
            return false;

        if (GetTargetStation(rule) is { } target)
            return GetStationMap(target) is { } map && transform.MapID == map;

        var query = EntityQueryEnumerator<CentCommStationComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (GetStationMap(station) == transform.MapID)
                return false;
        }

        return true;
    }

    private MapId? GetStationMap(EntityUid station)
    {
        return TryComp<StationDataComponent>(station, out var data) &&
               _station.GetLargestGrid((station, data)) is { } grid ? Transform(grid).MapID : null;
    }

    public Filter GetEventPlayers(EntityUid source)
    {
        var filter = Filter.Empty();
        if (GetTargetStation(source) is { } target)
        {
            if (GetStationMap(target) is { } map)
                filter.AddInMap(map);
            return filter;
        }

        if (HasComp<StationDataComponent>(source))
        {
            if (GetStationMap(source) is { } map)
                filter.AddInMap(map);
            return filter;
        }

        if (!HasComp<GameRuleComponent>(source))
        {
            if (TryComp(source, out TransformComponent? transform) && transform.MapID != MapId.Nullspace)
                filter.AddInMap(transform.MapID);
            return filter;
        }

        return GetStationPlayers();
    }

    public Filter GetStationPlayers()
    {
        // Round-wide modes can affect multiple playable stations, but never auxiliary maps or CentComm.
        var filter = Filter.Empty();
        var query = EntityQueryEnumerator<StationEventEligibleComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (GetStationMap(station) is { } map)
                filter.AddInMap(map);
        }

        return filter;
    }

    public void Announce(
        EntityUid source,
        string message,
        string? sender = null,
        bool playSound = true,
        SoundSpecifier? announcementSound = null,
        Color? colorOverride = null,
        string originalMessage = "",
        string? voice = null,
        bool usePresetTTS = false)
    {
        _chat.DispatchAdminFilteredAnnouncement(GetEventPlayers(source), message,
            sender ?? Loc.GetString("station-event-announcer"), playSound, announcementSound,
            colorOverride, originalMessage, voice, usePresetTTS);
    }
}

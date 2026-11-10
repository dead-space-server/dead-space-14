using System.Linq;
using Content.Server.Audio; // DS14
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AlertLevel;

public sealed class AlertLevelSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!; // DS14

    // Until stations are a prototype, this is how it's going to have to be.
    public const string DefaultAlertLevelSet = "stationAlerts";

    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
    }

    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out var station, out var alert))
        {
            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }

            alert.CurrentDelay -= time;
        }
    }

    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComponent))
            return;

        if (!_prototypeManager.TryIndex(alertLevelComponent.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }

        alertLevelComponent.AlertLevels = alerts;

        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.First();
        }

        SetLevel(args.Station, defaultLevel, false, false, true);//DS14

        //DS14-start
        var defaultSecondLevel = alertLevelComponent.AlertLevels.DefaultSecondLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultSecondLevel = "none";
        }

        SetLevel(args.Station, defaultSecondLevel, false, false, true); 
        //DS14-end
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }

        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AlertLevels = alerts;

            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                {
                    defaultLevel = comp.AlertLevels.Levels.Keys.First();
                }

                SetLevel(uid, defaultLevel, true, true, true);

                //DS14-start
                var defaultSecondLevel = comp.AlertLevels.DefaultSecondLevel;
                if (string.IsNullOrEmpty(defaultSecondLevel))
                {
                    defaultSecondLevel = "none";
                }

                SetLevel(uid, defaultSecondLevel, true, true, true);
                //DS14-end
            }
        }

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return string.Empty;
        }

        return alert.CurrentLevel;
    }

    //DS14-start
    public string GetSecondLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return string.Empty;
        }

        return alert.CurrentSecondLevel;
    }
    //DS14-end

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return float.NaN;
        }

        return alert.CurrentDelay;
    }

    /// <summary>
    /// Get the default alert level for a station entity.
    /// Returns an empty string if the station has no alert levels defined.
    /// </summary>
    /// <param name="station">The station entity.</param>
    public string GetDefaultLevel(Entity<AlertLevelComponent?> station)
    {
        if (!Resolve(station.Owner, ref station.Comp) || station.Comp.AlertLevels == null)
        {
            return string.Empty;
        }
        return station.Comp.AlertLevels.DefaultLevel;
    }

    /// <summary>
    /// Set the alert level based on the station's entity ID.
    /// </summary>
    /// <param name="station">Station entity UID.</param>
    /// <param name="level">Level to change the station's alert level to.</param>
    /// <param name="playSound">Play the alert level's sound.</param>
    /// <param name="announce">Say the alert level's announcement.</param>
    /// <param name="force">Force the alert change. This applies if the alert level is not selectable or not.</param>
    /// <param name="locked">Will it be possible to change level by crew.</param>
    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail)
            || component.CurrentLevel == level)
        {
            return;
        }

        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || component.IsLevelLocked)
            {
                return;
            }

            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }

        //DS14-start
        if (detail.IsSecondary)
            component.CurrentSecondLevel = level;
        else
            component.CurrentLevel = level;
        //DS14-end

        component.IsLevelLocked = locked;

        var stationName = dataComponent.EntityName;

        var name = level.ToLower();

        if (Loc.TryGetString($"alert-level-{level}", out var locName))
        {
            name = locName.ToLower();
        }

        // Announcement text. Is passed into announcementFull.
        var announcement = detail.Announcement;

        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement))
        {
            announcement = locAnnouncement;
        }

        // The full announcement to be spat out into chat.
        var announcementFull = string.Empty;

        //DS14-start
        if (detail.IsSecondary)
        {
            if (level == "none")
                announcementFull = Loc.GetString("alert-level-none-announcement");
            else
                announcementFull = Loc.GetString("alert-secondlevel-announcement", ("name", name), ("announcement", announcement));
        }
        else
        {
            announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));
        }
        //DS14-end

        var stationMapFilter = GetStationMapFilter(station); // DS14

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                _sound.PlayAlertLevelGlobal(stationMapFilter, detail.Sound, detail.Sound.Params); // DS14
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {
            //DS14-start
            string? secondAnnouncement = null;
            if (detail.IsSecondary)
            {
                if (component.AlertLevels.Levels.TryGetValue(component.CurrentLevel, out var mainLeveldetail))
                    secondAnnouncement = Loc.GetString("alert-level-main", ("color", mainLeveldetail.Color.ToHex()), ("level", Loc.GetString($"alert-level-{component.CurrentLevel}")));
            }
            else if (component.CurrentSecondLevel != "none")
            {
                if (component.AlertLevels.Levels.TryGetValue(component.CurrentSecondLevel, out var secondLeveldetail))
                    secondAnnouncement = Loc.GetString("alert-level-second", ("color", secondLeveldetail.Color.ToHex()), ("level", Loc.GetString($"alert-level-{component.CurrentSecondLevel}")));
            }
            //DS14-end

            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName, secondMessage: secondAnnouncement, recipientFilter: stationMapFilter); // DS14
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(station, component.CurrentLevel, component.CurrentSecondLevel)); //DS14
    }

    // DS14-start
    private Filter GetStationMapFilter(EntityUid station)
    {
        if (!TryComp<StationDataComponent>(station, out var stationData))
            return Filter.Empty();

        var filter = Filter.Empty();

        foreach (var mapId in stationData.Grids.Select(grid => Transform(grid).MapID).Distinct())
        {
            filter.AddInMap(mapId);
        }

        return filter;
    }
    // DS14-end
}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{}

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{}

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }

    public string AlertSecondLevel { get; } //DS14

    public AlertLevelChangedEvent(EntityUid station, string alertLevel, string alertSecondLevel) //DS14
    {
        Station = station;
        AlertLevel = alertLevel;
        AlertSecondLevel = alertSecondLevel; //DS14
    }
}

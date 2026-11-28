// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.Temperature.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly TimeSpan StormWarning = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan StormBite = TimeSpan.FromSeconds(2);

    private void TickWeather(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        if (!_proto.TryIndex(director.World, out var world) || world.Storm is not { } storm)
            return;

        var now = _timing.CurTime;
        var mapId = Transform(ent.Owner).MapID;

        if (!director.Storm)
        {
            if (!director.StormWarned && now >= director.StormAt - StormWarning)
            {
                director.StormWarned = true;
                Warn(ent.Owner, Loc.GetString(director.Storms == 0 ? "survey-storm-warning-first" : "survey-storm-warning"));
                Reveal(director, "kind");
            }

            if (now < director.StormAt)
                return;

            var growth = world.StormGrowth * ((director.Storms + director.Heat) * director.Threat);
            director.Storm = true;
            director.StormLoss = false;
            director.StormEnds = now + world.StormLength + growth;
            director.NextStormBite = now + StormBite;
            director.Storms++;
            _weather.SetWeather(mapId, _proto.Index(storm), director.StormEnds);
            return;
        }

        if (now >= director.StormEnds)
        {
            director.Storm = false;
            director.StormWarned = false;
            director.StormAt = now + RandomGap(world);
            _weather.SetWeather(mapId, world.Calm is { } calm ? _proto.Index(calm) : null, null);
            Warn(ent.Owner, Loc.GetString("survey-storm-over"));
            if (!director.StormLoss && director.Kind == SurveyKind.DeadElement && _players.Count > 0)
                Progress(ent.Owner, "kind");
            return;
        }

        if (now < director.NextStormBite)
            return;

        director.NextStormBite = now + StormBite;
        Bite(ent.Owner, world);
    }

    private void Bite(EntityUid map, SurveyWorldPrototype world)
    {
        if (!TryComp<MapGridComponent>(map, out var grid))
            return;

        var damage = world.StormDamage is { } spec ? new DamageSpecifier(spec) : null;
        foreach (var (uid, _) in _players)
        {
            var xform = Transform(uid);
            if (xform.GridUid != map ||
                !_map.TryGetTileRef(map, grid, xform.Coordinates, out var tile) ||
                tile.Tile.IsEmpty ||
                !_weather.CanWeatherAffect(map, grid, tile))
            {
                continue;
            }

            if (damage != null)
                _damage.TryChangeDamage(uid, damage, interruptsDoAfters: false, origin: map);

            if (world.StormCooling > 0f &&
                TryComp<TemperatureComponent>(uid, out var temperature) &&
                temperature.CurrentTemperature > world.StormMinTemperature)
            {
                _temperature.ChangeHeat(uid, -world.StormCooling * _temperature.GetHeatCapacity(uid, temperature), false, temperature);
            }
        }
    }

    private void Warn(EntityUid map, string text)
    {
        _chat.DispatchFilteredAnnouncement(
            Filter.Empty().AddInMap(Transform(map).MapID),
            text,
            sender: Loc.GetString("survey-sender-sky"),
            playSound: false,
            colorOverride: Color.LightSteelBlue);
    }
}

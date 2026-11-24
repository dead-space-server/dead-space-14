// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private static readonly ProtoId<RadioChannelPrototype> NativeChannel = "SurveyNative";
    private const string RadioKey = "EncryptionKeySurveyNative";
    private const float LandmarkRange = 14f;
    private const float RadioKeyDrop = 0.2f;
    private const int RadioVariants = 16;
    private const float CritLineChance = 0.01f;
    private const float JokeChance = 0.4f;
    private static readonly TimeSpan JokePause = TimeSpan.FromSeconds(4);

    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private ILocalizationManager _loc = default!;

    private void Radio(Entity<SurveyDirectorComponent> ent, EntityUid? speaker, string topic, params (string, object)[] args)
    {
        var director = ent.Comp;
        if (director.Kind is not (SurveyKind.Raiders or SurveyKind.Pirates or SurveyKind.Factory))
            return;

        if (director.CommsCut && director.Kind != SurveyKind.Factory)
            return;

        var voice = Voice(director);
        if (RadioLine($"survey-radio-{voice}-{topic}", args) is not { } line)
            return;

        var source = speaker is { } chosen && CanSpeak(chosen) ? chosen : AnySpeaker(director);
        if (source is { } uid)
            _radio.SendRadioMessage(uid, line, NativeChannel, uid);
    }

    private bool CanSpeak(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) && !_mobState.IsDead(uid);
    }

    private static string Voice(SurveyDirectorComponent director)
    {
        return director.Kind switch
        {
            SurveyKind.Pirates => "pirate",
            SurveyKind.Factory => "factory",
            _ => "raider",
        };
    }

    private void TellJoke(Entity<SurveyDirectorComponent> ent, EntityUid teller)
    {
        var director = ent.Comp;
        var voice = Voice(director);
        var jokes = 0;
        while (_loc.HasString($"survey-joke-{voice}-{jokes + 1}-1"))
        {
            jokes++;
        }

        if (jokes == 0)
            return;

        var joke = _random.Next(1, jokes + 1);
        var at = _timing.CurTime;
        for (var part = 1; _loc.HasString($"survey-joke-{voice}-{joke}-{part}"); part++)
        {
            director.RadioQueue.Enqueue((teller, Loc.GetString($"survey-joke-{voice}-{joke}-{part}"), at));
            at += JokePause;
        }

        var listener = director.Squads.Where(s => s.Alive > 0 && !s.Members.Contains(teller)).Select(s => Speaker(s)).FirstOrDefault(s => s != null);
        if (listener is { } other && RadioLine($"survey-radio-{voice}-laugh", Array.Empty<(string, object)>()) is { } laugh)
            director.RadioQueue.Enqueue((other, laugh, at));
    }

    private void FlushRadio(SurveyDirectorComponent director)
    {
        var now = _timing.CurTime;
        while (director.RadioQueue.TryPeek(out var next) && next.At <= now)
        {
            director.RadioQueue.Dequeue();
            if (director.CommsCut && director.Kind != SurveyKind.Factory || !CanSpeak(next.Speaker))
                continue;

            _radio.SendRadioMessage(next.Speaker, next.Text, NativeChannel, next.Speaker);
        }
    }

    private string? RadioLine(string id, (string, object)[] args)
    {
        var count = 0;
        while (count < RadioVariants && _loc.HasString($"{id}-{count + 1}"))
        {
            count++;
        }

        return count == 0 ? null : Loc.GetString($"{id}-{_random.Next(1, count + 1)}", args);
    }

    private EntityUid? AnySpeaker(SurveyDirectorComponent director)
    {
        foreach (var squad in director.Squads.OrderBy(_ => _random.Next()))
        {
            foreach (var member in squad.Members)
            {
                if (Alive(member))
                    return member;
            }
        }

        return null;
    }

    private EntityUid? Speaker(SurveySquad squad, Vector2? near = null)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var member in squad.Members)
        {
            if (!Alive(member))
                continue;

            var d = near is { } at ? Vector2.DistanceSquared(Pos(member), at) : _random.NextFloat();
            if (d >= bestDist)
                continue;

            bestDist = d;
            best = member;
        }

        return best;
    }

    private string Where(SurveyDirectorComponent director, Vector2 pos)
    {
        string? key = null;
        var best = LandmarkRange;

        void Consider(Vector2 at, string id)
        {
            var d = Vector2.Distance(at, pos);
            if (d >= best)
                return;

            best = d;
            key = id;
        }

        Consider(director.Landing, "survey-place-landing");
        Consider(director.Far, "survey-place-far");
        foreach (var outpost in director.Outposts)
        {
            Consider(outpost.Pos, outpost.Role switch
            {
                SurveyDeviceRole.Power => "survey-place-power",
                SurveyDeviceRole.Comms => "survey-place-comms",
                _ => "survey-place-outpost",
            });
        }

        foreach (var poi in director.Pois)
        {
            Consider(poi.Pos, poi.Kind switch
            {
                SurveyPoiKind.Stamp => "survey-place-stamp",
                SurveyPoiKind.Device => "survey-place-device",
                SurveyPoiKind.Site => "survey-place-site",
                SurveyPoiKind.Den => "survey-place-den",
                _ => "survey-place-settlement",
            });
        }

        if (key != null)
            return Loc.GetString(key);

        if (Vector2.Distance(pos, director.BaseCentre) <= director.BaseRadius)
            return Loc.GetString("survey-place-base", ("dir", Compass(pos - director.BaseCentre, "sector")));

        return Loc.GetString("survey-place-sector", ("dir", Compass(pos - director.BaseCentre, "sector")));
    }

    private string Compass(Vector2 dir, string form)
    {
        if (dir.LengthSquared() < 0.01f)
            return Loc.GetString($"survey-{form}-centre");

        var side = MathF.Abs(dir.X) > MathF.Abs(dir.Y)
            ? dir.X > 0 ? "east" : "west"
            : dir.Y > 0 ? "north" : "south";

        return Loc.GetString($"survey-{form}-{side}");
    }

    private string Heading(SurveyDirectorComponent director, EntityUid player, Vector2 pos)
    {
        var moved = director.Trail.TryGetValue(player, out var before) ? pos - before : Vector2.Zero;
        return moved.Length() < 3f ? Loc.GetString("survey-heading-still") : Compass(moved, "heading");
    }

    private void TickRadio(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;
        if (director.Kind is not (SurveyKind.Raiders or SurveyKind.Pirates or SurveyKind.Factory))
            return;

        FlushRadio(director);
        foreach (var squad in director.Squads)
        {
            foreach (var member in squad.Members)
            {
                if (_mobState.IsCritical(member) && _random.Prob(CritLineChance))
                    Radio(ent, member, "crit", ("where", Where(director, Pos(member))));
            }
        }

        if (_players.Count == 0)
            return;

        var hot = director.Alarm || director.Squads.Any(s => s.Engaged && s.Alive > 0);
        if (hot && now >= director.NextSitrep)
        {
            director.NextSitrep = now + TimeSpan.FromSeconds(_random.NextFloat(28f, 45f));
            var (player, pos) = _players.OrderBy(p => Vector2.DistanceSquared(p.Pos, director.BaseCentre)).First();
            Radio(ent, null, "sitrep",
                ("where", Where(director, pos)),
                ("count", PlayersNear(pos, 10f)),
                ("heading", Heading(director, player, pos)));

            foreach (var (uid, at) in _players)
            {
                director.Trail[uid] = at;
            }
        }
        else if (!hot && now >= director.NextRadioChatter)
        {
            director.NextRadioChatter = now + TimeSpan.FromSeconds(_random.NextFloat(70f, 140f));
            var squad = director.Squads.Where(s => s.Alive > 0).OrderBy(_ => _random.Next()).FirstOrDefault();
            if (squad != null && Speaker(squad) is { } teller)
            {
                if (_random.Prob(JokeChance))
                    TellJoke(ent, teller);
                else
                    Radio(ent, teller, "idle", ("where", Where(director, squad.Home.Position)));
            }

            foreach (var (uid, at) in _players)
            {
                director.Trail[uid] = at;
            }
        }
    }

    private void DropRadioKey(EntityUid body)
    {
        if (_random.Prob(RadioKeyDrop))
            SpawnAtPosition(RadioKey, Transform(body).Coordinates);
    }
}

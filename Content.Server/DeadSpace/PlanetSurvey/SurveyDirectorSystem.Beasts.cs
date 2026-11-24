// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const float LungeRange = 4f;
    private const float BroodRange = 8f;
    private const float CallReach = 30f;
    private const float SpotRange = 9f;

    private void SpawnBeasts(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, System.Random random, List<SurveySpot> spots)
    {
        var open = spots.Where(s => s.Open).ToList();
        if (open.Count > 0)
            spots = open;

        if (spots.Count == 0)
            return;

        for (var p = 0; p < world.PackCount && world.Predators.Count > 0; p++)
        {
            var den = NextSpot(spots, random);
            _guarded.Add(den);
            var squad = new SurveySquad { Home = At(ent.Owner, den.Center) };
            var proto = world.Predators[random.Next(world.Predators.Count)];
            for (var i = 0; i < world.PackSize; i++)
            {
                squad.Members.Add(SpawnMember(ent, proto, den, ent.Comp.Squads.Count, random));
            }

            squad.Size = squad.Members.Count;
            squad.Alive = squad.Size;
            ent.Comp.Squads.Add(squad);
        }

        if (world.Prey.Count == 0)
            return;

        for (var h = 0; h < world.Herds; h++)
        {
            var pasture = NextSpot(spots, random);
            var herd = new SurveySquad { Home = At(ent.Owner, pasture.Center) };
            for (var i = 0; i < world.HerdSize; i++)
            {
                var proto = world.Prey[random.Next(world.Prey.Count)];
                var uid = SpawnMember(ent, proto, pasture, ent.Comp.Squads.Count, random);
                EnsureComp<SurveyMemberComponent>(uid).Prey = true;
                herd.Members.Add(uid);
            }

            herd.Size = herd.Members.Count;
            herd.Alive = herd.Size;
            ent.Comp.Squads.Add(herd);
        }
    }

    private void TickBeasts(Entity<SurveyDirectorComponent> ent)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;
        _proto.TryIndex(director.World, out var world);

        foreach (var squad in director.Squads)
        {
            var den = squad.Home.Position;
            var broodThreat = NearestSeen(director, den, BroodRange, true) != null;

            foreach (var beast in squad.Members)
            {
                if (!Alive(beast) || !TryComp<SurveyMemberComponent>(beast, out var member) || Board(beast) is not { } board)
                    continue;

                var pos = Pos(beast);
                var hp = Health(beast);
                var hit = hp < member.LastHealth - 0.01f;
                member.LastHealth = hp;

                var close = NearestSeen(director, pos, member.Prey ? 6f : LungeRange, true);
                var flee = member.Prey ? close != null || hit : hp < 0.4f;
                var threat = !member.Prey && !flee && (close != null || broodThreat || hit);

                var wasFlee = board.GetValueOrDefault<bool>(SurveyKeys.Flee, EntityManager);
                var wasThreat = board.GetValueOrDefault<bool>(SurveyKeys.Threat, EntityManager);

                if (flee)
                {
                    var danger = close is { } c ? Pos(c) : den;
                    var away = pos - danger;
                    if (away.LengthSquared() < 0.01f)
                        away = Vector2.UnitY;
                    board.SetValue(SurveyKeys.Cover, At(ent.Owner, pos + Vector2.Normalize(away) * 10f));
                }

                board.SetValue(SurveyKeys.Flee, flee);
                board.SetValue(SurveyKeys.Threat, threat);
                if (threat)
                    director.LastContact = now;

                if (board.GetValueOrDefault<bool>(SurveyKeys.Called, EntityManager) &&
                    (threat || flee || board.TryGetValue<EntityCoordinates>(SurveyKeys.Call, out var call, EntityManager) &&
                        Vector2.Distance(pos, call.Position) < 4f))
                {
                    board.SetValue(SurveyKeys.Called, false);
                    Replan(beast);
                }

                if (!member.Prey && now >= member.NextCall &&
                    (hit || NearestSeen(director, pos, SpotRange, true) != null))
                {
                    member.NextCall = now + TimeSpan.FromSeconds(hit ? 8 : 20);
                    Call(ent, squad, beast, pos, world?.CallSound);
                }

                if (flee != wasFlee || threat != wasThreat)
                    Replan(beast);
            }
        }
    }

    private void Call(Entity<SurveyDirectorComponent> ent, SurveySquad squad, EntityUid caller, Vector2 at, SoundSpecifier? sound)
    {
        if (sound != null)
            _audio.PlayPvs(sound, caller, AudioParams.Default.WithMaxDistance(CallReach));

        var point = At(ent.Owner, at);
        foreach (var mate in squad.Members)
        {
            if (mate == caller || !Alive(mate) || Board(mate) is not { } board)
                continue;

            if (board.GetValueOrDefault<bool>(SurveyKeys.Flee, EntityManager) ||
                Vector2.Distance(Pos(mate), at) > CallReach)
            {
                continue;
            }

            board.SetValue(SurveyKeys.Call, point);
            board.SetValue(SurveyKeys.Called, true);
            Replan(mate);
        }
    }
}

using Content.Server.DeadSpace.Lavaland.Components; // DS14
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Handler for events that spawn random reagent foam around a vent.
/// </summary>
/// <remarks>
/// Do NOT copy paste this class to make a new mob event, create a new game rule entity using <see cref="VentCrittersRuleComponent"/>.
/// </remarks>
public sealed partial class VentCrittersRule : StationEventSystem<VentCrittersRuleComponent>
{
    protected override void Started(Entity<VentCrittersRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

<<<<<<< ours
        if (!TryGetRandomStation(out var station, rule: uid)) // DS14
        {
||||||| base
        if (!TryGetRandomStation(out var station))
        {
=======
        var critterRule = ent.Comp1;
        var validLocations = Station.GetEntitiesWithComponentOnStation<VentCritterSpawnLocationComponent>(true);
        if (validLocations.Count == 0)
>>>>>>> theirs
            return;
<<<<<<< ours
        }

        var specialLocations = new List<EntityCoordinates>(); // DS14
        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
            {
                // DS14-start
                if (transform.MapUid is not { } mapUid || !HasComp<LavalandMapComponent>(mapUid))
                    specialLocations.Add(transform.Coordinates);
                // DS14-end
                foreach (var spawn in EntitySpawnCollection.GetSpawns(component.Entries, RobustRandom))
                {
                    Spawn(spawn, transform.Coordinates);
                }
            }
        }
||||||| base
        }

        var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<EntityCoordinates>();
        while (locations.MoveNext(out _, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == station)
            {
                validLocations.Add(transform.Coordinates);
                foreach (var spawn in EntitySpawnCollection.GetSpawns(component.Entries, RobustRandom))
                {
                    Spawn(spawn, transform.Coordinates);
                }
            }
        }
=======
>>>>>>> theirs

<<<<<<< ours
        if (component.SpecialEntries.Count == 0 || specialLocations.Count == 0) // DS14
||||||| base
        if (component.SpecialEntries.Count == 0 || validLocations.Count == 0)
=======
        // guaranteed spawn
        if (critterRule.SpecialEntries.Count > 0)
>>>>>>> theirs
        {
            var specialEntry = RobustRandom.Pick(critterRule.SpecialEntries);
            var specialSpawn = Transform(RobustRandom.Pick(validLocations)).Coordinates;
            Spawn(specialEntry.PrototypeId, specialSpawn);
        }

<<<<<<< ours
        // guaranteed spawn
        var specialEntry = RobustRandom.Pick(component.SpecialEntries);
        var specialSpawn = RobustRandom.Pick(specialLocations); // DS14
        Spawn(specialEntry.PrototypeId, specialSpawn);

        foreach (var location in specialLocations) // DS14
||||||| base
        // guaranteed spawn
        var specialEntry = RobustRandom.Pick(component.SpecialEntries);
        var specialSpawn = RobustRandom.Pick(validLocations);
        Spawn(specialEntry.PrototypeId, specialSpawn);

        foreach (var location in validLocations)
=======
        foreach (var location in validLocations)
>>>>>>> theirs
        {
            var spawns = EntitySpawnCollection.GetSpawns(critterRule.Entries, RobustRandom);
            foreach (var spawn in spawns)
            {
                Spawn(spawn, Transform(location).Coordinates);
            }

            var specialSpawns = EntitySpawnCollection.GetSpawns(critterRule.SpecialEntries, RobustRandom);
            foreach (var spawn in specialSpawns)
            {
                Spawn(spawn, Transform(location).Coordinates);
            }
        }
    }
}

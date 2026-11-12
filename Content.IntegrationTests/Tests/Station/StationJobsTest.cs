using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Station;

[TestFixture]
[TestOf(typeof(ServerStationJobsSystem))]
public sealed class StationJobsTest : GameTest
{
    private const string StationMapId = "FooStation";
    private const string SecondStationMapId = "BarStation";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: playTimeTracker
  id: PlayTimeDummyAssistant

- type: playTimeTracker
  id: PlayTimeDummyMime

- type: playTimeTracker
  id: PlayTimeDummyClown

- type: playTimeTracker
  id: PlayTimeDummyCaptain

- type: playTimeTracker
  id: PlayTimeDummyChaplain

- type: department
  id: StationJobsTestDepartment
  name: department-Cargo
  description: department-Cargo-description
  color: ""#FFFFFF""
  roles:
  - TCaptain
  - TChaplain

- type: jobWeight
  id: StationJobsTest
  weights:
    TAssistant: 0
    TMime: 20
    TClown: -10
    TCaptain: 10
    TChaplain: 0

- type: gameMap
  id: {StationMapId}
  minPlayers: 0
  mapName: {StationMapId}
  mapPath: /Maps/Test/empty.yml
  jobWeights: StationJobsTest
  stations:
    Station:
      mapNameTemplate: {StationMapId}
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            TMime: [0, -1]
            TAssistant: [-1, -1]
            TCaptain: [5, 5]
            TClown: [5, 6]

- type: jobWeight
  id: StationJobsBarTest
  weights:
    TCaptain: 30
    TChaplain: 20
    TMime: 100
    TAssistant: 0
    TClown: 0

- type: gameMap
  id: {SecondStationMapId}
  minPlayers: 0
  mapName: {SecondStationMapId}
  mapPath: /Maps/Test/empty.yml
  jobWeights: StationJobsBarTest
  stations:
    First:
      mapNameTemplate: First
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            TCaptain: [1, 1]
            TChaplain: [1, 1]
            TAssistant: [0, 1]
            TClown: [-1, -1]
    Second:
      mapNameTemplate: Second
      stationProto: StandardNanotrasenStation
      components:
        - type: StationJobs
          availableJobs:
            TMime: [1, 1]

- type: job
  id: TAssistant
  playTimeTracker: PlayTimeDummyAssistant

- type: job
  id: TMime
  playTimeTracker: PlayTimeDummyMime

- type: job
  id: TClown
  playTimeTracker: PlayTimeDummyClown

- type: job
  id: TCaptain
  playTimeTracker: PlayTimeDummyCaptain

- type: job
  id: TChaplain
  playTimeTracker: PlayTimeDummyChaplain
";

    [Test]
    public async Task AssignJobsTest()
    {
<<<<<<< ours
        await using var pair = await PoolManager.GetServerClient(); // DS14 - pooled integration harness
||||||| base
        await using var pair = await PoolManager.GetServerClient();
=======
        var pair = Pair;
>>>>>>> theirs
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var barStationProto = prototypeManager.Index<GameMapPrototype>(SecondStationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<ServerStationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<ServerStationSystem>();

        var firstStation = EntityUid.Invalid;
        var secondStation = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            firstStation = stationSystem.InitializeNewStation(
                barStationProto.Stations["First"], null, "First", barStationProto);
            secondStation = stationSystem.InitializeNewStation(
                barStationProto.Stations["Second"], null, "Second", barStationProto);
        });

<<<<<<< ours
        // DS14-start: AssignJobs only needs stable NetUserId keys. Creating live dummy sessions here caused their
        // disconnect callbacks to race pooled-pair cleanup and intermittently leave the pair in CleanDisposed.
        var players = new[]
        {
            new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000002")),
            new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000003")),
            new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000004")),
            new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000005")),
        };
        // DS14-end
||||||| base
=======
        var dummies = await server.AddDummySessions(5);
>>>>>>> theirs
        await server.WaitAssertion(() =>
        {
<<<<<<< ours
            var fakePlayers = new Dictionary<NetUserId, HumanoidCharacterProfile>
            {
                // The first station's captain minimum wins despite a lower player preference and the second station's
                // mime having the highest weight. This verifies both role weighting and station-by-station allocation.
                [players[0]] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TCaptain", JobPriority.Low)
                    .WithJobPriority("TChaplain", JobPriority.High)
                    .WithJobPriority("TMime", JobPriority.Medium),
                [players[1]] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TChaplain", JobPriority.High),
                // The second station's minimum must be assigned before the first station's optional assistant slot.
                [players[2]] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TAssistant", JobPriority.High)
                    .WithJobPriority("TClown", JobPriority.Low),
                [players[3]] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TAssistant", JobPriority.High)
                    .WithJobPriority("TMime", JobPriority.Low),
                [players[4]] = HumanoidCharacterProfile.Random()
                    .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>()),
            };

            var stations = new[] { firstStation, secondStation };
||||||| base
            var fakePlayers = new Dictionary<NetUserId, HumanoidCharacterProfile>()
                .AddJob("TAssistant", JobPriority.Medium, PlayerCount)
                .AddPreference("TClown", JobPriority.Low)
                .AddPreference("TMime", JobPriority.High)
                .WithPlayers(
                    new Dictionary<NetUserId, HumanoidCharacterProfile>()
                    .AddJob("TCaptain", JobPriority.High, CaptainCount)
                );
            Assert.That(fakePlayers, Is.Not.Empty);

            var start = new Stopwatch();
            start.Start();
=======
            var fakePlayers = new Dictionary<NetUserId, HumanoidCharacterProfile>
            {
                // The first station's captain minimum wins despite a lower player preference and the second station's
                // mime having the highest weight. This verifies both role weighting and station-by-station allocation.
                [dummies[0].UserId] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TCaptain", JobPriority.Low)
                    .WithJobPriority("TChaplain", JobPriority.High)
                    .WithJobPriority("TMime", JobPriority.Medium),
                [dummies[1].UserId] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TChaplain", JobPriority.High),
                // The second station's minimum must be assigned before the first station's optional assistant slot.
                [dummies[2].UserId] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TAssistant", JobPriority.High)
                    .WithJobPriority("TClown", JobPriority.Low),
                [dummies[3].UserId] = HumanoidCharacterProfile.Random()
                    .WithJobPriority("TAssistant", JobPriority.High)
                    .WithJobPriority("TMime", JobPriority.Low),
                [dummies[4].UserId] = HumanoidCharacterProfile.Random()
                    .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>()),
            };

            var stations = new[] { firstStation, secondStation };
>>>>>>> theirs
            var assigned = stationJobs.AssignJobs(fakePlayers, stations);
            stationJobs.AssignOverflowJobs(ref assigned, fakePlayers.Keys, fakePlayers, stations);

            Assert.Multiple(() =>
            {
<<<<<<< ours
                Assert.That(assigned[players[0]], Is.EqualTo(((ProtoId<JobPrototype>?) "TCaptain", firstStation)));
                Assert.That(assigned[players[1]], Is.EqualTo(((ProtoId<JobPrototype>?) "TChaplain", firstStation)));
                Assert.That(assigned[players[2]], Is.EqualTo(((ProtoId<JobPrototype>?) "TAssistant", firstStation)));
                Assert.That(assigned[players[3]], Is.EqualTo(((ProtoId<JobPrototype>?) "TMime", secondStation)));
                Assert.That(assigned[players[4]], Is.EqualTo(((ProtoId<JobPrototype>?) "TClown", firstStation)));
||||||| base
                foreach (var station in stations)
                {
                    var assignedHere = assigned
                        .Where(x => x.Value.Item2 == station)
                        .ToDictionary(x => x.Key, x => x.Value);

                    // Each station should have SOME players.
                    Assert.That(assignedHere, Is.Not.Empty);
                    // And it should have at least the minimum players to be considered a "fair" share, as they're all the same.
                    Assert.That(assignedHere, Has.Count.GreaterThanOrEqualTo(TotalPlayers / stations.Count), "Station has too few players.");
                    // And it shouldn't have ALL the players, either.
                    Assert.That(assignedHere, Has.Count.LessThan(TotalPlayers), "Station has too many players.");
                    // And there should be *A* captain, as there's one player with captain enabled per station.
                    Assert.That(assignedHere.Where(x => x.Value.Item1 == "TCaptain").ToList(), Has.Count.EqualTo(1));
                }

                // All clown players have assistant as a higher priority.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Not.Contain("TClown"));
                // Mime isn't an open job-slot at round-start.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Not.Contain("TMime"));
                // All players have slots they can fill.
                Assert.That(assigned.Values, Has.Count.EqualTo(TotalPlayers), $"Expected {TotalPlayers} players.");
                // There must be assistants present.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Contain("TAssistant"));
                // There must be captains present, too.
                Assert.That(assigned.Values.Select(x => x.Item1).ToList(), Does.Contain("TCaptain"));
=======
                Assert.That(assigned[dummies[0].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) "TCaptain", firstStation)));
                Assert.That(assigned[dummies[1].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) "TChaplain", firstStation)));
                Assert.That(assigned[dummies[2].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) "TAssistant", firstStation)));
                Assert.That(assigned[dummies[3].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) "TMime", secondStation)));
                Assert.That(assigned[dummies[4].UserId], Is.EqualTo(((ProtoId<JobPrototype>?) "TClown", firstStation)));
>>>>>>> theirs
            });
        });
    }

    [Test]
    public async Task MinimumJobsUseConfiguredFallback()
    {
        var pair = Pair;
        var server = pair.Server;
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var barStationProto = prototypeManager.Index<GameMapPrototype>(SecondStationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<ServerStationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<ServerStationSystem>();
        var station = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(
                barStationProto.Stations["First"], null, "First", barStationProto);
        });

        var dummies = await server.AddDummySessions(2);
        var sameDepartmentDummy = dummies[0];
        var noPreferenceDummy = dummies[1];
        var sameDepartmentProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [sameDepartmentDummy.UserId] = new HumanoidCharacterProfile()
                .WithJobPriority("TChaplain", JobPriority.Low),
        };

        var noPreferenceProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [noPreferenceDummy.UserId] = new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>()),
        };

        var anyEligibleProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [sameDepartmentDummy.UserId] = sameDepartmentProfiles[sameDepartmentDummy.UserId],
            [noPreferenceDummy.UserId] = noPreferenceProfiles[noPreferenceDummy.UserId],
        };

        var originalValue = configuration.GetCVar(CCVars.GameMinimumJobFallback);
        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.SameDepartment);
                var sameDepartmentAssignments = stationJobs.AssignJobs(sameDepartmentProfiles, [station]);
                Assert.That(sameDepartmentAssignments[sameDepartmentDummy.UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TCaptain"));

                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.AnyEligiblePlayer);
                var anyEligibleAssignments = stationJobs.AssignJobs(anyEligibleProfiles, [station]);
                Assert.That(anyEligibleAssignments[sameDepartmentDummy.UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TCaptain"));
                Assert.That(anyEligibleAssignments[noPreferenceDummy.UserId].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TChaplain"));

                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.None);
                var noFallbackAssignments = stationJobs.AssignJobs(noPreferenceProfiles, [station]);
                Assert.That(noFallbackAssignments, Is.Empty);
            });
        }
        finally
        {
            await server.WaitPost(() =>
                configuration.SetCVar(CCVars.GameMinimumJobFallback, originalValue));
        }
    }

    [Test]
    public async Task MinimumJobsUseConfiguredFallback()
    {
        await using var pair = await PoolManager.GetServerClient(); // DS14 - pooled integration harness
        var server = pair.Server;
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var barStationProto = prototypeManager.Index<GameMapPrototype>(SecondStationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<StationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<StationSystem>();
        var station = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(
                barStationProto.Stations["First"], null, "First", barStationProto);
        });

        // DS14: these assignments are likewise independent from live player sessions.
        var sameDepartmentUser = new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000006"));
        var noPreferenceUser = new NetUserId(Guid.Parse("10000000-0000-0000-0000-000000000007"));
        var sameDepartmentProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [sameDepartmentUser] = new HumanoidCharacterProfile()
                .WithJobPriority("TChaplain", JobPriority.Low),
        };

        var noPreferenceProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [noPreferenceUser] = new HumanoidCharacterProfile()
                .WithJobPriorities(Array.Empty<KeyValuePair<ProtoId<JobPrototype>, JobPriority>>()),
        };

        var anyEligibleProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>
        {
            [sameDepartmentUser] = sameDepartmentProfiles[sameDepartmentUser],
            [noPreferenceUser] = noPreferenceProfiles[noPreferenceUser],
        };

        var originalValue = configuration.GetCVar(CCVars.GameMinimumJobFallback);
        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.SameDepartment);
                var sameDepartmentAssignments = stationJobs.AssignJobs(sameDepartmentProfiles, [station]);
                Assert.That(sameDepartmentAssignments[sameDepartmentUser].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TCaptain"));

                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.AnyEligiblePlayer);
                var anyEligibleAssignments = stationJobs.AssignJobs(anyEligibleProfiles, [station]);
                Assert.That(anyEligibleAssignments[sameDepartmentUser].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TCaptain"));
                Assert.That(anyEligibleAssignments[noPreferenceUser].Item1, Is.EqualTo((ProtoId<JobPrototype>?) "TChaplain"));

                configuration.SetCVar(CCVars.GameMinimumJobFallback, MinimumJobFallback.None);
                var noFallbackAssignments = stationJobs.AssignJobs(noPreferenceProfiles, [station]);
                Assert.That(noFallbackAssignments, Is.Empty);
            });
        }
        finally
        {
            await server.WaitPost(() =>
                configuration.SetCVar(CCVars.GameMinimumJobFallback, originalValue));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjustJobsTest()
    {
<<<<<<< ours
        await using var pair = await PoolManager.GetServerClient(); // DS14 - pooled integration harness
||||||| base
        await using var pair = await PoolManager.GetServerClient();
=======
        var pair = Pair;
>>>>>>> theirs
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var fooStationProto = prototypeManager.Index<GameMapPrototype>(StationMapId);
        var entSysMan = server.ResolveDependency<IEntityManager>().EntitySysManager;
        var stationJobs = entSysMan.GetEntitySystem<ServerStationJobsSystem>();
        var stationSystem = entSysMan.GetEntitySystem<ServerStationSystem>();

        var station = EntityUid.Invalid;
        await server.WaitPost(() =>
        {
            station = stationSystem.InitializeNewStation(fooStationProto.Stations["Station"], null, $"Foo Station", fooStationProto);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            // Verify jobs are/are not unlimited.
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.IsJobUnlimited(station, "TAssistant"), "TAssistant is expected to be unlimited.");
                Assert.That(stationJobs.IsJobUnlimited(station, "TMime"), "TMime is expected to be unlimited.");
                Assert.That(!stationJobs.IsJobUnlimited(station, "TCaptain"), "TCaptain is expected to not be unlimited.");
                Assert.That(!stationJobs.IsJobUnlimited(station, "TClown"), "TClown is expected to not be unlimited.");
            });
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.TrySetJobSlot(station, "TClown", 0), "Could not set TClown to have zero slots.");
                Assert.That(stationJobs.TryGetJobSlot(station, "TClown", out var clownSlots), "Could not get the number of TClown slots.");
                Assert.That(clownSlots, Is.EqualTo(0));
                Assert.That(!stationJobs.TryAdjustJobSlot(station, "TCaptain", -9999), "Was able to adjust TCaptain by -9999 without clamping.");
                Assert.That(stationJobs.TryAdjustJobSlot(station, "TCaptain", -9999, false, true), "Could not adjust TCaptain by -9999.");
                Assert.That(stationJobs.TryGetJobSlot(station, "TCaptain", out var captainSlots), "Could not get the number of TCaptain slots.");
                Assert.That(captainSlots, Is.EqualTo(0));
            });
            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.TrySetJobSlot(station, "TChaplain", 10, true), "Could not create 10 TChaplain slots.");
                stationJobs.MakeJobUnlimited(station, "TChaplain");
                Assert.That(stationJobs.IsJobUnlimited(station, "TChaplain"), "Could not make TChaplain unlimited.");
            });
        });
    }

    [Test]
    public async Task InvalidRoundstartJobsTest()
    {
<<<<<<< ours
        await using var pair = await PoolManager.GetServerClient(); // DS14 - pooled integration harness
||||||| base
        await using var pair = await PoolManager.GetServerClient();
=======
        var pair = Pair;
>>>>>>> theirs
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var compFact = server.ResolveDependency<IComponentFactory>();
        var name = compFact.GetComponentName<StationJobsComponent>();

        await server.WaitAssertion(() =>
        {
            // invalidJobs contains all the jobs which can't be set for preference:
            // i.e. all the jobs that shouldn't be available round-start.
            var invalidJobs = new HashSet<string>();
            foreach (var job in prototypeManager.EnumeratePrototypes<JobPrototype>())
            {
                if (!job.SetPreference)
                    invalidJobs.Add(job.ID);
            }

            Assert.Multiple(() =>
            {
                foreach (var gameMap in prototypeManager.EnumeratePrototypes<GameMapPrototype>())
                {
                    foreach (var (stationId, station) in gameMap.Stations)
                    {
                        if (!station.StationComponentOverrides.TryGetComponent(name, out var comp))
                            continue;

                        foreach (var (job, array) in ((StationJobsComponent) comp).SetupAvailableJobs)
                        {
                            Assert.That(array.Length, Is.EqualTo(2));
                            Assert.That(array[0] is -1 or >= 0);
                            Assert.That(array[1] is -1 or >= 0);
                            if (array[0] >= 0 && array[1] >= 0)
                                Assert.That(array[0], Is.LessThanOrEqualTo(array[1]), "Round-start minimum exceeds maximum slots.");
                            Assert.That(invalidJobs, Does.Not.Contain(job), $"Station {stationId} contains job prototype {job} which cannot be present roundstart.");
                        }
                    }
                }
            });
        });
<<<<<<< ours
        await pair.CleanReturnAsync();
    }
}
||||||| base
        await pair.CleanReturnAsync();
    }
}

internal static class JobExtensions
{
    public static Dictionary<NetUserId, HumanoidCharacterProfile> AddJob(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp, string jobId, JobPriority prio = JobPriority.Medium,
        int amount = 1)
    {
        for (var i = 0; i < amount; i++)
        {
            inp.Add(new NetUserId(Guid.NewGuid()), HumanoidCharacterProfile.Random().WithJobPriority(jobId, prio));
        }

        return inp;
    }

    public static Dictionary<NetUserId, HumanoidCharacterProfile> AddPreference(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp, string jobId, JobPriority prio = JobPriority.Medium)
    {
        return inp.ToDictionary(x => x.Key, x => x.Value.WithJobPriority(jobId, prio));
    }

    public static Dictionary<NetUserId, HumanoidCharacterProfile> WithPlayers(
        this Dictionary<NetUserId, HumanoidCharacterProfile> inp,
        Dictionary<NetUserId, HumanoidCharacterProfile> second)
    {
        return new[] { inp, second }.SelectMany(x => x).ToDictionary(x => x.Key, x => x.Value);
    }
}
=======
    }
}
>>>>>>> theirs

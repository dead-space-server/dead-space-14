using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using Content.Server.Database;
using Content.Server.DeadSpace.Arena;
using Content.Server.DeadSpace.Prison.Components;
using Content.Server.DeadSpace.Prison;
using Content.Shared.Cargo.Components;
using Content.Shared.Destructible;
using Content.Shared.Database;
using Content.Server.Stack;
using Content.Shared.ActionBlocker;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Prison;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.Prison;

[TestFixture]
public sealed class PrisonOreProvenanceTest
{
    [Test]
    public async Task FactionBasesAreSeparateAndOnlySlagHasOreProcessors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;

        await server.WaitPost(() => server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            EntityUid? slagGrid = null;
            EntityUid? frontierGrid = null;
            var slagSpawns = 0;
            var frontierSpawns = 0;
            var spawnQuery = server.EntMan.EntityQueryEnumerator<PrisonSpawnPointComponent, TransformComponent>();
            while (spawnQuery.MoveNext(out _, out var spawn, out var xform))
            {
                if (spawn.Faction?.Id == "PrisonSlag")
                {
                    slagGrid ??= xform.GridUid;
                    Assert.That(xform.GridUid, Is.EqualTo(slagGrid));
                    slagSpawns++;
                }
                else if (spawn.Faction?.Id == "PrisonFrontier")
                {
                    frontierGrid ??= xform.GridUid;
                    Assert.That(xform.GridUid, Is.EqualTo(frontierGrid));
                    frontierSpawns++;
                }
            }

            Assert.That(slagGrid, Is.Not.Null);
            Assert.That(frontierGrid, Is.Not.Null);

            var slagProcessors = 0;
            var frontierProcessors = 0;
            var processorQuery = server.EntMan.EntityQueryEnumerator<PrisonOreProcessorComponent, TransformComponent>();
            while (processorQuery.MoveNext(out _, out _, out var xform))
            {
                if (xform.GridUid == slagGrid)
                    slagProcessors++;
                else if (xform.GridUid == frontierGrid)
                    frontierProcessors++;
            }

            var transform = server.System<SharedTransformSystem>();
            var slagPosition = transform.GetWorldPosition(slagGrid.Value);
            var frontierPosition = transform.GetWorldPosition(frontierGrid.Value);

            Assert.Multiple(() =>
            {
                Assert.That(slagGrid, Is.Not.EqualTo(frontierGrid));
                Assert.That(Vector2.Distance(slagPosition, frontierPosition), Is.GreaterThanOrEqualTo(160f));
                Assert.That(slagSpawns, Is.EqualTo(11));
                Assert.That(frontierSpawns, Is.EqualTo(11));
                Assert.That(slagProcessors, Is.EqualTo(2));
                Assert.That(frontierProcessors, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobbyBanRegistersPrisonerWithoutMovingLobbySession()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var player = pair.Player!;
        var prison = server.System<PrisonSystem>();
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();
        var database = server.ResolveDependency<IServerDbManager>();
        var originalEntity = player.AttachedEntity;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisonerSlag", new MapCoordinates(Vector2.Zero, mapId));
            server.EntMan.SpawnEntity("SpawnPointPrisonerFrontier", new MapCoordinates(Vector2.One, mapId));
        });

        Assert.That(ticker.UserHasJoinedGame(player), Is.False, "The test session must still be in the lobby.");
        var now = DateTimeOffset.UtcNow;
        var ban = await AddPrisonBan(database, player.UserId, now, now + TimeSpan.FromHours(1));
        var handled = false;

        await server.WaitPost(() => handled = prison.TrySendToPrison(player, ban));
        await server.WaitPost(() =>
        {
            Assert.That(prison.GetFactionEuiState().Factions, Has.Count.EqualTo(2));
            Assert.That(prison.TrySelectFaction(player, "PrisonSlag"), Is.False);
            Assert.That(prison.TrySelectFaction(player, "PrisonFrontier"), Is.False);
        });

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(player.AttachedEntity, Is.EqualTo(originalEntity));
            Assert.That(prison.IsUserPrisoner(player.UserId), Is.True);
            Assert.That(server.System<ArenaSystem>().CanJoinArena(player), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GhostFactionSelectionCreatesPrisonerBodyOnFactionBase()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var player = pair.Player!;
        var prison = server.System<PrisonSystem>();
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();
        var mind = server.System<Content.Server.Mind.MindSystem>();
        var hands = server.System<SharedHandsSystem>();
        var database = server.ResolveDependency<IServerDbManager>();
        EntityUid ghost = default;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisonerSlag", new MapCoordinates(Vector2.Zero, mapId));
            server.EntMan.SpawnEntity("SpawnPointPrisonerFrontier", new MapCoordinates(Vector2.One, mapId));

            ghost = server.EntMan.SpawnEntity(
                Content.Server.GameTicking.GameTicker.ObserverPrototypeName,
                MapCoordinates.Nullspace);
            var playerMind = mind.CreateMind(player.UserId, player.Name);
            mind.TransferTo(playerMind, ghost);
            ((IDictionary<NetUserId, PlayerGameStatus>) ticker.PlayerGameStatuses)[player.UserId] =
                PlayerGameStatus.JoinedGame;
        });
        await pair.RunTicksSync(2);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.UserHasJoinedGame(player), Is.True);
            Assert.That(player.AttachedEntity, Is.EqualTo(ghost));
            Assert.That(server.EntMan.HasComponent<GhostComponent>(ghost), Is.True);
        });

        var now = DateTimeOffset.UtcNow;
        var ban = await AddPrisonBan(database, player.UserId, now, now + TimeSpan.FromHours(1));
        var handled = false;
        var selected = false;

        await server.WaitPost(() =>
        {
            handled = prison.TrySendToPrison(player, ban);
            selected = prison.TrySelectFaction(player, "PrisonSlag");
        });
        await pair.RunTicksSync(3);

        var prisoner = player.AttachedEntity;
        Assert.That(prisoner, Is.Not.Null);
        var heldPrototypes = hands.EnumerateHeld(prisoner!.Value)
            .Select(entity => server.EntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(selected, Is.True);
            Assert.That(prisoner, Is.Not.EqualTo(ghost));
            Assert.That(server.EntMan.HasComponent<GhostComponent>(prisoner!.Value), Is.False);
            Assert.That(server.EntMan.HasComponent<PrisonBoundComponent>(prisoner.Value), Is.True);
            Assert.That(
                server.EntMan.GetComponent<PrisonFactionMemberComponent>(prisoner.Value).Faction.Id,
                Is.EqualTo("PrisonSlag"));
            Assert.That(prison.IsEntityPrisoner(prisoner.Value), Is.True);
            Assert.That(heldPrototypes, Does.Contain("Pickaxe"));
            Assert.That(heldPrototypes, Does.Contain("OreBag"));
        });

        await server.WaitPost(() =>
        {
            Assert.That(prison.TrySelectFaction(player, "PrisonSlag"), Is.True);
            Assert.That(player.AttachedEntity, Is.EqualTo(prisoner));
            Assert.That(prison.TrySelectFaction(player, "PrisonFrontier"), Is.False);
        });

        EntityUid secondGhost = default;
        await server.WaitPost(() =>
        {
            secondGhost = server.EntMan.SpawnEntity(
                Content.Server.GameTicking.GameTicker.ObserverPrototypeName,
                MapCoordinates.Nullspace);
            mind.TransferTo(mind.GetMind(player.UserId)!.Value, secondGhost);
        });
        await pair.RunTicksSync(3);

        var observingEntity = player.AttachedEntity;
        Assert.Multiple(() =>
        {
            Assert.That(observingEntity, Is.EqualTo(secondGhost));
            Assert.That(server.EntMan.HasComponent<GhostComponent>(secondGhost), Is.True);
            Assert.That(server.EntMan.HasComponent<PrisonBoundComponent>(secondGhost), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LivingBodyIsLockedUntilFrontierSelection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var player = pair.Player!;
        var prison = server.System<PrisonSystem>();
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();
        var mind = server.System<Content.Server.Mind.MindSystem>();
        var blocker = server.System<ActionBlockerSystem>();
        var hands = server.System<SharedHandsSystem>();
        var database = server.ResolveDependency<IServerDbManager>();
        EntityUid body = default;

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisonerSlag", new MapCoordinates(Vector2.Zero, mapId));
            server.EntMan.SpawnEntity("SpawnPointPrisonerFrontier", new MapCoordinates(Vector2.One, mapId));

            body = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(4, 4), mapId));
            var playerMind = mind.CreateMind(player.UserId, player.Name);
            mind.TransferTo(playerMind, body);
            ((IDictionary<NetUserId, PlayerGameStatus>) ticker.PlayerGameStatuses)[player.UserId] =
                PlayerGameStatus.JoinedGame;
        });
        await pair.RunTicksSync(2);

        var now = DateTimeOffset.UtcNow;
        var ban = await AddPrisonBan(database, player.UserId, now, now + TimeSpan.FromHours(1));
        await server.WaitPost(() =>
        {
            Assert.That(prison.TrySendToPrison(player, ban), Is.True);
            Assert.That(server.EntMan.HasComponent<PrisonFactionSelectionLockedComponent>(body), Is.True);
            Assert.That(blocker.CanMove(body), Is.False);

            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, false);
            Assert.That(prison.TrySelectFaction(player, "PrisonFrontier"), Is.False);
            Assert.That(server.EntMan.HasComponent<PrisonFactionSelectionLockedComponent>(body), Is.True);

            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            Assert.That(prison.TrySelectFaction(player, "InvalidFaction"), Is.False);
            Assert.That(server.EntMan.HasComponent<PrisonFactionSelectionLockedComponent>(body), Is.True);

            Assert.That(prison.TrySelectFaction(player, "PrisonFrontier"), Is.True);
            Assert.That(server.EntMan.HasComponent<PrisonFactionSelectionLockedComponent>(body), Is.False);
            Assert.That(blocker.CanMove(body), Is.True);
            Assert.That(hands.EnumerateHeld(body), Is.Empty);
            Assert.That(
                server.EntMan.GetComponent<PrisonFactionMemberComponent>(body).Faction.Id,
                Is.EqualTo("PrisonFrontier"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobbyObserverDoesNotStartFactionSelection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var player = pair.Player!;
        var prison = server.System<PrisonSystem>();
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();
        var mind = server.System<Content.Server.Mind.MindSystem>();
        var database = server.ResolveDependency<IServerDbManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisonerSlag", new MapCoordinates(Vector2.Zero, mapId));
            server.EntMan.SpawnEntity("SpawnPointPrisonerFrontier", new MapCoordinates(Vector2.One, mapId));
        });

        Assert.That(ticker.UserHasJoinedGame(player), Is.False);
        var now = DateTimeOffset.UtcNow;
        var ban = await AddPrisonBan(database, player.UserId, now, now + TimeSpan.FromHours(1));

        await server.WaitPost(() =>
        {
            Assert.That(prison.TrySendToPrison(player, ban), Is.True);
            Assert.That(prison.GetFactionEuiState(player).SecondsRemaining, Is.Zero);

            var observer = server.EntMan.SpawnEntity(
                Content.Server.GameTicking.GameTicker.ObserverPrototypeName,
                MapCoordinates.Nullspace);
            var playerMind = mind.CreateMind(player.UserId, player.Name);
            mind.TransferTo(playerMind, observer);
            ((IDictionary<NetUserId, PlayerGameStatus>) ticker.PlayerGameStatuses)[player.UserId] =
                PlayerGameStatus.JoinedGame;
        });
        await pair.RunTicksSync(server.ResolveDependency<IGameTiming>().TickRate * 2);

        var observer = player.AttachedEntity;
        Assert.That(observer, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(ticker.UserHasJoinedGame(player), Is.True);
            Assert.That(server.EntMan.HasComponent<GhostComponent>(observer!.Value), Is.True);
            Assert.That(server.EntMan.HasComponent<PrisonBoundComponent>(observer.Value), Is.False);
            Assert.That(prison.GetFactionEuiState(player).SecondsRemaining, Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactionSelectionTimeoutAutomaticallySpawnsPrisoner()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var player = pair.Player!;
        var prison = server.System<PrisonSystem>();
        var ticker = server.System<Content.Server.GameTicking.GameTicker>();
        var mind = server.System<Content.Server.Mind.MindSystem>();
        var database = server.ResolveDependency<IServerDbManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CCCCVars.PrisonEnabled, true);
            server.CfgMan.SetCVar(CCCCVars.PrisonFactionSelectionSeconds, 5);
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisonerSlag", new MapCoordinates(Vector2.Zero, mapId));
            server.EntMan.SpawnEntity("SpawnPointPrisonerFrontier", new MapCoordinates(Vector2.One, mapId));

            var ghost = server.EntMan.SpawnEntity(
                Content.Server.GameTicking.GameTicker.ObserverPrototypeName,
                MapCoordinates.Nullspace);
            var playerMind = mind.CreateMind(player.UserId, player.Name);
            mind.TransferTo(playerMind, ghost);
            ((IDictionary<NetUserId, PlayerGameStatus>) ticker.PlayerGameStatuses)[player.UserId] =
                PlayerGameStatus.JoinedGame;
        });
        await pair.RunTicksSync(2);

        var now = DateTimeOffset.UtcNow;
        var ban = await AddPrisonBan(database, player.UserId, now, now + TimeSpan.FromHours(1));
        await server.WaitPost(() =>
        {
            Assert.That(prison.TrySendToPrison(player, ban), Is.True);
            Assert.That(prison.GetFactionEuiState(player).SecondsRemaining, Is.GreaterThan(0));
        });

        var timing = server.ResolveDependency<IGameTiming>();
        await pair.RunTicksSync(timing.TickRate * 7);

        var prisoner = player.AttachedEntity;
        Assert.That(prisoner, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(server.EntMan.HasComponent<GhostComponent>(prisoner!.Value), Is.False);
            Assert.That(server.EntMan.HasComponent<PrisonBoundComponent>(prisoner.Value), Is.True);
            Assert.That(server.EntMan.HasComponent<PrisonFactionMemberComponent>(prisoner.Value), Is.True);
            Assert.That(prison.GetFactionEuiState(player).SecondsRemaining, Is.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PhysicalShipmentUsesLooseOreBelowThresholdAndOpenCrateAtThreshold()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var userId = new NetUserId(Guid.NewGuid());
        EntityUid shuttle = default;

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var grid = server.System<SharedMapSystem>().CreateGridEntity(mapId);
            shuttle = grid.Owner;
            for (var x = 0; x < 3; x++)
                mapSystem.SetTile(grid.Owner, grid.Comp, new Vector2i(x, 0), new Tile(1));
            server.EntMan.EnsureComponent<CargoShuttleComponent>(shuttle);

            var processor = new PrisonOreProcessorComponent
            {
                PointsPerSecond = 10,
                CrateMinimumUnits = 10,
            };
            processor.OreValues["SteelOre"] = 1;

            var prisonOre = server.System<PrisonOreSystem>();
            Assert.That(
                prisonOre.TryCreatePhysicalShipment(
                    new Dictionary<Robust.Shared.Prototypes.ProtoId<StackPrototype>, int> { ["SteelOre"] = 5 },
                    userId,
                    1,
                    processor,
                    out _),
                Is.True);
            Assert.That(
                prisonOre.TryCreatePhysicalShipment(
                    new Dictionary<Robust.Shared.Prototypes.ProtoId<StackPrototype>, int> { ["SteelOre"] = 10 },
                    userId,
                    1,
                    processor,
                    out _),
                Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var looseUnits = 0;
            var containedUnits = 0;
            var shipmentQuery = server.EntMan.EntityQueryEnumerator<PrisonOreShipmentComponent, StackComponent, TransformComponent>();
            while (shipmentQuery.MoveNext(out var uid, out _, out var stack, out var xform))
            {
                if (xform.GridUid != shuttle)
                    continue;

                if (server.EntMan.HasComponent<InsideEntityStorageComponent>(uid))
                    containedUnits += stack.Count;
                else
                    looseUnits += stack.Count;
            }

            var crateCount = 0;
            var crateQuery = server.EntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (crateQuery.MoveNext(out var uid, out var metadata, out var xform))
            {
                if (xform.GridUid != shuttle || metadata.EntityPrototype?.ID != "CratePrisonOreShipment")
                    continue;

                crateCount++;
                Assert.That(server.EntMan.HasComponent<PrisonOreShipmentComponent>(uid), Is.False);
                Assert.That(server.EntMan.GetComponent<EntityStorageComponent>(uid).Contents.ContainedEntities, Is.Not.Empty);
            }

            Assert.Multiple(() =>
            {
                Assert.That(looseUnits, Is.EqualTo(5));
                Assert.That(containedUnits, Is.EqualTo(10));
                Assert.That(crateCount, Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FindsDirectCargoShuttlePlacementWithoutPallets()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        EntityUid shuttle = default;
        EntityCoordinates coordinates = EntityCoordinates.Invalid;

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var grid = server.System<SharedMapSystem>().CreateGridEntity(mapId);
            shuttle = grid.Owner;
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            server.EntMan.EnsureComponent<CargoShuttleComponent>(shuttle);

            Assert.That(server.System<PrisonOreSystem>().TryGetCargoSpawnCoordinates(out coordinates), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(coordinates.EntityId, Is.EqualTo(shuttle));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipmentCreditFollowsStackSplitAndMerge()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        EntityUid source = default;
        EntityUid split = default;
        EntityUid recipient = default;
        var userId = new NetUserId(Guid.NewGuid());

        await server.WaitPost(() =>
        {
            source = server.EntMan.SpawnEntity("SteelOre", MapCoordinates.Nullspace);
            recipient = server.EntMan.SpawnEntity("SteelOre", MapCoordinates.Nullspace);
            var stackSystem = server.System<StackSystem>();
            var sourceStack = server.EntMan.GetComponent<StackComponent>(source);
            var recipientStack = server.EntMan.GetComponent<StackComponent>(recipient);
            stackSystem.SetCount((source, sourceStack), 30);
            stackSystem.SetCount((recipient, recipientStack), 10);

            server.System<PrisonOreSystem>().SetShipmentTracking(source, "SteelOre", 10, userId, 42, 1_000);

            split = stackSystem.Split(
                (source, sourceStack),
                12,
                server.EntMan.GetComponent<TransformComponent>(source).Coordinates)!.Value;

            var splitStack = server.EntMan.GetComponent<StackComponent>(split);
            Assert.That(stackSystem.TryMergeStacks((split, splitStack), (recipient, recipientStack), out _), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var shipment = server.EntMan.GetComponent<PrisonOreShipmentComponent>(recipient);
            Assert.Multiple(() =>
            {
                Assert.That(shipment.Ores["SteelOre"], Is.EqualTo(10));
                Assert.That(shipment.Contributions[0].ReductionTicks, Is.EqualTo(1_000));
                if (server.EntMan.TryGetComponent<PrisonOreShipmentComponent>(source, out var sourceShipment))
                    Assert.That(sourceShipment.Ores, Is.Empty);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OreMinedOnPrisonMapBecomesEligible()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        EntityUid[] minedOres = [];

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            server.EntMan.SpawnEntity("SpawnPointPrisoner", new MapCoordinates(Vector2.Zero, mapId));
            var vein = server.EntMan.SpawnEntity("MeteorRockCoal", new MapCoordinates(Vector2.One, mapId));

            server.EntMan.EventBus.RaiseLocalEvent(vein, new DestructionEventArgs());

            var query = server.EntMan.EntityQueryEnumerator<PrisonMinedOreComponent, StackComponent, TransformComponent>();
            var result = new List<EntityUid>();
            while (query.MoveNext(out var uid, out var mined, out var stack, out var xform))
            {
                if (xform.MapID != mapId)
                    continue;

                Assert.That(mined.EligibleUnits, Is.EqualTo(stack.Count));
                result.Add(uid);
            }

            minedOres = result.ToArray();
        });

        Assert.That(minedOres, Is.Not.Empty, "Ore spawned from a prison-map vein must be marked as eligible.");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SplitMovesOnlyEligibleUnits()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        EntityUid source = default;
        EntityUid split = default;

        await server.WaitPost(() =>
        {
            source = server.EntMan.SpawnEntity("SteelOre", MapCoordinates.Nullspace);
            var stack = server.EntMan.GetComponent<StackComponent>(source);
            server.System<StackSystem>().SetCount((source, stack), 30);
            server.System<PrisonOreSystem>().SetEligibleUnits(source, 10);

            split = server.System<StackSystem>().Split(
                (source, stack),
                12,
                server.EntMan.GetComponent<TransformComponent>(source).Coordinates)!.Value;
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<StackComponent>(source).Count, Is.EqualTo(18));
                Assert.That(server.EntMan.GetComponent<StackComponent>(split).Count, Is.EqualTo(12));
                Assert.That(
                    server.EntMan.GetComponent<PrisonMinedOreComponent>(source).EligibleUnits,
                    Is.EqualTo(0));
                Assert.That(server.EntMan.GetComponent<PrisonMinedOreComponent>(split).EligibleUnits, Is.EqualTo(10));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MergeConservesEligibleUnitsInMixedStacks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        EntityUid donor = default;
        EntityUid recipient = default;

        await server.WaitPost(() =>
        {
            donor = server.EntMan.SpawnEntity("SteelOre", MapCoordinates.Nullspace);
            recipient = server.EntMan.SpawnEntity("SteelOre", MapCoordinates.Nullspace);
            var donorStack = server.EntMan.GetComponent<StackComponent>(donor);
            var recipientStack = server.EntMan.GetComponent<StackComponent>(recipient);
            var stackSystem = server.System<StackSystem>();
            stackSystem.SetCount((donor, donorStack), 20);
            stackSystem.SetCount((recipient, recipientStack), 10);
            server.System<PrisonOreSystem>().SetEligibleUnits(donor, 7);

            Assert.That(
                stackSystem.TryMergeStacks((donor, donorStack), (recipient, recipientStack), out var transferred),
                Is.True);
            Assert.That(transferred, Is.EqualTo(20));
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<StackComponent>(recipient).Count, Is.EqualTo(30));
                Assert.That(server.EntMan.GetComponent<PrisonMinedOreComponent>(recipient).EligibleUnits, Is.EqualTo(7));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RewardOnlyChangesLatestTemporaryPrisonBan()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var database = server.ResolveDependency<IServerDbManager>();
        var prison = server.System<PrisonSystem>();
        var userId = new NetUserId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var oldExpiration = now + TimeSpan.FromHours(2);
        var latestExpiration = now + TimeSpan.FromHours(3);

        var oldBan = await AddPrisonBan(database, userId, now - TimeSpan.FromMinutes(2), oldExpiration);
        var latestBan = await AddPrisonBan(database, userId, now - TimeSpan.FromMinutes(1), latestExpiration);

        Assert.That(
            await prison.TryReduceSentence(userId, oldBan.Id!.Value, TimeSpan.FromMinutes(5)),
            Is.EqualTo(TimeSpan.Zero),
            "Ore from an older sentence must not reduce a superseded ban.");
        Assert.That((await database.GetBanAsync(oldBan.Id.Value))!.ExpirationTime, Is.EqualTo(oldExpiration));

        Assert.That(
            await prison.TryReduceSentence(userId, latestBan.Id!.Value, TimeSpan.FromMinutes(5)),
            Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(
            (await database.GetBanAsync(latestBan.Id.Value))!.ExpirationTime,
            Is.EqualTo(latestExpiration - TimeSpan.FromMinutes(5)));

        await database.SetBanPrisonAccess(latestBan.Id.Value, false);
        Assert.That(
            await prison.TryReduceSentence(userId, latestBan.Id.Value, TimeSpan.FromMinutes(5)),
            Is.EqualTo(TimeSpan.Zero),
            "Revoking prison access must invalidate an ore shipment reward.");

        var permanentBan = await AddPrisonBan(database, userId, now, null);
        Assert.That(
            await prison.TryReduceSentence(userId, permanentBan.Id!.Value, TimeSpan.FromMinutes(5)),
            Is.EqualTo(TimeSpan.Zero),
            "A permanent sentence must never be reduced.");

        await pair.CleanReturnAsync();
    }

    private static Task<BanDef> AddPrisonBan(
        IServerDbManager database,
        NetUserId userId,
        DateTimeOffset banTime,
        DateTimeOffset? expiration)
    {
        return database.AddBanAsync(new BanDef(
            null,
            BanType.Server,
            ImmutableArray.Create(userId),
            ImmutableArray<(IPAddress address, int cidrMask)>.Empty,
            ImmutableArray<ImmutableTypedHwid>.Empty,
            banTime,
            expiration,
            ImmutableArray<int>.Empty,
            TimeSpan.Zero,
            "prison ore test",
            NoteSeverity.Minor,
            null,
            null,
            sendToPrison: true));
    }
}

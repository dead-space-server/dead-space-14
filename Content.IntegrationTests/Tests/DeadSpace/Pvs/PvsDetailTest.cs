// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared.Body.Systems;
using Content.Shared.PDA;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Light.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Pvs;

[TestFixture]
[NonParallelizable]
public sealed class PvsDetailTest
{
    [Test]
    public async Task ClosedStorageContentsOnlyNearby()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var ticker = server.System<GameTicker>();
        var xforms = server.System<SharedTransformSystem>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.NetPVS, true);
            server.CfgMan.SetCVar(CVars.NetMaxUpdateRange, 24f);
            server.CfgMan.SetCVar(CVars.NetPvsPriorityRange, 30f);
            server.CfgMan.SetCVar(CVars.NetPvsDetailRange, 4f);
            server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            server.CfgMan.SetCVar(CCVars.GameMap, "Box");
            ticker.RestartRound();
        });

        await PoolManager.WaitUntil(server, () => ticker.RunLevel == GameRunLevel.InRound, maxTicks: 3600, tickStep: 10);

        EntityUid locker = default;
        EntityUid item = default;
        EntityCoordinates near = default;
        EntityCoordinates far = default;
        EntityUid mob = default;
        EntityUid belt = default;
        EntityUid pawn = default;
        EntityUid gun = default;
        EntityUid lantern = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var storage, out var xform))
            {
                if (xform.GridUid is not { } grid || xform.ParentUid != grid || storage.Open ||
                    storage.Contents.ContainedEntities.Count == 0)
                {
                    continue;
                }

                locker = uid;
                item = storage.Contents.ContainedEntities[0];
                near = xform.Coordinates.Offset(new Vector2(1f, 0f));
                var chunk = new Vector2(MathF.Floor(xform.LocalPosition.X / 8f) + 0.5f, MathF.Floor(xform.LocalPosition.Y / 8f) + 0.5f) * 8f;
                far = new EntityCoordinates(grid, chunk + new Vector2(12f, 0f));
                break;
            }

            Assert.That(locker.IsValid(), "No closed filled entity storage on the map.");
            var flags = sEntMan.GetComponent<MetaDataComponent>(item).Flags;
            Assert.That(flags & MetaDataFlags.PvsDetail, Is.EqualTo(MetaDataFlags.PvsDetail));

            mob = sEntMan.SpawnAttachedTo("MobHuman", near);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.First(), mob);
            belt = sEntMan.SpawnAttachedTo("ClothingBeltUtilityFilled", near);
            pawn = sEntMan.SpawnAttachedTo("MobHuman", near);
            gun = sEntMan.SpawnAttachedTo("WeaponPistolMk58", near);
            lantern = sEntMan.SpawnAttachedTo("FlashlightLantern", near);
        });

        var netLocker = sEntMan.GetNetEntity(locker);
        var netItem = sEntMan.GetNetEntity(item);
        var netBelt = sEntMan.GetNetEntity(belt);
        var netPawn = sEntMan.GetNetEntity(pawn);
        var netGun = sEntMan.GetNetEntity(gun);
        var netLantern = sEntMan.GetNetEntity(lantern);
        NetEntity netTool = default;
        NetEntity netPart = default;
        NetEntity netMagazine = default;
        NetEntity netCell = default;
        NetEntity netCard = default;
        await server.WaitPost(() =>
        {
            var tools = server.System<SharedContainerSystem>().GetContainer(belt, StorageComponent.ContainerId);
            Assert.That(tools.ContainedEntities, Is.Not.Empty);
            netTool = sEntMan.GetNetEntity(tools.ContainedEntities[0]);

            var part = server.System<SharedBodySystem>().GetBodyChildren(pawn).First().Id;
            netPart = sEntMan.GetNetEntity(part);

            var slots = server.System<ItemSlotsSystem>();
            var magazine = slots.GetItemOrNull(gun, "gun_magazine");
            var cell = slots.GetItemOrNull(lantern, "cell_slot");
            Assert.That(magazine, Is.Not.Null, "The pistol has no magazine.");
            Assert.That(cell, Is.Not.Null, "The lantern has no cell.");
            netMagazine = sEntMan.GetNetEntity(magazine.Value);
            netCell = sEntMan.GetNetEntity(cell.Value);

            var pda = sEntMan.SpawnAttachedTo("PassengerPDA", near);
            var card = sEntMan.GetComponent<PdaComponent>(pda).ContainedId;
            Assert.That(card, Is.Not.Null, "The PDA has no ID card.");
            Assert.That(sEntMan.GetComponent<MetaDataComponent>(card.Value).Flags & MetaDataFlags.PvsDetail, Is.EqualTo((MetaDataFlags) 0));
            netCard = sEntMan.GetNetEntity(card.Value);
        });

        await pair.RunTicksSync(90);
        Assert.That(Seen(cEntMan, netItem), Is.True, "Contents are missing next to the storage.");
        Assert.That(Seen(cEntMan, netPart), Is.True, "Body parts are missing up close.");
        Assert.That(Seen(cEntMan, netMagazine), Is.True, "The magazine is missing up close.");
        Assert.That(Seen(cEntMan, netCell), Is.True, "The cell is missing up close.");
        var beltLayers = BeltLayers(cEntMan, netBelt);
        Assert.That(beltLayers, Is.Not.Empty, "The filled belt shows no tools up close.");
        var gunLook = Look(cEntMan, netGun, AmmoVisuals.MagLoaded);
        var lanternLook = Look(cEntMan, netLantern, HandheldLightVisuals.Power);

        await server.WaitPost(() => xforms.SetCoordinates(mob, far));
        await pair.RunTicksSync(90);
        Assert.That(Seen(cEntMan, netLocker), Is.True, "The storage itself left PVS.");
        Assert.That(Seen(cEntMan, netPawn), Is.True, "The other player left PVS.");
        Assert.That(Seen(cEntMan, netGun), Is.True, "The pistol left PVS.");
        Assert.That(Seen(cEntMan, netItem), Is.False, "Contents were sent from far away.");
        Assert.That(Seen(cEntMan, netTool), Is.False, "Belt tools were sent from far away.");
        Assert.That(Seen(cEntMan, netPart), Is.False, "Body parts were sent from far away.");
        Assert.That(Seen(cEntMan, netMagazine), Is.False, "The magazine was sent from far away.");
        Assert.That(Seen(cEntMan, netCell), Is.False, "The cell was sent from far away.");
        Assert.That(Seen(cEntMan, netCard), Is.True, "The ID card was not sent from far away.");
        Assert.That(BeltLayers(cEntMan, netBelt), Is.EqualTo(beltLayers), "The belt looks different from far away.");
        Assert.That(Look(cEntMan, netGun, AmmoVisuals.MagLoaded), Is.EqualTo(gunLook), "The pistol looks different from far away.");
        Assert.That(Look(cEntMan, netLantern, HandheldLightVisuals.Power), Is.EqualTo(lanternLook), "The lantern looks different from far away.");

        await server.WaitPost(() => xforms.SetCoordinates(mob, near));
        await pair.RunTicksSync(90);
        Assert.That(Seen(cEntMan, netItem), Is.True, "Contents did not come back.");

        await server.WaitPost(() =>
        {
            server.System<SharedEntityStorageSystem>().OpenStorage(locker);
            Assert.That(sEntMan.GetComponent<MetaDataComponent>(item).Flags & MetaDataFlags.PvsDetail, Is.EqualTo((MetaDataFlags) 0));
            xforms.SetCoordinates(mob, far);
        });

        await pair.RunTicksSync(90);
        Assert.That(Seen(cEntMan, netItem), Is.True, "Items from an opened storage are hidden from far away.");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UncoveredCableSeenFromFar()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var sEntMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var maps = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(CVars.NetPVS, true);
            server.CfgMan.SetCVar(CVars.NetMaxUpdateRange, 24f);
            server.CfgMan.SetCVar(CVars.NetPvsPriorityRange, 30f);
            server.CfgMan.SetCVar(CVars.NetPvsDetailRange, 4f);
            server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            server.CfgMan.SetCVar(CCVars.GameMap, "Box");
            ticker.RestartRound();
        });

        await PoolManager.WaitUntil(server, () => ticker.RunLevel == GameRunLevel.InRound, maxTicks: 3600, tickStep: 10);

        EntityUid cable = default;
        EntityUid grid = default;
        Vector2i tile = default;
        await server.WaitPost(() =>
        {
            var query = sEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID != "CableApcExtension" ||
                    (meta.Flags & MetaDataFlags.PvsDetail) == 0 ||
                    xform.GridUid is not { } gridUid)
                {
                    continue;
                }

                cable = uid;
                grid = gridUid;
                break;
            }

            Assert.That(cable.IsValid(), "No covered cable on the map.");
            var xformCable = sEntMan.GetComponent<TransformComponent>(cable);
            var gridComp = sEntMan.GetComponent<MapGridComponent>(grid);
            tile = maps.TileIndicesFor(grid, gridComp, xformCable.Coordinates);
            var chunk = new Vector2(MathF.Floor(xformCable.LocalPosition.X / 8f) + 0.5f, MathF.Floor(xformCable.LocalPosition.Y / 8f) + 0.5f) * 8f;
            var mob = sEntMan.SpawnAttachedTo("MobHuman", new EntityCoordinates(grid, chunk + new Vector2(12f, 0f)));
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.First(), mob);
        });

        var netCable = sEntMan.GetNetEntity(cable);
        await pair.RunTicksSync(90);
        Assert.That(Seen(pair.Client.EntMan, netCable), Is.False, "A covered cable was sent from far away.");

        await server.WaitPost(() =>
        {
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            maps.SetTile(grid, sEntMan.GetComponent<MapGridComponent>(grid), tile, new Tile(plating));
            Assert.That(sEntMan.GetComponent<MetaDataComponent>(cable).Flags & MetaDataFlags.PvsDetail, Is.EqualTo((MetaDataFlags) 0));
        });

        await pair.RunTicksSync(90);
        Assert.That(Seen(pair.Client.EntMan, netCable), Is.True, "An uncovered cable is hidden from far away.");

        await pair.CleanReturnAsync();
    }

    private static string Look(IEntityManager entMan, NetEntity net, Enum key)
    {
        var uid = entMan.GetEntity(net);
        return entMan.System<SharedAppearanceSystem>().TryGetData<object>(uid, key, out var data)
            ? data.ToString()
            : "(none)";
    }

    private static string BeltLayers(IEntityManager entMan, NetEntity net)
    {
        var uid = entMan.GetEntity(net);
        return entMan.System<SharedAppearanceSystem>().TryGetData<ShowLayerData>(uid, StorageMapVisuals.LayerChanged, out var data)
            ? string.Join(",", data.QueuedEntities)
            : string.Empty;
    }

    private static bool Seen(IEntityManager entMan, NetEntity net)
    {
        return entMan.TryGetEntity(net, out var uid)
               && entMan.TryGetComponent(uid, out MetaDataComponent meta)
               && (meta.Flags & MetaDataFlags.Detached) == 0;
    }
}

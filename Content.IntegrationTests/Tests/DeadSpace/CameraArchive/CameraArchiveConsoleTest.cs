// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Client.DeadSpace.CameraArchives;
using Content.Server.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.UniformAccessories.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.SSDIndicator;
using Content.Shared.SurveillanceCamera;
using Content.Shared.TextScreen;
using Content.Shared.DeadSpace.SignBoard;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.CameraArchives;

[TestFixture]
public sealed class CameraArchiveConsoleTest
{
    [Test]
    public void ZoomLocksPanUntilMagnified()
    {
        var half = new Vector2(8f, 6f);
        Assert.That(CameraArchiveViewMath.ClampPan(new Vector2(100f, -40f), half, 1f), Is.EqualTo(Vector2.Zero));

        var clamped = CameraArchiveViewMath.ClampPan(new Vector2(100f, -40f), half, 2f);
        Assert.That(clamped.X, Is.EqualTo(4f).Within(0.001f));
        Assert.That(clamped.Y, Is.EqualTo(-3f).Within(0.001f));

        var inside = CameraArchiveViewMath.ClampPan(new Vector2(1f, -1f), half, 2f);
        Assert.That(inside.X, Is.EqualTo(1f).Within(0.001f));
        Assert.That(inside.Y, Is.EqualTo(-1f).Within(0.001f));

        var zoomedIn = CameraArchiveViewMath.EyeZoom(Vector2.One, 2f);
        Assert.That(zoomedIn.X, Is.EqualTo(0.5f).Within(0.001f));
        var wider = CameraArchiveViewMath.EyeZoom(Vector2.One, 0.25f);
        Assert.That(wider.X, Is.LessThanOrEqualTo(1f));
        Assert.That(CameraArchiveViewMath.EyeZoom(new Vector2(2f, 2f), 1f).X, Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void HistoryDropsFramesOlderThanTheWindow()
    {
        var frames = new Queue<CameraArchiveSystem.CameraFrame>();
        frames.Enqueue(new CameraArchiveSystem.CameraFrame(TimeSpan.FromMinutes(0), new CameraArchive()));
        frames.Enqueue(new CameraArchiveSystem.CameraFrame(TimeSpan.FromMinutes(11), new CameraArchive()));

        CameraArchiveSystem.TrimOlderThan(frames, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(10));

        Assert.That(frames, Has.Count.EqualTo(1));
        Assert.That(frames.Peek().RoundTime, Is.EqualTo(TimeSpan.FromMinutes(11)));
        Assert.That(CameraArchiveViewMath.AcceptPlayback(4, 4), Is.True);
        Assert.That(CameraArchiveViewMath.AcceptPlayback(4, 2), Is.False);
    }

    [Test]
    public async Task EveryCameraKeepsAFrameAndPrintShowsIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var stills = server.System<CameraArchiveSystem>();
            var archive = server.System<CameraArchiveConsoleSystem>();

            var first = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            var second = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            server.System<MetaDataSystem>().SetEntityName(first, "Архив-камера");

            var firstStill = stills.Capture(first);
            var secondStill = stills.Capture(second);
            Assert.That(firstStill, Is.Not.Null);
            Assert.That(secondStill, Is.Not.Null);
            stills.Remember(first, firstStill!, TimeSpan.FromMinutes(2));
            stills.Remember(second, secondStill!, TimeSpan.FromMinutes(2));
            Assert.That(stills.Count(first), Is.EqualTo(1));
            Assert.That(stills.Count(second), Is.EqualTo(1));

            stills.Remember(first, new CameraArchive(), TimeSpan.Zero);
            stills.Remember(first, new CameraArchive(), TimeSpan.FromMinutes(11));
            Assert.That(stills.TryGetFrame(first, 0, out var oldest), Is.True);
            Assert.That(oldest.RoundTime, Is.GreaterThan(TimeSpan.Zero), "a frame from the start of a 10 minute window must be gone");

            var console = ents.SpawnEntity("ComputerSurveillanceCameraMonitor", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var picture = new byte[] { 9, 8, 7, 6 };
            Assert.That(archive.TryPrint(console, EntityUid.Invalid, first, -1, picture, out var paper), Is.True);
            var content = ents.GetComponent<PaperComponent>(paper).Content;
            Assert.That(content, Does.Contain("Архив-камера"));
            Assert.That(content, Does.Contain("00:11:00"));
            Assert.That(content, Does.Contain("Запись с камеры видеонаблюдения"));
            Assert.That(content, Does.Contain("{СЮДА КАРТИНКУ}"));
            Assert.That(content, Does.Not.Contain("{В кадре}"));
            Assert.That(content, Does.Not.Contain("Лог сообщений"));
            Assert.That(content, Does.Contain("Место для печатей"));
            Assert.That(ents.GetComponent<CameraArchivePrintComponent>(paper).Image, Is.EqualTo(picture));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HiddenPartsStayHiddenAndTextAndPeopleAreCopied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var stills = server.System<CameraArchiveSystem>();
            var appearance = server.System<SharedAppearanceSystem>();

            var camera = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            var comms = ents.SpawnEntity("ComputerComms", map.GridCoords);
            var sign = ents.SpawnEntity("SignBoard", map.GridCoords);
            ents.GetComponent<SignBoardComponent>(sign).Text = "BAR";
            appearance.SetData(sign, TextScreenVisuals.ScreenText, "BAR");

            var mob = ents.SpawnEntity("MobHuman", map.GridCoords);
            ents.GetComponent<SSDIndicatorComponent>(mob).IsSSD = false;
            var shirt = ents.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(mob, shirt, "jumpsuit", force: true), Is.True);

            var still = stills.Capture(camera);
            Assert.That(still, Is.Not.Null);

            var board = still!.Entities.Find(entity => entity.Prototype == "CommsComputerCircuitboard");
            Assert.That(board, Is.Not.Null);
            Assert.That(board!.Container, Is.EqualTo("board"));

            var copiedSign = still.Entities.Find(entity => entity.Prototype == "SignBoard");
            Assert.That(copiedSign, Is.Not.Null);
            Assert.That(copiedSign!.SignText, Is.EqualTo("BAR"));
            Assert.That(copiedSign.ScreenText, Is.EqualTo("BAR"));

            var copiedMob = still.Entities.Find(entity => entity.Prototype == "MobHuman");
            Assert.That(copiedMob, Is.Not.Null);
            Assert.That(copiedMob!.CopySsd, Is.True);
            Assert.That(copiedMob.IsSsd, Is.False);
            Assert.That(copiedMob.Humanoid, Is.Not.Null);
            Assert.That(copiedMob.Humanoid!.Length, Is.GreaterThan(0));

            var copiedShirt = still.Entities.Find(entity => entity.Prototype == "ClothingUniformJumpsuitColorGrey");
            Assert.That(copiedShirt, Is.Not.Null);
            Assert.That(copiedShirt!.Container, Is.EqualTo("jumpsuit"));

            Assert.That(stills.TryReuse(camera, TimeSpan.FromSeconds(2)), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlaybackKeepsSkinClothesAndHeldItem()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var skin = new Color(1f, 0.2f, 0.2f);
        CameraArchive still = null;

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var camera = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            var mob = ents.SpawnEntity("MobHuman", map.GridCoords);
            ents.GetComponent<SSDIndicatorComponent>(mob).IsSSD = false;
            server.System<SharedHumanoidAppearanceSystem>().SetSkinColor(mob, skin, verify: false);

            var shirt = ents.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(mob, shirt, "jumpsuit", force: true), Is.True);
            var tool = ents.SpawnEntity("Crowbar", map.GridCoords);
            var hands = server.System<SharedHandsSystem>();
            string hand = null;
            foreach (var name in hands.EnumerateHands(mob))
            {
                hand = name;
                break;
            }

            Assert.That(hand, Is.Not.Null);
            Assert.That(hands.TryForcePickup(mob, tool, hand!, checkActionBlocker: false), Is.True);

            still = server.System<CameraArchiveSystem>().Capture(camera);
            Assert.That(still, Is.Not.Null);
            var copied = still!.Entities.Find(entity => entity.Prototype == "MobHuman");
            Assert.That(copied, Is.Not.Null);
            Assert.That(copied!.Humanoid, Is.Not.Null);
            var copiedTool = still.Entities.Find(entity => entity.Prototype == "Crowbar");
            Assert.That(copiedTool, Is.Not.Null);
            Assert.That(copiedTool!.Container, Is.Not.Empty);
        });

        await client.WaitAssertion(() =>
        {
            var playback = client.System<CameraArchivePlaybackSystem>();
            var view = playback.Play(still!);
            playback.Settle();

            var found = false;
            var query = client.EntMan.EntityQueryEnumerator<HumanoidAppearanceComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var humanoid, out var xform))
            {
                if (xform.MapID != view.MapId)
                    continue;

                found = true;
                Assert.That(humanoid.SkinColor.R, Is.EqualTo(skin.R).Within(0.02f));
                Assert.That(humanoid.SkinColor.G, Is.EqualTo(skin.G).Within(0.02f));
                Assert.That(client.System<InventorySystem>().TryGetSlotEntity(uid, "jumpsuit", out var worn), Is.True);
                Assert.That(client.EntMan.GetComponent<MetaDataComponent>(worn!.Value).EntityPrototype?.ID, Is.EqualTo("ClothingUniformJumpsuitColorGrey"));
                var hand = still!.Entities.Find(entity => entity.Prototype == "Crowbar")!.Container;
                Assert.That(client.System<SharedHandsSystem>().TryGetHeldItem(uid, hand, out var held), Is.True);
                Assert.That(client.EntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID, Is.EqualTo("Crowbar"));
            }

            Assert.That(found, Is.True);
            playback.Clear();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyCameraRecordsOnlyWhenNamedAndCarried()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var stills = server.System<CameraArchiveSystem>();
            var hands = server.System<SharedHandsSystem>();
            var inventory = server.System<InventorySystem>();

            var body = ents.SpawnEntity("ClothingNeckBodyCamera", map.GridCoords);
            var mob = ents.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(stills.ShouldRecord(body), Is.False);

            Assert.That(hands.TryPickupAnyHand(mob, body), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.False, "a held body camera still needs a name");

            var naming = new SurveillanceCameraSetupSetName("Пост-1")
            {
                UiKey = SurveillanceCameraSetupUiKey.Camera,
            };
            ents.EventBus.RaiseLocalEvent(body, naming);
            Assert.That(stills.ShouldRecord(body), Is.True);

            Assert.That(hands.TryDrop(mob, body, checkActionBlocker: false), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.False);

            Assert.That(inventory.TryEquip(mob, body, "neck", force: true, silent: true), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.True);

            Assert.That(inventory.TryUnequip(mob, "neck", silent: true, force: true), Is.True);
            var suit = ents.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            var holder = ents.GetComponent<UniformAccessoryHolderComponent>(suit);
            Assert.That(holder.AccessoryContainer, Is.Not.Null);
            Assert.That(server.System<SharedContainerSystem>().Insert(body, holder.AccessoryContainer), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.False);

            Assert.That(inventory.TryEquip(mob, suit, "jumpsuit", force: true, silent: true), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.True);

            Assert.That(inventory.TryUnequip(mob, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(stills.ShouldRecord(body), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CloakCoversOnlyTheWearer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        CameraArchive still = null;

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var stills = server.System<CameraArchiveSystem>();
            var inventory = server.System<InventorySystem>();

            var camera = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            var cloaked = ents.SpawnEntity("MobHuman", map.GridCoords);
            ents.SpawnEntity("MobHuman", map.GridCoords);
            var cloak = ents.SpawnEntity("ClothingNeckCameraConcealCloak", map.GridCoords);
            Assert.That(inventory.TryEquip(cloaked, cloak, "neck", force: true, silent: true), Is.True);

            still = stills.Capture(camera);
            Assert.That(still, Is.Not.Null);
            Assert.That(still!.Interference, Is.False);

            var hidden = still.Entities.FindAll(entity => entity.Conceal);
            Assert.That(hidden, Has.Count.EqualTo(1));
            Assert.That(hidden[0].Prototype, Is.EqualTo("MobHuman"));
            Assert.That(hidden[0].Humanoid, Is.Null);

            var visible = still.Entities.FindAll(entity => entity.Prototype == "MobHuman" && !entity.Conceal);
            Assert.That(visible, Has.Count.EqualTo(1));
            Assert.That(visible[0].Humanoid, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }
}

using System.Linq;
using System.Numerics;
using Content.Client.DeadSpace.Mapping;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using ClientDecalPlacementSystem = Content.Client.Decals.DecalPlacementSystem;
using ServerDecalSystem = Content.Server.Decals.DecalSystem;

namespace Content.IntegrationTests.Tests.DeadSpace;

[TestFixture]
public sealed class DeadSpaceMappingEditorTest
{
    [Test]
    public async Task SetupCommandOpensEditor()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true
        });
        var client = pair.Client;
        var console = client.ResolveDependency<IClientConsoleHost>();
        var state = client.ResolveDependency<IStateManager>();

        await client.WaitPost(() => console.ExecuteCommand("mappingeditor"));
        await client.WaitRunTicks(5);

        Assert.That(state.CurrentState, Is.TypeOf<DeadSpaceMappingState>());

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EditorToggleAndFavoritesFile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true
        });
        var client = pair.Client;
        var state = client.ResolveDependency<IStateManager>();
        var resources = client.ResolveDependency<IResourceManager>();

        await client.WaitPost(() => state.RequestStateChange<DeadSpaceMappingState>());
        await client.WaitRunTicks(5);

        Assert.That(state.CurrentState, Is.TypeOf<DeadSpaceMappingState>());

        await client.WaitPost(() => state.RequestStateChange<GameplayState>());
        await client.WaitRunTicks(5);

        Assert.That(resources.UserData.Exists(new ResPath("/mapping_editor_favorites.yml")), Is.True);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChatIsAvailableAndTogglable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true
        });
        var client = pair.Client;
        var state = client.ResolveDependency<IStateManager>();
        var ui = client.ResolveDependency<IUserInterfaceManager>();

        await client.WaitPost(() => state.RequestStateChange<DeadSpaceMappingState>());
        await client.WaitRunTicks(5);

        var screen = ui.ActiveScreen as DeadSpaceMappingScreen;
        Assert.That(screen, Is.Not.Null);

        var chatController = ui.GetUIController<ChatUIController>();
        Assert.That(chatController.Chats, Does.Contain(screen!.ChatBox));

        Assert.Multiple(() =>
        {
            Assert.That(screen!.Chat.Visible, Is.True);
            Assert.That(screen.Chat.Size.X, Is.GreaterThan(0));
            Assert.That(screen.Chat.Size.Y, Is.GreaterThan(0));
            Assert.That(screen.ChatBox.Main, Is.True);
            Assert.That(screen.ChatToggle.Pressed, Is.True);
        });

        await client.WaitPost(() => screen!.SetChatVisible(false));

        Assert.Multiple(() =>
        {
            Assert.That(screen!.Chat.Visible, Is.False);
            Assert.That(screen.ChatToggle.Pressed, Is.False);
        });

        await client.WaitPost(() => screen!.SetChatVisible(true));

        Assert.That(screen!.Chat.Visible, Is.True);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExitButtonLeavesEditor()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true
        });
        var client = pair.Client;
        var state = client.ResolveDependency<IStateManager>();
        var ui = client.ResolveDependency<IUserInterfaceManager>();

        await client.WaitPost(() => state.RequestStateChange<DeadSpaceMappingState>());
        await client.WaitRunTicks(5);

        var screen = ui.ActiveScreen as DeadSpaceMappingScreen;
        Assert.That(screen, Is.Not.Null);

        await client.WaitPost(() => screen!.ExitEditor());
        await client.WaitRunTicks(5);

        Assert.That(state.CurrentState, Is.TypeOf<GameplayState>());

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DecalPickCopiesDecalSettings()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true
        });
        var server = pair.Server;
        var client = pair.Client;

        var map = await pair.CreateTestMap();
        var serverDecals = server.System<ServerDecalSystem>();
        var serverEntities = server.ResolveDependency<IEntityManager>();
        var decalCoords = new EntityCoordinates(map.Grid.Owner, 0.5f, 0.5f);

        await server.WaitPost(() =>
        {
            Assert.That(
                serverDecals.TryAddDecal("Dirt", decalCoords, out _, color: Color.Red, rotation: Angle.FromDegrees(90), zIndex: 3, cleanable: true),
                Is.True);
        });

        var found = DeadSpaceMappingDecalLookup.GetDecalsInRange(serverEntities, map.Grid.Owner, new Vector2(0.5f, 0.5f));

        Assert.That(found, Is.Not.Empty);

        var picked = found.First().Decal;
        Assert.That(picked.Id, Is.EqualTo("Dirt"));

        var state = client.ResolveDependency<IStateManager>();
        var ui = client.ResolveDependency<IUserInterfaceManager>();

        await client.WaitPost(() => state.RequestStateChange<DeadSpaceMappingState>());
        await client.WaitRunTicks(5);

        var screen = ui.ActiveScreen as DeadSpaceMappingScreen;
        Assert.That(screen, Is.Not.Null);

        var placement = client.System<ClientDecalPlacementSystem>();
        await client.WaitPost(() =>
        {
            placement.SetActive(true);
            screen!.SelectDecal(picked);
        });

        Assert.That(screen!.DecalColor, Is.EqualTo(Color.Red));

        var active = placement.GetActiveDecal();
        Assert.Multiple(() =>
        {
            Assert.That(active.Decal, Is.Not.Null);
            Assert.That(active.Decal!.ID, Is.EqualTo("Dirt"));
            Assert.That(active.Angle.Degrees, Is.EqualTo(90).Within(0.01));
            Assert.That(active.Color, Is.EqualTo(Color.Red));
        });

        await pair.CleanReturnAsync();
    }
}

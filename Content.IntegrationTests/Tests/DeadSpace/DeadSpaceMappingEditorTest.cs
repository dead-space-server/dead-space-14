using Content.Client.DeadSpace.Mapping;
using Content.Client.Gameplay;
using Robust.Client.Console;
using Robust.Client.State;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

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

        await client.WaitPost(() => console.ExecuteCommand("mappingclientsidesetup"));
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
}

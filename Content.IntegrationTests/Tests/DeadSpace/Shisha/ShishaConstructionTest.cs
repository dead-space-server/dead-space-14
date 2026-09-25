using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.Smokables.Systems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Shisha;

public sealed class ShishaConstructionTest : InteractionTest
{
    [Test]
    public async Task CraftWithRequiredMaterialsCreatesDockedHose()
    {
        await PlaceInHands("Bucket");
        var pipe = await SpawnEntity("GasPipeStraight", ToServer(PlayerCoords));
        await Server.WaitPost(() => Server.System<SharedTransformSystem>().Unanchor(pipe));
        await SpawnEntity((Cable, 3), ToServer(PlayerCoords));
        await CraftItem("Shisha", shouldSucceed: false);
        await FindEntity("Shisha", shouldSucceed: false);
        await SpawnEntity("Ashtray", ToServer(PlayerCoords));
        await CraftItem("Shisha");
        var shisha = await FindEntity("Shisha");
        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<ShishaComponent>(shisha);
            Assert.That(component.Hose, Is.Not.Null);
            Assert.That(Server.System<ShishaSystem>().IsDocked((shisha, component)), Is.True);
            Assert.That(SEntMan.GetComponent<ShishaHoseComponent>(component.Hose!.Value).Base, Is.EqualTo(shisha));
        });
        foreach (var ingredient in new[] { "Bucket", "GasPipeStraight", "Ashtray" })
        {
            await FindEntity(ingredient, shouldSucceed: false);
        }
        var stack = await FindEntity((Cable, 1));
        Assert.That(SEntMan.GetComponent<StackComponent>(stack).Count, Is.EqualTo(1));
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.RCD;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace;

public sealed class ArchitectConstructionEffectTest : InteractionTest
{
    [TestCase("ArchitectIntegratedArm", "FloorSteel", "EffectArchitectConstruct1")]
    [TestCase("ArchitectIntegratedArm", "WallSolid", "EffectArchitectConstruct3")]
    [TestCase("ArchitectIntegratedArm", "CircleTriggerKudzu", "EffectArchitectConstruct3")]
    [TestCase("ArchitectIntegratedArm", "CircleTriggerMeleeTentacle", "EffectArchitectConstruct3")]
    [TestCase("ArchitectIntegratedArm", "CircleTriggerRangedTentacle", "EffectArchitectConstruct3")]
    [TestCase("RCD", "FloorSteel", "EffectRCDConstruct0")]
    public async Task ConstructionUsesDeviceEffect(string device, string recipe, string effect)
    {
        var location = Transform.WithEntityId(
            new EntityCoordinates(SPlayer, new Vector2(0, 1)), MapData.Grid);
        await SetTile(PlatingRCD, SEntMan.GetNetCoordinates(location), MapData.Grid);
        var rcd = await PlaceInHands(device);

        await UseInHand();
        await RunTicks(3);
        await SendBui(RcdUiKey.Key, new RCDSystemMessage(recipe), rcd);
        await CloseBui(RcdUiKey.Key, rcd);
        await Interact(null, location, awaitDoAfters: false);

        await FindEntity(effect);
        await RunSeconds(5);
    }
}

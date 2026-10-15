#nullable enable
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Holosign;
using Content.Shared.PowerCell;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests.Holosign;

/// <summary>
/// Tests for different devices using <see cref="HolosignProjectorComponent"/>.
/// </summary>
[TestOf(typeof(HolosignProjectorComponent))]
public sealed class HolosignProjectorTest : MovementTest
{
    private static readonly EntProtoId HoloSignProjectorProtoId = "Holoprojector";

    /// <summary>
    /// Tests the janitors holosign projector.
    /// </summary>
    [Test]
    public async Task HoloSignTest()
    {
        var projector = await PlaceInHands(HoloSignProjectorProtoId);
        var projectorComp = Comp<HolosignProjectorComponent>(projector);
        var signProtoId = projectorComp.SignProto;

        // No holosigns before using the item.
        await AssertEntityLookup((WallPrototype, 2));

        var powerCellSystem = SEntMan.System<PowerCellSystem>();
        var initialUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(initialUses, Is.GreaterThan(0), "Holoprojector spawned without usable charges.");

        // Click on the tile next to the player.
        await Interact(null, TargetCoords);

        // We should have one charge less.
        var remainingUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(remainingUses, Is.EqualTo(initialUses - 1), "Holoprojector did not use the right amount of charge when used.");

        // We should have spawned exactly one holosign.
        await AssertEntityLookup(
            (signProtoId, 1),
            (WallPrototype, 2));

        // Try spawn more holosigns than we have charge.
        for (var i = 0; i < initialUses; i++)
        {
            await Interact(null, TargetCoords);
        }

        // The total should be the same as the initial charges.
        await AssertEntityLookup(
            (signProtoId, initialUses),
            (WallPrototype, 2));

        // We should have no charges left.
        remainingUses = powerCellSystem.GetRemainingUses(ToServer(projector), projectorComp.ChargeUse);
        Assert.That(remainingUses, Is.Zero, "Holoprojector did not use up all charges.");
    }
}

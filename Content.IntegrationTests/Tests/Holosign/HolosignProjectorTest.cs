#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.DeadSpace.Holosign;
using Content.Shared.Holosign;
using Content.Shared.PowerCell;
using Content.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Holosign;

/// <summary>
/// Tests for different devices using <see cref="HolosignProjectorComponent"/> and, DS14,
/// its replacement <see cref="LimitedHolosignProjectorComponent"/>.
/// </summary>
[TestOf(typeof(HolosignProjectorComponent))]
public sealed class HolosignProjectorTest : MovementTest
{
    private static readonly EntProtoId HoloBarrierProjectorProtoId = "HoloprojectorSecurity";
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

    // DS14-start: Climb coverage restored for the security holobarrier. The barrier is spawned by a
    // LimitedHolosignProjector, and its TimedDespawn is disabled in DS14, so instead of waiting for the
    // barrier to despawn we just verify that the placed barrier persists.
    /// <summary>
    /// Tests the security holo barrier projector and the barrier.
    /// </summary>
    [Test]
    public async Task HoloBarrierTest()
    {
        var projector = await PlaceInHands(HoloBarrierProjectorProtoId);
        var holoBarrierProtoId = Comp<LimitedHolosignProjectorComponent>(projector).SignProto;
        // No holobarriers before using the item.
        await AssertEntityLookup((WallPrototype, 2));

        // Click on the tile next to the player.
        await Interact(null, TargetCoords);

        // We should have spawned exactly one holobarrier.
        await AssertEntityLookup(
            (holoBarrierProtoId, 1),
            (WallPrototype, 2));
        Target = FromServer(await FindEntity(holoBarrierProtoId));

        // Check that the barrier is at the location we clicked at.
        AssertLocation(Target, TargetCoords);

        // Try moving past the barrier.
        Assert.That(Delta(), Is.GreaterThan(0.5), "Player was not located west of the holobarrier.");
        await Move(DirectionFlag.East, 0.5f);
        Assert.That(Delta(), Is.GreaterThan(0.5), "Player was able to walk through a holobarrier.");

        // Try to climb the barrier.
        await Interact(Target, TargetCoords, altInteract: true);

        // We should be able to move past the barrier now.
        await Move(DirectionFlag.East, 0.5f);
        Assert.That(Delta(), Is.LessThan(-0.5), "Player was not able to climb over a holobarrier.");

        // We should not be able to walk back without climbing again.
        await Move(DirectionFlag.West, 0.5f);
        Assert.That(Delta(), Is.LessThan(-0.5), "Player was able to walk through a holobarrier.");

        // The barrier no longer despawns, so it must still exist.
        AssertExists(Target);
    }
    // DS14-end

    // DS14-start: The limited holosign system spawns barriers on use and removes an existing barrier when the
    // projector is used on it again. Check that every entity prototype with a LimitedHolosignProjector component
    // can place barriers up to its configured MaxBarriers, that the limit is actually enforced, and that a placed
    // barrier is removed again without breaking the limit.
    [Test]
    public async Task LimitedHolosignProjectorsTest()
    {
        var projectors = ProtoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(p => p.HasComponent<LimitedHolosignProjectorComponent>(Factory))
            .Select(p => p.ID)
            .OrderBy(id => id)
            .ToList();

        Assert.That(projectors, Is.Not.Empty, "Expected at least one entity prototype with a LimitedHolosignProjector component.");

        foreach (var projectorProto in projectors)
        {
            await AssertLimitedHoloprojector(projectorProto);
        }
    }

    private async Task AssertLimitedHoloprojector(string projectorProto)
    {
        var projector = await PlaceInHands(projectorProto);
        var projectorComp = Comp<LimitedHolosignProjectorComponent>(projector);
        var maxBarriers = projectorComp.MaxBarriers;

        Assert.That(ProtoMan.HasIndex(projectorComp.SignProto), Is.True, $"{projectorProto}: SignProto {projectorComp.SignProto} does not exist.");
        Assert.That(maxBarriers, Is.GreaterThan(0), $"{projectorProto}: MaxBarriers must be greater than zero.");
        // The test can only interact with the eight tiles around the player, which allows a limit of up to 7
        // barriers plus one capped attempt.
        Assert.That(maxBarriers, Is.LessThan(8), $"{projectorProto}: MaxBarriers is too high for the test setup.");
        Assert.That(projectorComp.Barriers, Is.Empty, $"{projectorProto}: projector should start without barriers.");

        // Place barriers up to the configured limit and make sure each one is actually spawned.
        for (var i = 0; i < maxBarriers; i++)
        {
            await Interact(null, BarrierCoords(i));
            Assert.That(projectorComp.Barriers.Count, Is.EqualTo(i + 1), $"{projectorProto}: barrier #{(i + 1)} was not registered.");
        }

        foreach (var barrier in projectorComp.Barriers)
            Assert.That(SEntMan.EntityExists(barrier), Is.True, $"{projectorProto}: registered barrier is missing from the world.");

        // Creating one more barrier than the limit must be rejected by the system.
        await Interact(null, BarrierCoords(maxBarriers));
        Assert.That(projectorComp.Barriers.Count, Is.EqualTo(maxBarriers), $"{projectorProto}: the barrier limit was not enforced.");

        // Using the projector on a placed barrier must remove it instead of creating a new one.
        var removedBarrier = projectorComp.Barriers[0];
        Target = SEntMan.GetNetEntity(removedBarrier);
        await Interact(Target, TargetCoords);
        Assert.That(SEntMan.Deleted(removedBarrier), Is.True, $"{projectorProto}: removing a barrier did not delete it.");
        Assert.That(projectorComp.Barriers.Count, Is.EqualTo(maxBarriers - 1), $"{projectorProto}: removing a barrier did not update the barrier list.");
        Assert.That(projectorComp.Barriers, Does.Not.Contain(removedBarrier));

        // Clean up the remaining barriers so the next prototype starts from an empty grid.
        await Server.WaitPost(() =>
        {
            foreach (var barrier in projectorComp.Barriers)
            {
                if (SEntMan.EntityExists(barrier))
                    SEntMan.DeleteEntity(barrier);
            }
        });
        await RunTicks(2);
    }

    /// <summary>
    /// Coordinates one tile away from the player, all within interaction range. Up to eight distinct positions.
    /// </summary>
    private NetCoordinates BarrierCoords(int index)
    {
        var offset = index switch
        {
            0 => new Vector2(1f, 0f),
            1 => new Vector2(0f, 1f),
            2 => new Vector2(-1f, 0f),
            3 => new Vector2(0f, -1f),
            4 => new Vector2(1f, 1f),
            5 => new Vector2(-1f, 1f),
            6 => new Vector2(-1f, -1f),
            7 => new Vector2(1f, -1f),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        var coords = Transform.WithEntityId(MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f) + offset), MapData.MapUid);
        return SEntMan.GetNetCoordinates(coords);
    }
    // DS14-end
}
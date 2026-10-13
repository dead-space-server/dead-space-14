#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Maths;
using ClimbingComponent = Content.Shared.Climbing.Components.ClimbingComponent;
using ClimbSystem = Content.Shared.Climbing.Systems.ClimbSystem;

namespace Content.IntegrationTests.Tests.Climbing;

public sealed class ClimbingTest : MovementTest
{
    [Test]
    public async Task ClimbTableTest()
    {
        // Spawn a table to the right of the player.
        await SpawnTarget("Table");
        Assert.That(Delta(), Is.GreaterThan(0));

        // Player is not initially climbing anything.
        var comp = Comp<ClimbingComponent>(Player);
        Assert.Multiple(() =>
        {
            Assert.That(comp.IsClimbing, Is.False);
            Assert.That(comp.DisabledFixtureMasks, Has.Count.EqualTo(0));
        });

        // Attempt (and fail) to walk past the table.
        await Move(DirectionFlag.East, 1f);
        Assert.That(Delta(), Is.GreaterThan(0));

        // Try to start climbing
        var sys = SEntMan.System<ClimbSystem>();
        await Server.WaitPost(() => sys.TryClimb(SEntMan.GetEntity(Player), SEntMan.GetEntity(Player), SEntMan.GetEntity(Target.Value), out _));
        await AwaitDoAfters();

        // Player should now be climbing
        Assert.Multiple(() =>
        {
            Assert.That(comp.IsClimbing, Is.True);
            Assert.That(comp.DisabledFixtureMasks, Has.Count.GreaterThan(0));
        });

        // Can now walk over the table.
        await Move(DirectionFlag.East, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(Delta(), Is.LessThan(0));

            // After walking away from the table, player should have stopped climbing.
            Assert.That(comp.IsClimbing, Is.False);
            Assert.That(comp.DisabledFixtureMasks, Has.Count.EqualTo(0));
        });

        // Try to walk back to the other side (and fail).
        await Move(DirectionFlag.West, 1f);
        Assert.That(Delta(), Is.LessThan(0));

        // Start climbing
        await Server.WaitPost(() => sys.TryClimb(SEntMan.GetEntity(Player), SEntMan.GetEntity(Player), SEntMan.GetEntity(Target.Value), out _));
        await AwaitDoAfters();

        Assert.Multiple(() =>
        {
            Assert.That(comp.IsClimbing, Is.True);
            Assert.That(comp.DisabledFixtureMasks, Has.Count.GreaterThan(0));
        });

        // Walk past table and stop climbing again.
        await Move(DirectionFlag.West, 1f);
        Assert.Multiple(() =>
        {
            Assert.That(Delta(), Is.GreaterThan(0));
            Assert.That(comp.IsClimbing, Is.False);
            Assert.That(comp.DisabledFixtureMasks, Has.Count.EqualTo(0));
        });
    }

    //DS-14 start
    [Test]
    public async Task StandDuringClimbIsRefused()
    {
        await SpawnTarget("Table");
        var player = SEntMan.GetEntity(Player);
        var stun = SEntMan.System<SharedStunSystem>();

        var knockedDown = false;
        await Server.WaitPost(() => knockedDown = stun.TryKnockdown(player, TimeSpan.FromSeconds(30), autoStand: false, drop: false, force: true));
        await RunTicks(5);
        Assert.That(knockedDown, Is.True, "knockdown failed");
        Assert.That(Comp<StandingStateComponent>(Player).Standing, Is.False);

        var climb = SEntMan.System<ClimbSystem>();
        var table = SEntMan.GetEntity(Target.Value);
        var started = false;
        await Server.WaitPost(() => started = climb.TryClimb(player, player, table, out _));
        Assert.That(started, Is.True, "climb failed");
        Assert.That(Comp<ClimbingComponent>(Player).DoAfter, Is.Not.Null, "climb doafter missing");

        await Server.WaitPost(() =>
        {
            stun.SetKnockdownTime(player, TimeSpan.Zero);
            stun.TryStanding(player);
        });
        await RunTicks(90);
        Assert.That(Comp<StandingStateComponent>(Player).Standing, Is.False);
        AssertComp<KnockedDownComponent>(true, Player);

        await Server.WaitPost(() =>
        {
            SEntMan.GetComponent<ClimbingComponent>(player).IsClimbing = true;
            stun.SetKnockdownTime(player, TimeSpan.Zero);
            stun.TryStanding(player);
        });
        await RunTicks(90);
        Assert.That(Comp<StandingStateComponent>(Player).Standing, Is.False);
        AssertComp<KnockedDownComponent>(true, Player);
    }
    //DS-14 end
}

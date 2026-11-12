using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Power;

<<<<<<< ours
[TestFixture, TestOf(typeof(SharedPowerStateSystem))]
// DS14-start: use the pre-v288 PoolManager integration-test fixture API.
public sealed class PowerStatePrototypeTest
||||||| base
[TestFixture, TestOf(typeof(SharedPowerStateSystem))]
public sealed class PowerStatePrototypeTest
=======
[TestFixture, TestOf(typeof(PowerStateSystem))]
public sealed class PowerStatePrototypeTest : GameTest
>>>>>>> theirs
{
    /// <summary>
    /// Asserts that the power load is the same
    /// as the idle or working power draw from <see cref="PowerStateComponent"/>,
    /// depending on the current power state.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public async Task AssertApcPowerMatchesPowerState()
    {
        using (Assert.EnterMultipleScope())
        {
            var nonTestEntityPrototypes = SProtoMan.EnumeratePrototypes<EntityPrototype>()
                                                   .Where(p => !p.Abstract)
                                                   .Where(p => !Pair.IsTestPrototype(p));

            foreach (var prototype in nonTestEntityPrototypes)
            {
                if (!prototype.TryComp<PowerStateComponent>(out var powerStateComp, SEntMan.ComponentFactory))
                    continue;

                // LESSON LEARNED:
                // ENSURE THAT THE COMPONENT YOU ARE TRYING TO GET IS THE SERVER-SIDE VARIANT
                if (powerStateComp.EnsureApc && !prototype.HasComp<ApcPowerReceiverComponent>(SEntMan.ComponentFactory))
                {
                    Assert.Fail(
                        $"Entity prototype '{prototype.ID}' has a PowerStateComponent but is missing the required ApcPowerReceiverComponent.");
                }
                
                var expectedLoad = powerStateComp.IsTurnedOn
                    ? powerStateComp.WorkingPowerDraw
                    : powerStateComp.IdlePowerDraw;


<<<<<<< ours
                    // We have either an APC component and work with an APC network, or have a PowerConsumer
                    // component and work with a higher voltage network.
                    if (prototype.TryGetComponent<ApcPowerReceiverComponent>(out var powerReceiverComp, entMan.ComponentFactory))
                    {
                        Assert.That(powerReceiverComp.Load,
                            Is.EqualTo(expectedLoad),
                            $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and SharedApcPowerReceiverComponent.");
                    }
                    else
                    {
                        if (powerStateComp.EnsureApc)
                        {
                            Assert.Fail(
                                $"Entity prototype '{prototype.ID}' has a PowerStateComponent but is missing the required ApcPowerReceiverComponent.");
                        }

                        Assert.That(prototype.TryGetComponent<PowerConsumerComponent>(out var powerConsumer, entMan.ComponentFactory),
                            Is.True,
                            $"Entity prototype '{prototype.ID}' has a PowerStateComponent with EnsureApc disabled but is missing the required PowerConsumerComponent.");
                        Assert.That(powerConsumer!.DrawRate,
                            Is.EqualTo(expectedLoad),
                            $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and PowerConsumerComponent.");
                    }
||||||| base
                    Assert.That(powerReceiverComp.Load,
                        Is.EqualTo(expectedLoad),
                        $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and SharedApcPowerReceiverComponent.");
=======
                // we have either APC comp and work with APC network, or have PowerConsumer comp and work with higher voltage network
                prototype.TryComp(out ApcPowerReceiverComponent powerReceiverComp, SEntMan.ComponentFactory);
                if (powerReceiverComp != null)
                {
                    Assert.That(powerReceiverComp.Load,
                        Is.EqualTo(expectedLoad),
                        $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and SharedApcPowerReceiverComponent.");
>>>>>>> theirs
                }
                else
                {
                    Assert.That(prototype.TryComp<PowerConsumerComponent>(out var powerConsumer, SEntMan.ComponentFactory), Is.True);
                    Assert.That(powerConsumer!.DrawRate,
                        Is.EqualTo(expectedLoad),
                        $"Entity prototype '{prototype.ID}' has mismatched power draw between PowerStateComponent and SharedApcPowerReceiverComponent.");
                }
            }
        }
    }
}
// DS14-end

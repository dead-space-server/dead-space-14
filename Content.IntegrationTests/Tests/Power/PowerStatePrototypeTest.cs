using System.Linq;
using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Power;

[TestFixture, TestOf(typeof(SharedPowerStateSystem))]
// DS14-start: use the pre-v288 PoolManager integration-test fixture API.
public sealed class PowerStatePrototypeTest
{
    /// <summary>
    /// Asserts that the power load is the same
    /// as the idle or working power draw from <see cref="PowerStateComponent"/>,
    /// depending on the current power state.
    /// </summary>
    [Test]
    public async Task AssertApcPowerMatchesPowerState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(delegate
            {
                foreach (var prototype in protoMan.EnumeratePrototypes<EntityPrototype>()
                             .Where(p => !p.Abstract)
                             .Where(p => !pair.IsTestPrototype(p)))
                {
                    if (!prototype.TryGetComponent<PowerStateComponent>(out var powerStateComp, entMan.ComponentFactory))
                        continue;

                    // LESSON LEARNED:
                    // ENSURE THAT THE COMPONENT YOU ARE TRYING TO GET IS THE SERVER-SIDE VARIANT
                    var expectedLoad = powerStateComp.IsWorking
                        ? powerStateComp.WorkingPowerDraw
                        : powerStateComp.IdlePowerDraw;

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
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
// DS14-end

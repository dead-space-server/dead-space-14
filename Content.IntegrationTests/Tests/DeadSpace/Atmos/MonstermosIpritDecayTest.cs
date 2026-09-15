// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Atmos;

[TestFixture]
[TestOf(typeof(AtmosphereSystem))]
public sealed class MonstermosIpritDecayTest
{
    [Test]
    public async Task TransferPreservesIpritDeadlineAndClearsEmptyGiver()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        Assert.That(server.CfgMan.GetCVar(CCVars.MonstermosEqualization), Is.True);

        var atmosphere = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<AtmosphereSystem>();

        await server.WaitAssertion(() =>
        {
            var receiver = new GasMixture(Atmospherics.CellVolume);
            receiver.AdjustMoles(Gas.Iprit, 10f);
            receiver.EnsureIpritDecayDeadline(TimeSpan.FromSeconds(10));

            var giver = new GasMixture(Atmospherics.CellVolume);
            giver.AdjustMoles(Gas.Iprit, 10f);
            giver.EnsureIpritDecayDeadline(TimeSpan.FromSeconds(30));

            atmosphere.TransferGas(receiver, giver, giver.TotalMoles);

            Assert.Multiple(() =>
            {
                Assert.That(receiver.GetMoles(Gas.Iprit), Is.EqualTo(20f));
                Assert.That(receiver.IpritDecayDeadline, Is.EqualTo(TimeSpan.FromSeconds(20)));
                Assert.That(giver.GetMoles(Gas.Iprit), Is.Zero);
                Assert.That(giver.IpritDecayDeadline, Is.EqualTo(TimeSpan.Zero));
            });
        });

        await pair.CleanReturnAsync();
    }
}

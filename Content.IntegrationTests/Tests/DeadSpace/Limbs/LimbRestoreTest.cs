// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using Content.Server.DeadSpace.Surgery;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbRestoreTest
{
    [Test]
    public async Task RestoreClearsRibsAndTheRest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var mob = ents.SpawnEntity("MobHuman", map.GridCoords);
            var limbs = ents.GetComponent<LimbBodyComponent>(mob);
            var torso = limbs.Zones[(int) LimbZone.Torso];
            torso.Bone = LimbWoundRules.BreakBone + 10f;
            torso.Flesh = LimbWoundRules.StrainFlesh + 10f;
            torso.Cut = 30f;
            torso.Pierce = 20f;
            torso.Sever = 15f;
            torso.Bleed = 4f;
            torso.Embedded = true;
            torso.Scar = 2;
            torso.Splinted = true;
            limbs.Zones[(int) LimbZone.Torso] = torso;
            var liver = limbs.Organs[(int) LimbOrgan.Liver];
            liver.Ruptured = true;
            liver.Damage = 40f;
            limbs.Organs[(int) LimbOrgan.Liver] = liver;
            limbs.LungWhistle = true;

            var site = ents.EnsureComponent<SurgerySiteComponent>(mob);
            site.SpringBone = 12f;
            site.Zone = LimbZone.Torso;
            site.Layers.Add(new SurgeryLayerState { Integrity = 0.2f, Retracted = true });

            server.System<SurgeryLimbPort>().Restore(mob);

            var body = ents.GetComponent<LimbBodyComponent>(mob);
            var after = body.Zones[(int) LimbZone.Torso];
            Assert.That(after.Bone, Is.EqualTo(0f), "open ribs stay on the chest");
            Assert.That(after.Flesh, Is.EqualTo(0f));
            Assert.That(after.Cut, Is.EqualTo(0f));
            Assert.That(after.Pierce, Is.EqualTo(0f));
            Assert.That(after.Sever, Is.EqualTo(0f));
            Assert.That(after.Bleed, Is.EqualTo(0f));
            Assert.That(after.Embedded, Is.False);
            Assert.That(after.Scar, Is.EqualTo(0));
            Assert.That(after.Splinted, Is.False);
            Assert.That(body.LungWhistle, Is.False);
            Assert.That(body.Organs[(int) LimbOrgan.Liver].Ruptured, Is.False);

            var issues = new List<LimbScanIssue>();
            LimbScan.Build(body.Zones, body.Organs, out _, issues);
            Assert.That(issues.Exists(issue => issue.Kind is LimbIssueKind.RibOpen or LimbIssueKind.RibClosed or LimbIssueKind.RibCrack), Is.False);

            var sealedSite = ents.GetComponent<SurgerySiteComponent>(mob);
            Assert.That(sealedSite.SpringBone, Is.EqualTo(0f));
            Assert.That(sealedSite.Layers[0].Retracted, Is.False);
            Assert.That(sealedSite.Layers[0].Integrity, Is.EqualTo(1f));
        });

        await pair.CleanReturnAsync();
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class SurgeryMannequinTest
{
    [Test]
    public async Task AMannequinNeverDies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid dummy = default;

        await server.WaitPost(() =>
        {
            dummy = entities.SpawnEntity("MobSurgeryMannequin", new MapCoordinates(Vector2.Zero, map.MapId));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var mobs = entities.System<MobStateSystem>();
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(1000);
            entities.System<DamageableSystem>().TryChangeDamage(dummy, damage, true);
            Assert.That(mobs.IsDead(dummy), Is.False, "damage killed it");
            Assert.That(mobs.IsCritical(dummy), Is.True);

            entities.System<LimbWoundSystem>().KillByLimbs(dummy);
            Assert.That(mobs.IsDead(dummy), Is.False, "limbs killed it");

            Assert.That(entities.System<LimbWoundSystem>().Amputate(dummy, LimbZone.Head), Is.True);
            Assert.That(mobs.IsDead(dummy), Is.False, "the lost head killed it");
        });

        await pair.CleanReturnAsync();
    }
}

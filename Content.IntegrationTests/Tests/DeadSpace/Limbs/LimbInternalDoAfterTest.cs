// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed partial class LimbInternalDoAfterTest
{
    [Serializable, NetSerializable]
    private sealed partial class LimbSelfCareDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone()
        {
            return this;
        }
    }

    [Test]
    public async Task OrganFailureDoesNotBreakSelfCare()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ev = new LimbSelfCareDoAfterEvent();
        EntityUid mob = default;
        var before = 0f;

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            mob = ents.SpawnEntity("MobHuman", map.GridCoords);
            var limbs = server.System<LimbWoundSystem>();
            Assert.That(limbs.ApplyOrganEdit(mob, LimbOrgan.Liver, new LimbOrganEdit { SetRuptured = true, Ruptured = true }), Is.True);
            ents.EnsureComponent<LimbActiveComponent>(mob);
            before = ents.GetComponent<DamageableComponent>(mob).TotalDamage.Float();

            var args = new DoAfterArgs(ents, mob, TimeSpan.FromSeconds(30), ev, null)
            {
                Broadcast = true,
                BreakOnDamage = true,
                DamageThreshold = FixedPoint2.New(0.01),
            };
            Assert.That(server.System<SharedDoAfterSystem>().TryStartDoAfter(args), Is.True);
        });

        await server.WaitRunTicks(150);

        await server.WaitAssertion(() =>
        {
            var after = server.EntMan.GetComponent<DamageableComponent>(mob).TotalDamage.Float();
            Assert.That(after, Is.GreaterThan(before), "a ruptured liver must keep poisoning");
            Assert.That(ev.Cancelled, Is.False, "damage ticking from organs must not break a self-care do-after");
        });

        await pair.CleanReturnAsync();
    }
}

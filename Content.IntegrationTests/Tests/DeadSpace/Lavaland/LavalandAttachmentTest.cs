// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Collections.Generic;
using System.Linq;
using Content.Server.DeadSpace.Attachments;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavalandAttachmentTest
{
    [Test]
    public async Task CrusherTakesTrophiesAndUpgrades()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var slots = entities.System<ItemSlotsSystem>();
        var attachments = entities.System<AttachmentSystem>();
        var melee = entities.System<SharedMeleeWeaponSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var crusherId in new[] { "WeaponCrusher", "WeaponCrusherGlaive", "WeaponCrusherHammer", "WeaponCrusherHalberd" })
            {
                var crusher = entities.SpawnEntity(crusherId, MapCoordinates.Nullspace);
                Assert.That(entities.TryGetComponent<AttachmentHolderComponent>(crusher, out var holder), Is.True, crusherId);
                Assert.That(holder!.Slots, Does.Contain("trophy_slot_1"), $"{crusherId}: holder lost its slots");

                var trophy = entities.SpawnEntity("CrusherTrophyGoliathTentacle", MapCoordinates.Nullspace);
                Assert.That(slots.TryInsert(crusher, "trophy_slot_1", trophy, null), Is.True, $"{crusherId}: trophy did not go in");
                Assert.That(attachments.GetAttachments((crusher, holder)).Count(), Is.EqualTo(1), crusherId);

                // Two of the same trophy are not allowed.
                var second = entities.SpawnEntity("CrusherTrophyGoliathTentacle", MapCoordinates.Nullspace);
                Assert.That(slots.TryInsert(crusher, "trophy_slot_2", second, null), Is.False, $"{crusherId}: duplicate trophy");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpgradesChangeTheWeapon()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var slots = entities.System<ItemSlotsSystem>();
        var melee = entities.System<SharedMeleeWeaponSystem>();

        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var hammer = entities.SpawnEntity("WeaponCrusherHammer", MapCoordinates.Nullspace);
            entities.RemoveComponent<LavalandPressureDamageComponent>(hammer);

            // Sharpness blade: +7 slash.
            var before = melee.GetDamage(hammer, user).DamageDict.GetValueOrDefault("Slash").Float();
            var blade = entities.SpawnEntity("CrusherUpgradeBladeSharpness", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(hammer, "upgrade_slot_blade", blade, null), Is.True);
            var after = melee.GetDamage(hammer, user).DamageDict.GetValueOrDefault("Slash").Float();
            Assert.That(after - before, Is.EqualTo(7f).Within(0.01f));

            // The hammer has no shot of its own: the marker capsule adds it, taking it out removes it.
            Assert.That(entities.HasComponent<GunComponent>(hammer), Is.False);
            var marker = entities.SpawnEntity("CrusherUpgradeMainMarker", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(hammer, "upgrade_slot_main", marker, null), Is.True);
            Assert.That(entities.HasComponent<GunComponent>(hammer), Is.True);
            Assert.That(slots.TryEject(hammer, "upgrade_slot_main", null, out _), Is.True);
            Assert.That(entities.HasComponent<GunComponent>(hammer), Is.False);

            // A knife on a kinetic accelerator becomes a bayonet usable on right click.
            var pka = entities.SpawnEntity("WeaponProtoKineticAccelerator", MapCoordinates.Nullspace);
            entities.RemoveComponent<LavalandPressureDamageComponent>(pka);
            Assert.That(entities.HasComponent<MeleeWeaponComponent>(pka), Is.False);
            var knife = entities.SpawnEntity("SurvivalKnife", MapCoordinates.Nullspace);
            Assert.That(slots.TryInsert(pka, "bayonet_slot", knife, null), Is.True);
            Assert.That(entities.HasComponent<MeleeWeaponComponent>(pka), Is.True);
            Assert.That(entities.HasComponent<AltFireMeleeComponent>(pka), Is.True);
            Assert.That(melee.GetDamage(pka, user).GetTotal().Float(), Is.GreaterThan(0f));

            // The bayonet stabs exactly like the knife, with the demons' strike animation.
            var knifeMelee = entities.GetComponent<MeleeWeaponComponent>(knife);
            var pkaMelee = entities.GetComponent<MeleeWeaponComponent>(pka);
            Assert.That(pkaMelee.Damage.GetTotal(), Is.EqualTo(knifeMelee.Damage.GetTotal()));
            Assert.That(pkaMelee.AttackRate, Is.EqualTo(knifeMelee.AttackRate));
            Assert.That(pkaMelee.Range, Is.EqualTo(knifeMelee.Range));
            Assert.That(pkaMelee.Animation.Id, Is.EqualTo("WeaponArcClaw"));
        });
        await pair.CleanReturnAsync();
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Medical;
using Content.Server.Temperature.Systems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Temperature;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeadSpace.Temperature;

[TestFixture]
public sealed class CorpseBurningTest
{
    [Test]
    public async Task HealthScansWarnAboutCurrentHeatDamageWithSpeciesAndContainerThresholds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var map = server.System<SharedMapSystem>().CreateMap(out _);
            var analyzer = server.System<HealthAnalyzerSystem>();
            var temperature = server.System<TemperatureSystem>();
            try
            {
                Assert.That(analyzer.GetHealthAnalyzerUiState(null).Overheating, Is.False);
                foreach (var prototype in new[] { "MobHuman", "MobMoth", "MobReptilian" })
                {
                    var body = entities.SpawnEntity(prototype, new EntityCoordinates(map, Vector2.Zero));
                    var limits = entities.GetComponent<TemperatureDamageComponent>(body);
                    var threshold = limits.HeatDamageThreshold;

                    temperature.ForceChangeTemperature(body, threshold);
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.False, prototype);
                    temperature.ForceChangeTemperature(body, threshold + 10);
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.True,
                        "Scanning must warn before heat damage accumulates, including for living patients.");

                    limits.ParentHeatDamageThreshold = threshold + 20;
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.False,
                        "Container protection must use the same threshold as actual temperature damage.");
                    temperature.ForceChangeTemperature(body, threshold + 30);
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.True);

                    limits.ParentHeatDamageThreshold = null;
                    server.System<DamageableSystem>().SetDamage(body,
                        new DamageSpecifier { DamageDict = { ["Heat"] = 1100 } });
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.True,
                        "The warning must also be present when scanning a hot corpse.");
                    temperature.ForceChangeTemperature(body, threshold - 10);
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.False,
                        "Cooling must clear the warning even while old burns remain.");

                    temperature.ForceChangeTemperature(body, threshold + 30);
                    limits.HeatDamage = new DamageSpecifier();
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.False,
                        "Harmless high temperatures must not report inevitable destruction.");
                    entities.RemoveComponent<TemperatureComponent>(body);
                    Assert.That(analyzer.GetHealthAnalyzerUiState(body).Overheating, Is.False,
                        "Unknown body temperature must not trigger the warning.");
                }
            }
            finally
            {
                entities.DeleteEntity(map);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeatStagesSurviveHealingAndRevivalWhileExamineReflectsCurrentDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var map = server.System<SharedMapSystem>().CreateMap(out _);
            var body = entities.SpawnEntity("MobHuman", new EntityCoordinates(map, Vector2.Zero));
            var damage = server.System<DamageableSystem>();
            var burning = entities.GetComponent<CorpseBurningComponent>(body);
            var locale = server.ResolveDependency<ILocalizationManager>();

            string Examine()
            {
                var ev = new ExaminedEvent(new FormattedMessage(), body, body, true, false);
                entities.EventBus.RaiseLocalEvent(body, ev);
                return ev.GetTotalMessage().ToMarkup();
            }

            try
            {
                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Cold"] = 1200 } });
                Assert.That(burning.LastPopupStage, Is.Zero, "Cold damage must not cause charring.");
                Assert.That(Examine(), Does.Not.Contain(locale.GetString("corpse-burning-skin-examine-1")));

                var rotting = server.System<SharedRottingSystem>();
                rotting.ReduceAccumulator(body, TimeSpan.FromSeconds(-1));
                var fresh = FormattedMessage.FromMarkupOrThrow(locale.GetString("perishable-1",
                    ("target", Identity.Entity(body, entities)))).ToString();
                Assert.That(Examine(), Does.Contain(fresh));

                foreach (var heat in new[] { 1, CorpseBurningComponent.FirstStageDamage - 1 })
                {
                    damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = heat, ["Cold"] = 300 } });
                    Assert.That(burning.LastPopupStage, Is.Zero);
                    Assert.That(Examine(), Does.Contain(fresh), "Minor burns must not hide freshness.");
                }

                foreach (var (heat, stage) in new[] { (400, 1), (600, 2), (800, 3), (950, 4), (1100, 5) })
                {
                    damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = heat } });
                    Assert.That(burning.LastPopupStage, Is.EqualTo(stage));
                    Assert.That(Examine(), Does.Not.Contain(fresh), "Burnt bodies must not look fresh.");
                    Assert.That(Examine(), Does.Contain("color").And.Contain(
                        FormattedMessage.FromMarkupOrThrow(locale.GetString($"corpse-burning-skin-examine-{stage}")).ToString()));

                    damage.TryChangeDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = -100 } },
                        ignoreResistances: true);
                    Assert.That(burning.LastPopupStage, Is.EqualTo(stage), "Healing must not re-arm a popup.");
                    damage.TryChangeDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 100 } },
                        ignoreResistances: true);
                    Assert.That(burning.LastPopupStage, Is.EqualTo(stage), "Re-crossing a threshold preserves its latch.");
                }

                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 450 } });
                Assert.That(Examine(), Does.Contain(
                    FormattedMessage.FromMarkupOrThrow(locale.GetString("corpse-burning-skin-examine-1")).ToString()));
                Assert.That(burning.LastPopupStage, Is.EqualTo(5));

                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 1, ["Cold"] = 300 } });
                Assert.That(Examine(), Does.Not.Contain(fresh), "Remaining burns still hide the fresh description.");
                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Cold"] = 300 } });
                Assert.That(Examine(), Does.Contain(fresh), "Healing the burns restores the fresh description.");
                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 399, ["Cold"] = 300 } });
                Assert.That(Examine(), Does.Contain(fresh), "Old popup history must not make new minor burns hide freshness.");
                Assert.That(burning.LastPopupStage, Is.EqualTo(5), "Full healing must not reset popup history.");

                damage.SetAllDamage(body, 0);
                server.System<MobStateSystem>().ChangeMobState(body, MobState.Alive);
                Assert.That(burning.LastPopupStage, Is.EqualTo(5), "Revival must preserve the same body's popup history.");

                entities.RemoveComponent<MobThresholdsComponent>(body);
                damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 1100 } });
                Assert.That(Examine(), Does.Not.Contain(
                    FormattedMessage.FromMarkupOrThrow(locale.GetString("corpse-burning-skin-examine-5")).ToString()),
                    "A living character must not be described as a corpse.");
            }
            finally
            {
                entities.DeleteEntity(map);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpeciesUseTheirOwnBurnDescriptionsAndLargeHitsSkipToLatestStage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var map = server.System<SharedMapSystem>().CreateMap(out _);
            var damage = server.System<DamageableSystem>();
            var locale = server.ResolveDependency<ILocalizationManager>();
            try
            {
                foreach (var (prototype, type) in new[]
                         {
                             ("MobSlimePerson", "slime"), ("MobDiona", "plant"), ("MobArachnid", "chitin"),
                             ("MobMoth", "chitin"), ("MobIPC", "synthetic"), ("MobVox", "feathers"),
                             ("MobGingerbread", "dough"), ("MobXenomorph", "chitin")
                         })
                {
                    var body = entities.SpawnEntity(prototype, new EntityCoordinates(map, Vector2.Zero));
                    var burning = entities.GetComponent<CorpseBurningComponent>(body);
                    Assert.That(burning.MessageType, Is.EqualTo(type), prototype);
                    damage.SetDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 1100 } });
                    Assert.That(burning.LastPopupStage, Is.EqualTo(5), prototype);
                    Assert.That(entities.IsQueuedForDeletion(body), Is.False,
                        "The final warning must leave the body available to examine and treat.");
                    var ev = new ExaminedEvent(new FormattedMessage(), body, body, true, false);
                    entities.EventBus.RaiseLocalEvent(body, ev);
                    Assert.That(ev.GetTotalMessage().ToString(), Does.Contain(
                        FormattedMessage.FromMarkupOrThrow(locale.GetString($"corpse-burning-{type}-examine-5")).ToString()));

                    damage.TryChangeDamage(body, new DamageSpecifier { DamageDict = { ["Heat"] = 400 } },
                        ignoreResistances: true);
                    Assert.That(entities.IsQueuedForDeletion(body), Is.True,
                        "Burn stages must preserve the existing destruction threshold.");
                }
            }
            finally
            {
                entities.DeleteEntity(map);
            }
        });
        await pair.CleanReturnAsync();
    }
}

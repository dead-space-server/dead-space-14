using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Reactions;
using Content.Shared.DeadSpace.Sandevistan;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Kerensky;

[TestFixture]
public sealed class KerenskyShoutTest
{
    public sealed class SpeakListenerSystem : TestListenerSystem<EntitySpokeEvent>;

    [Test]
    public async Task NearbyCriticalTargetTriggersPhraseOnlyWhenActive()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var ent = server.EntMan;
        EntityUid host = default;
        EntityUid victim = default;
        EntityUid secondVictim = default;

        await server.WaitPost(() =>
        {
            host = ent.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
            victim = ent.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(1.5f, 0.5f), map.MapId));
            secondVictim = ent.SpawnEntity("MobHuman", new MapCoordinates(new Vector2(2.5f, 0.5f), map.MapId));
            var reaction = ent.EnsureComponent<NearbyCriticalShoutComponent>(host);
            reaction.Messages = Enumerable.Range(1, 5)
                .Select(index => (LocId) $"kerensky-nearby-critical-{index}")
                .ToList();
            ent.EnsureComponent<TestListenerComponent>(host);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() => server.System<MobStateSystem>().ChangeMobState(victim, MobState.Critical));

        await server.WaitAssertion(() =>
            Assert.That(server.System<SpeakListenerSystem>().GetEvents(host), Is.Empty));

        await server.WaitPost(() =>
        {
            server.System<MobStateSystem>().ChangeMobState(victim, MobState.Alive);
            var implant = ent.SpawnEntity("KerenskyImplant", new MapCoordinates(new Vector2(0.5f, 0.5f), map.MapId));
            ent.GetComponent<LimbImplantComponent>(implant).Host = host;
            var active = ent.EnsureComponent<ActiveSandevistanComponent>(host);
            active.SourceImplant = implant;
            var endTime = server.Timing.CurTime + TimeSpan.FromSeconds(30);
            active.EndTime = endTime;
            active.SoftcapTime = endTime;
            active.NextOverloadTime = endTime;
            active.NextEndWarningTime = endTime;
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedSandevistanSystem>().IsActive(host), Is.True));

        await server.WaitPost(() => server.System<MobStateSystem>().ChangeMobState(victim, MobState.Critical));
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var spoken = server.System<SpeakListenerSystem>().GetEvents(host).Single();
            var phrases = Enumerable.Range(1, 5)
                .Select(index => Loc.GetString($"kerensky-nearby-critical-{index}"))
                .ToArray();
            Assert.That(phrases, Does.Contain(spoken.Message));
        });

        await server.WaitPost(() =>
        {
            server.System<MobStateSystem>().ChangeMobState(victim, MobState.Alive);
            server.System<MobStateSystem>().ChangeMobState(secondVictim, MobState.Critical);
        });

        await server.WaitAssertion(() =>
            Assert.That(server.System<SpeakListenerSystem>().GetEvents(host).Count(), Is.EqualTo(1)));
        await pair.CleanReturnAsync();
    }
}

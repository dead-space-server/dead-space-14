// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Client.Effects;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Screech;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.VoiceMask;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Changeling;

[TestFixture]
[TestOf(typeof(ScreechShockWaveSystem))]
public sealed class ScreechActionTest
{
    private static readonly EntProtoId[] ChangedActionPrototypes =
    [
        "ActionRetractableItemArmBlade",
        "ActionChangelingBiodegrade",
        "ActionChangelingDevour",
        "ActionChangelingTransform",
        "ActionChangelingStasis",
        "ActionChangelingStasisWeak",
        "ActionChangelingStore",
        "ActionChangelingVoiceMimic",
        "ActionChangelingStingDna",
        "ActionChangelingStingBlind",
        "ActionChangelingStingCryogenic",
        "ActionChangelingStingLead",
        "ActionChangelingStingHallucinogenic",
        "ActionChangelingStingMute",
        "ActionChangelingLastResort",
        "ActionChangelingTakeOverCorpse",
        "ActionChangelingNightVision",
        "ActionChangelingScreech",
        "ActionChangelingFakeMindshieldToggle",
    ];

    [Test]
    public async Task ChangedActionsStartAndLoadIconsOnClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var sprites = client.System<SpriteSystem>();

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var prototype in ChangedActionPrototypes)
                {
                    var action = client.EntMan.Spawn(prototype);
                    var component = client.EntMan.GetComponent<ActionComponent>(action);

                    Assert.That(component.Icon, Is.Not.Null, $"{prototype} has no action icon.");
                    Assert.DoesNotThrow(() => sprites.Frame0(component.Icon!), $"{prototype} has an invalid action icon.");

                    if (component.IconOn is { } iconOn)
                        Assert.DoesNotThrow(() => sprites.Frame0(iconOn), $"{prototype} has an invalid toggled action icon.");

                    client.EntMan.DeleteEntity(action);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScreechEffectStartsOnClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var effect = client.EntMan.Spawn("EffectScreech");
            Assert.That(client.EntMan.HasComponent<ScreechShockWaveComponent>(effect));
            client.EntMan.DeleteEntity(effect);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VoiceMimicPurchaseGrantsUsableAction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var changeling = server.EntMan.SpawnEntity("MobLing", testMap.GridCoords);
            var store = server.EntMan.GetComponent<StoreComponent>(changeling);
            var purchase = new StoreBuyListingMessage("ChangelingVoiceMimic") { Actor = changeling };

            server.EntMan.EventBus.RaiseLocalEvent(changeling, purchase);

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<VoiceMaskComponent>(changeling),
                    "Buying voice mimicry did not add VoiceMaskComponent.");
                Assert.That(server.System<SharedUserInterfaceSystem>().HasUi(changeling, VoiceMaskUIKey.Key),
                    "Voice mimicry has no bound UI.");
                Assert.That(server.System<SharedActionsSystem>()
                        .GetActions(changeling)
                        .Any(action => server.EntMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == "ActionChangelingVoiceMimic"),
                    "Buying voice mimicry did not grant its action.");
                Assert.That(store.Balance["ChangelingDNA"].Int(), Is.EqualTo(50),
                    "Voice mimicry purchase did not use the expected DNA cost.");
            });

            server.EntMan.DeleteEntity(changeling);
        });

        await pair.CleanReturnAsync();
    }
}

using System.Linq;
using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Crayon;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Crayon;

[TestFixture]
public sealed class CrayonOpacityTest
{
    [Test]
    public async Task DrawnMarkUsesTheCrayonOpacity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var user = ents.SpawnEntity("MobHuman", map.GridCoords);
            var crayon = ents.SpawnEntity("CrayonRed", map.GridCoords);
            var component = ents.GetComponent<CrayonComponent>(crayon);
            ents.EventBus.RaiseLocalEvent(crayon, new CrayonSelectMessage("0"));
            ents.EventBus.RaiseLocalEvent(crayon, new CrayonOpacityMessage(0.4f));

            var click = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            var ev = new AfterInteractEvent(user, crayon, null, click, true);
            ents.EventBus.RaiseLocalEvent(crayon, ev);

            Assert.That(component.Opacity, Is.EqualTo(0.4f).Within(0.001f));
            var found = server.System<DecalSystem>().GetDecalsInRange(map.Grid.Owner, new Vector2(0.5f, 0.5f), 0.25f);
            Assert.That(found, Has.Count.EqualTo(1));
            var color = found.Single().Decal.Color;
            Assert.That(color, Is.Not.Null);
            Assert.That(color!.Value.R, Is.GreaterThan(0.9f));
            Assert.That(color.Value.A, Is.EqualTo(0.4f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OpacityBelowTheFloorStillDrawsAtTheFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var user = ents.SpawnEntity("MobHuman", map.GridCoords);
            var crayon = ents.SpawnEntity("CrayonRed", map.GridCoords);
            var component = ents.GetComponent<CrayonComponent>(crayon);
            ents.EventBus.RaiseLocalEvent(crayon, new CrayonSelectMessage("0"));
            ents.EventBus.RaiseLocalEvent(crayon, new CrayonOpacityMessage(0.01f));

            var click = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
            ents.EventBus.RaiseLocalEvent(crayon, new AfterInteractEvent(user, crayon, null, click, true));

            Assert.That(component.Opacity, Is.EqualTo(component.MinOpacity).Within(0.001f));
            var found = server.System<DecalSystem>().GetDecalsInRange(map.Grid.Owner, new Vector2(0.5f, 0.5f), 0.25f);
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found.Single().Decal.Color!.Value.A, Is.EqualTo(component.MinOpacity).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}

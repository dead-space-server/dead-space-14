using Content.IntegrationTests.Fixtures;
using System.Numerics;
using Content.Server.Cloning;
using Content.Shared.Cloning;
using Content.Shared.Sprite;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Cloning;

public sealed class CloningSettingsPrototypeTest : GameTest
{
    /// <summary>
    /// Checks that the components named in every <see cref="CloningSettingsPrototype"/> are valid components known to the server.
    /// This is used instead of <see cref="ComponentNameSerializer"/> because we only care if the components are registered with the server,
    /// and instead of a <see cref="ComponentRegistry"/> because we only need component names.
    /// </summary>
    [Test]
    public async Task ValidatePrototypes()
    {
        var pair = Pair;
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var compFactory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                var protos = protoMan.EnumeratePrototypes<CloningSettingsPrototype>();
                foreach (var proto in protos)
                {
                    foreach (var compName in proto.Components)
                    {
                        Assert.That(compFactory.TryGetRegistration(compName, out _),
                            $"Failed to find a component named {compName} for {nameof(CloningSettingsPrototype)} \"{proto.ID}\""
                        );
                    }

                    foreach (var eventCompName in proto.EventComponents)
                    {
                        Assert.That(compFactory.TryGetRegistration(eventCompName, out _),
                            $"Failed to find a component named {eventCompName} for {nameof(CloningSettingsPrototype)} \"{proto.ID}\""
                        );
                    }
                }
            });
        });
    }

    [Test]
    public async Task CloneCopiesScaleVisuals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entityManager = server.EntMan;
        var cloning = server.System<CloningSystem>();
        var scaleVisuals = server.System<SharedScaleVisualsSystem>();

        var expectedScale = new Vector2(0.9f, 0.9f);

        await server.WaitAssertion(() =>
        {
            var original = entityManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            scaleVisuals.SetSpriteScale(original, expectedScale);

            Assert.That(cloning.TryCloning(original, null, "BaseClone", out var clone), Is.True);
            Assert.That(clone, Is.Not.Null);
            Assert.That(entityManager.HasComponent<ScaleVisualsComponent>(clone!.Value), Is.True);
            Assert.That(scaleVisuals.GetSpriteScale(clone.Value), Is.EqualTo(expectedScale));
        });

        await pair.CleanReturnAsync();
    }
}

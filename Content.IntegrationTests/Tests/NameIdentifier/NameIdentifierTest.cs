using System.Collections.Generic;
using System.Linq;
using Content.Server.NameIdentifier;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.NameIdentifier;

[TestOf(typeof(NameIdentifierSystem))]
[TestFixture]
public sealed class NameIdentifierTest
{
    private const string MaxIds = "5";
    private const int MaxIdsInt = 5;

    private const string NameTest = "NameTest";
    private const string NumberTestGroup = "NumberTestGroup";
    private const string PrefixTestGroup = "PrefixTestGroup";
    private const string PrefixTest = "PrefixTest";
    private const string ParenTestEnt = "ParenTestEnt";
    private const string LocTestEnt = "LocTestEnt";

    [TestPrototypes]
    private const string Prototypes =
        $"""
        - type: nameIdentifierGroup
          id: {NumberTestGroup}
          minValue: 1
          maxValue: {MaxIds}

        - type: entity
          name: {NameTest}
          id: {NameTest}
          components:
          - type: NameIdentifier
            group: {NumberTestGroup}

        - type: nameIdentifierGroup
          parent: {NumberTestGroup}
          id: {PrefixTestGroup}
          prefix: true

        - type: entity
          name: {PrefixTest}
          id: {PrefixTest}
          components:
          - type: NameIdentifier
            group: {PrefixTestGroup}

        - type: entity
          name: {ParenTestEnt}
          id: {ParenTestEnt}
          components:
          - type: NameIdentifier
            group: GenericNumber

        - type: localizedDataset
          id: NameIdentifierTest
          values:
            prefix: name-identifier-test-
            count: 1

        - type: nameIdentifierGroup
          id: Localized
          identifierDataset: NameIdentifierTest

        - type: entity
          name: {LocTestEnt}
          id: {LocTestEnt}
          components:
          - type: NameIdentifier
            group: Localized
        """;

    // DS14-start: Adapt the upstream fixture-based test to this branch's pooled integration-test harness.
    [Test]
    public async Task NameIdentifierBehavior()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entManager = server.EntMan;
        var nameIdentifier = server.System<NameIdentifierSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(nameIdentifier.CurrentIds.ContainsKey(NumberTestGroup),
                    "Failed to load test prototypes.");
                Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                    Has.Count.EqualTo(MaxIdsInt),
                    "Failed to load test prototypes.");
            });

            var single = entManager.SpawnEntity(NameTest, testMap.GridCoords);
            Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                Has.Count.EqualTo(MaxIdsInt - 1),
                "CurrentIds did not decrease.");
            entManager.DeleteEntity(single);
            Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                Has.Count.EqualTo(MaxIdsInt),
                "CurrentIds did not return to max.");

            var original = nameIdentifier.CurrentIds[NumberTestGroup].ToArray();
            var entities = new List<EntityUid>();
            for (var i = 0; i <= MaxIdsInt; i++)
                entities.Add(entManager.SpawnEntity(NameTest, testMap.GridCoords));

            Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                Has.Count.EqualTo(0),
                "CurrentIds failed to empty.");
            Assert.That(entManager.GetComponent<MetaDataComponent>(entities[^1]).EntityName,
                Is.EqualTo(NameTest),
                "Created an invalid name after exhausting the pool.");

            foreach (var entity in entities)
                entManager.DeleteEntity(entity);

            Assert.Multiple(() =>
            {
                Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                    Has.Count.EqualTo(MaxIdsInt),
                    "CurrentIds failed to refill.");
                Assert.That(nameIdentifier.CurrentIds[NumberTestGroup],
                    Is.EquivalentTo(original),
                    "The original IDs were not returned to the pool.");
            });

            entities.Clear();
            for (var i = 0; i < MaxIdsInt; i++)
                entities.Add(entManager.SpawnEntity(NameTest, testMap.GridCoords));

            var names = entities
                .Select(entity => entManager.GetComponent<MetaDataComponent>(entity).EntityName)
                .ToList();
            Assert.Multiple(() =>
            {
                Assert.That(names, Is.Unique, "A duplicate name was generated.");
                Assert.That(names, Has.All.Contain(NameTest), "The base name was not preserved.");
                Assert.That(names, Has.All.Match(@"\d+$"), "Created an invalid name.");
            });

            var parenthesized = entManager.SpawnEntity(ParenTestEnt, testMap.GridCoords);
            Assert.That(entManager.GetComponent<MetaDataComponent>(parenthesized).EntityName,
                Does.Match($@"{ParenTestEnt} \(\d+\)$"),
                "Did not create a valid parenthesis wrapped name.");

            var localized = entManager.SpawnEntity(LocTestEnt, testMap.GridCoords);
            Assert.That(entManager.GetComponent<MetaDataComponent>(localized).EntityName,
                Is.EqualTo($"{LocTestEnt} TestValue"),
                "Did not create a valid localized name.");

            var prefixed = entManager.SpawnEntity(PrefixTest, testMap.GridCoords);
            Assert.That(entManager.GetComponent<MetaDataComponent>(prefixed).EntityName,
                Does.Match(@"^\d+"),
                "Did not create a valid name with a prefix.");
        });

        await pair.CleanReturnAsync();
    }
    // DS14-end
}

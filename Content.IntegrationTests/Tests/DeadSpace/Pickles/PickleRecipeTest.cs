// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.Components;
using Content.Shared.DeadSpace.Pickles;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Pickles;

[TestFixture]
public sealed class PickleRecipeTest
{
    private static readonly EntProtoId BarrelWood = "PickleBarrelWood";
    private static readonly EntProtoId FoodJar = "FoodPickleJar";

    private static readonly EntProtoId Cucumber = "FoodCucumber";
    private static readonly EntProtoId CucumberSeeds = "CucumberSeeds";
    private static readonly EntProtoId SeedCrate = "CratePickleSeeds";
    private static readonly EntProtoId Cabbage = "FoodCabbage";
    private static readonly EntProtoId Grape = "FoodGrape";
    private static readonly EntProtoId Berries = "FoodBerries";

    private static readonly ProtoId<ReagentPrototype> VinegarBrine = "PickleVinegarBrine";
    private static readonly ProtoId<ReagentPrototype> SaltBrine = "PickleSaltBrine";
    private static readonly ProtoId<ReagentPrototype> Bacteria = "PickleBacteria";
    private static readonly ProtoId<ReagentPrototype> PickleWine = "PickleWine";

    [Test]
    public async Task OverlayPrototypesAndRecipesAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        var loc = pair.Server.ResolveDependency<ILocalizationManager>();

        await pair.Server.WaitAssertion(() =>
        {
            foreach (var id in new EntProtoId[]
                     {
                         BarrelWood,
                         FoodJar,
                         Cucumber,
                         CucumberSeeds,
                         SeedCrate,
                     })
            {
                Assert.That(proto.HasIndex(id), Is.True, $"Missing entity {id}");
            }

            Assert.That(proto.HasIndex(new EntProtoId("PickleBarrelPlastic")), Is.False,
                "Plastic curing barrel was removed");
            Assert.That(proto.HasIndex(new EntProtoId("PickleBarrelPlasticFrame")), Is.False,
                "Plastic curing barrel frame was removed");

            Assert.That(proto.HasIndex(VinegarBrine), Is.True);
            Assert.That(proto.HasIndex(SaltBrine), Is.True);
            Assert.That(proto.HasIndex(Bacteria), Is.True);
            Assert.That(proto.HasIndex(PickleWine), Is.True);
            Assert.That(proto.HasIndex(new ProtoId<ReagentPrototype>("PickleCider")), Is.True);

            AssertBrineSobersAndCleansToxins(proto.Index(VinegarBrine));
            AssertBrineSobersAndCleansToxins(proto.Index(SaltBrine));

            var recipes = proto.EnumeratePrototypes<PickleRecipePrototype>().ToList();
            Assert.That(recipes, Is.Not.Empty);

            foreach (var recipe in recipes)
            {
                Assert.That(proto.HasIndex(recipe.Produce),
                    Is.True,
                    $"{recipe.ID} produce {recipe.Produce} does not exist");
                Assert.That(proto.HasIndex(recipe.RequiredReagent),
                    Is.True,
                    $"{recipe.ID} reagent {recipe.RequiredReagent} does not exist");
                Assert.That(recipe.DurationSeconds, Is.GreaterThan(0).And.LessThanOrEqualTo(90),
                    $"{recipe.ID} duration should stay round-friendly");
                Assert.That(recipe.ProducePerJar, Is.GreaterThan(0));

                if (recipe.OutputDrinkReagent is { } drink)
                {
                    Assert.That(recipe.Method, Is.EqualTo(PickleMethod.Alcohol), recipe.ID);
                    Assert.That(proto.HasIndex(drink), Is.True, $"{recipe.ID} drink {drink} does not exist");
                }

                var produce = proto.Index(recipe.Produce);
                Assert.That(produce.TryGetComponent<ProduceComponent>(out _, factory),
                    Is.True,
                    $"{recipe.ID} produce {recipe.Produce} should be harvestable food");
            }

            Assert.That(Find(recipes, Cucumber, PickleMethod.Vinegar)?.MinSugar, Is.GreaterThan(0));
            Assert.That(Find(recipes, Cucumber, PickleMethod.Salt)?.MinSugar ?? 0, Is.EqualTo(0));
            Assert.That(Find(recipes, Cabbage, PickleMethod.Salt)?.LowBrine, Is.True);

            // Explicit start-fermentation hint strings must resolve (Fluent args optional).
            foreach (var id in new[]
                     {
                         "pickle-barrel-no-liquid",
                         "pickle-barrel-no-sugar",
                         "pickle-barrel-not-enough",
                         "pickle-barrel-not-enough-generic",
                         "pickle-barrel-need-vinegar",
                         "pickle-barrel-need-salt",
                         "pickle-barrel-bad-recipe",
                     })
            {
                Assert.That(loc.TryGetString(id, out var text), Is.True, $"missing locale {id}");
                Assert.That(text, Is.Not.Null.And.Not.Empty, id);
            }
            Assert.That(Find(recipes, Cabbage, PickleMethod.Vinegar), Is.Not.Null);
            Assert.That(Find(recipes, Grape, PickleMethod.Alcohol)?.OutputDrinkReagent, Is.EqualTo(PickleWine));
            Assert.That(Find(recipes, Berries, PickleMethod.Alcohol)?.OutputDrinkReagent, Is.EqualTo(PickleWine));
            Assert.That(Find(recipes, new EntProtoId("FoodApple"), PickleMethod.Alcohol)?.OutputDrinkReagent,
                Is.EqualTo(new ProtoId<ReagentPrototype>("PickleCider")));
            Assert.That(Find(recipes, new EntProtoId("FoodMushroom"), PickleMethod.Salt), Is.Not.Null);
            Assert.That(Find(recipes, new EntProtoId("FoodCactus"), PickleMethod.Salt), Is.Not.Null);
            Assert.That(recipes.Any(r => r.Produce.Id is "FoodPear"), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WoodenBarrelIsNotAnchorableAfterMapInit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();

        await pair.Server.WaitAssertion(() =>
        {
            var ent = pair.Server.EntMan.SpawnEntity(BarrelWood, map.GridCoords);
            Assert.That(pair.Server.EntMan.HasComponent<AnchorableComponent>(ent), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static PickleRecipePrototype Find(
        IEnumerable<PickleRecipePrototype> recipes,
        EntProtoId produce,
        PickleMethod method)
    {
        return recipes.FirstOrDefault(recipe => recipe.Produce == produce && recipe.Method == method);
    }

    private static void AssertBrineSobersAndCleansToxins(ReagentPrototype brine)
    {
        Assert.That(brine.Metabolisms, Is.Not.Null, brine.ID);
        var effects = brine.Metabolisms!.Values.SelectMany(entry => entry.Effects).ToArray();

        Assert.That(effects.OfType<AdjustReagent>().Any(effect =>
                effect.Reagent == "Ethanol" && effect.Amount < FixedPoint2.Zero),
            Is.True,
            $"{brine.ID} should reduce Ethanol when metabolized");

        Assert.That(effects.OfType<HealthChange>().Any(effect =>
                effect.Damage.DamageDict.TryGetValue("Poison", out var poison) && poison < FixedPoint2.Zero),
            Is.True,
            $"{brine.ID} should heal Poison damage");
    }
}

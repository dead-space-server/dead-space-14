// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Cargo.Components;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Sprite;
using Content.Shared.Storage;

namespace Content.Server.DeadSpace.Husbandry.Growth;

/// <summary>
/// Applies what a growth stage means to an animal: its name, size, health, price, how much meat it gives,
/// when it gets hungry, how much it eats and how much manure it leaves.
/// </summary>
public sealed class GrowthEffectsSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrowthComponent, GrowthStageChangedEvent>(OnStageChanged);
    }

    private void OnStageChanged(Entity<GrowthComponent> ent, ref GrowthStageChangedEvent args)
    {
        var stage = args.Stage;

        var sex = TryComp<AnimalSexComponent>(ent, out var sexComp) ? sexComp.Sex : AnimalSexKind.Female;
        if (stage.Names.TryGetValue(sex, out var name))
            _meta.SetEntityName(ent, Loc.GetString(name));

        _scale.SetSpriteScale(ent, stage.Scale);

        if (stage.MaxHealth > 0f)
            _thresholds.SetMobStateThreshold(ent, FixedPoint2.New(stage.MaxHealth), MobState.Dead);

        // A stage that does not say how much meat or what it is worth leaves what the prototype has.
        if (stage.MeatCount > 0 && TryComp<ButcherableComponent>(ent, out var butcherable))
        {
            butcherable.SpawnedEntities.Clear();
            butcherable.SpawnedEntities.Add(new EntitySpawnEntry { PrototypeId = ent.Comp.Meat, Amount = stage.MeatCount });
        }

        if (stage.Price > 0 && TryComp<MobPriceComponent>(ent, out var price))
            price.Price = stage.Price;

        if (TryComp<AnimalNeedsComponent>(ent, out var needs))
            _needs.SetSatietyProfile((ent.Owner, needs), stage.SatietySeekBelow, stage.SatietyDecayPerMinute);
    }
}

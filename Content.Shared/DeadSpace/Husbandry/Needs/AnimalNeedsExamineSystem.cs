// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// Tells on examine how hungry and thirsty an animal is.
/// </summary>
public sealed class AnimalNeedsExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalNeedsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<AnimalNeedsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var identity = Identity.Entity(ent, EntityManager);
        args.PushMarkup(Loc.GetString($"animal-needs-satiety-{ent.Comp.SatietyLevel.ToString().ToLowerInvariant()}",
            ("entity", identity)));
        args.PushMarkup(Loc.GetString($"animal-needs-hydration-{ent.Comp.HydrationLevel.ToString().ToLowerInvariant()}",
            ("entity", identity)));
    }
}

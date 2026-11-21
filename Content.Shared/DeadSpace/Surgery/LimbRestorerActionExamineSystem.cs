// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions.Components;
using Content.Shared.Examine;

namespace Content.Shared.DeadSpace.Surgery;

/// <summary>
/// Shows the current and maximum charges of a limb restorer implant in its action tooltip.
/// </summary>
public sealed class LimbRestorerActionExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionComponent, ExaminedEvent>(OnActionExamined);
    }

    private void OnActionExamined(Entity<ActionComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Container is not { } container || !TryComp<LimbRestorerComponent>(container, out var restorer))
            return;

        if (restorer.MaxCharges <= 0)
            return;

        args.PushMarkup(Loc.GetString("limb-restorer-charges-max",
            ("count", restorer.Charges),
            ("max", restorer.MaxCharges)));
    }
}

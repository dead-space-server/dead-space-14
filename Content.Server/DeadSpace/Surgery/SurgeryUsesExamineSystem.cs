// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Examine;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryUsesExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgerySkinRollComponent, ExaminedEvent>((_, comp, args) => Show(args, comp.Uses));
        SubscribeLocalEvent<SurgerySlimeGelComponent, ExaminedEvent>((_, comp, args) => Show(args, comp.Uses));
        SubscribeLocalEvent<SurgeryDionaSapComponent, ExaminedEvent>((_, comp, args) => Show(args, comp.Uses));
        SubscribeLocalEvent<SurgeryBoneGelComponent, ExaminedEvent>((_, comp, args) => Show(args, comp.Uses));
    }

    private void Show(ExaminedEvent args, int uses)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("surgery-uses-left", ("count", uses)));
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Verbs;

namespace Content.Shared.DeadSpace.Flashbang;

public sealed partial class FlashbangProtectionSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlashbangProtectionComponent, GetVerbsEvent<ExamineVerb>>(OnDetailedExamine);
    }

    private void OnDetailedExamine(EntityUid ent, FlashbangProtectionComponent component, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!HasComp<ClothingComponent>(ent))
            return;

        var iconTexture = "/Textures/Interface/VerbIcons/dot.svg.192dpi.png";

        _examine.AddHoverExamineVerb(args,
            component,
            Loc.GetString("flashbang-protection-examinable-verb-text"),
            Loc.GetString("flashbang-protection-examinable-verb-text-message", ("reduction", component.Reduction)),
            iconTexture
        );
    }
}

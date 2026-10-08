// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.DeadSpace.Skills.Components;
using Content.Shared.DeadSpace.Skills.Prototypes;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Construction;

[UsedImplicitly]
[DataDefinition]
public sealed partial class SkillCondition : IConstructionCondition
{
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Skill;

    [DataField]
    public LocId Popup = "construction-skill-required";

    [DataField]
    public SpriteSpecifier? GuideIcon;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var hasSkill = entityManager.TryGetComponent(user, out SkillComponent? skills)
                       && skills.Skills.TryGetValue(Skill, out var progress)
                       && progress >= 1f;

        if (!hasSkill)
            entityManager.System<SharedPopupSystem>().PopupEntity(Loc.GetString(Popup), user, user);

        return hasSkill;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = Popup,
            Icon = GuideIcon,
        };
    }
}

using Content.Server.DeadSpace.Armutant.Base.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Antag;
using Content.Server.Roles;
using Content.Shared.Humanoid;

namespace Content.Server.DeadSpace.Armutant.Base;

public sealed class ArmutantRuleSystem : GameRuleSystem<ArmutantRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmutantRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<ArmutantRuleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void AfterAntagSelected(Entity<ArmutantRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;
        _antag.SendBriefing(ent, MakeBriefing(ent), null, null);
    }

    private void OnGetBriefing(Entity<ArmutantRuleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        var isHuman = HasComp<HumanoidAppearanceComponent>(ent);
        var briefing = isHuman
            ? Loc.GetString("armutant-role-greeting-human")
            : Loc.GetString("armutant-role-greeting-animal");

        if (isHuman)
            briefing += "\n \n" + Loc.GetString("armutant-role-description") + "\n";

        return briefing;
    }
}

using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.DeadSpace.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.DeadSpace.Abductor;
using Content.Shared.DeadSpace.Roles;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    private static readonly string DefaultAbductorVictimRule = "AbductorVictim";

    public void InitializeVictim()
    {
        SubscribeLocalEvent<AbductorOrganComponent, ImplantImplantedEvent>(OnImplanted);
    }

    private void OnImplanted(Entity<AbductorOrganComponent> ent, ref ImplantImplantedEvent args)
    {
        if (HasComp<AbductorComponent>(args.Implanted)
            || !TryComp<AbductorVictimComponent>(args.Implanted, out var victimComp)
            || victimComp.Implanted
            || !HasComp<HumanoidAppearanceComponent>(args.Implanted)
            || !_mind.TryGetMind(args.Implanted, out var mindId, out var mind)
            || !TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        if (mindId == default
            || !_role.MindHasRole<AbductorVictimRoleComponent>(mindId, out _))
        {
            _role.MindAddRole(mindId, "MindRoleAbductorVictim");
            victimComp.Implanted = true;
            _antag.ForceMakeAntag<AbductorVictimRuleComponent>(actor.PlayerSession, DefaultAbductorVictimRule);

            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(args.Implanted)} has been implanted with {ToPrettyString(ent.Owner)} and given an abductee objective.");
        }
    }
}
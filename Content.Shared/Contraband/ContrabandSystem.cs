using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Localizations;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Contraband;

/// <summary>
/// This handles showing examine messages for contraband-marked items.
/// </summary>
public sealed class ContrabandSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedIdCardSystem _id = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private bool _contrabandExamineEnabled;
    private bool _contrabandExamineOnlyInHudEnabled;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ContrabandComponent, GetVerbsEvent<ExamineVerb>>(OnDetailedExamine);

        Subs.CVar(_configuration, CCVars.ContrabandExamine, SetContrabandExamine, true);
        Subs.CVar(_configuration, CCVars.ContrabandExamineOnlyInHUD, SetContrabandExamineOnlyInHUD, true);
    }

    public void CopyDetails(EntityUid uid, ContrabandComponent other, ContrabandComponent? contraband = null)
    {
        if (!Resolve(uid, ref contraband))
            return;

        contraband.Severity = other.Severity;
        contraband.AllowedDepartments = other.AllowedDepartments;
        contraband.AllowedJobs = other.AllowedJobs;
        Dirty(uid, contraband);
    }

    private void OnDetailedExamine(EntityUid ent, ContrabandComponent component, ref GetVerbsEvent<ExamineVerb> args)
    {

        if (!_contrabandExamineEnabled)
            return;

        // Checking if contraband is only shown in the HUD
        if (_contrabandExamineOnlyInHudEnabled)
        {
            var ev = new GetContrabandDetailsEvent();
            RaiseLocalEvent(args.User, ref ev);
            if (!ev.CanShowContraband)
                return;
        }

        // CanAccess is not used here, because we want people to be able to examine legality in strip menu.
        if (!args.CanInteract)
            return;

        // two strings:
        // one, the actual informative 'this is restricted'
        // then, the 'you can/shouldn't carry this around' based on the ID the user is wearing
        var severity = _proto.Index(component.Severity);
        String departmentExamineMessage;
        if (severity.ShowDepartmentsAndJobs)
        {
            // department restricted text
            departmentExamineMessage =
                GenerateDepartmentExamineMessage(component.AllowedDepartments, component.AllowedJobs);
        }
        else
        {
            departmentExamineMessage = Loc.GetString(severity.ExamineText);
        }

        // text based on ID card
        List<ProtoId<DepartmentPrototype>> departments = new();
        var jobId = "";
        if (_id.TryFindIdCard(args.User, out var id))
        {
            departments = id.Comp.JobDepartments;
            if (id.Comp.LocalizedJobTitle is not null)
            {
                jobId = id.Comp.LocalizedJobTitle;
            }
        }

        var jobs = component.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
        // if it is fully restricted, you're department-less, or your department isn't in the allowed list, you cannot carry it. Otherwise, you can.
        var carryingMessage = Loc.GetString("contraband-examine-text-avoid-carrying-around");
        var iconTexture = "/Textures/Interface/VerbIcons/lock-red.svg.192dpi.png";
        if (departments.Intersect(component.AllowedDepartments).Any()
            || jobs.Contains(jobId))
        {
            carryingMessage = Loc.GetString("contraband-examine-text-in-the-clear");
            iconTexture = "/Textures/Interface/VerbIcons/unlock-green.svg.192dpi.png";
        }
        var examineMarkup = GetContrabandExamine(departmentExamineMessage, carryingMessage);
        _examine.AddHoverExamineVerb(args,
            component,
            Loc.GetString("contraband-examinable-verb-text"),
            examineMarkup.ToMarkup(),
            iconTexture);
    }

    public string GenerateDepartmentExamineMessage(HashSet<ProtoId<DepartmentPrototype>> allowedDepartments, HashSet<ProtoId<JobPrototype>> allowedJobs, ContrabandItemType itemType = ContrabandItemType.Item)
    {
        var localizedDepartments = allowedDepartments.Select(p => Loc.GetString("contraband-department-plural", ("department", Loc.GetString(_proto.Index(p).Name))));
        var jobs = allowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
        var localizedJobs = jobs.Select(p => Loc.GetString("contraband-job-plural", ("job", p)));

        //creating a combined list of jobs and departments for the restricted text
        var list = ContentLocalizationManager.FormatList(localizedDepartments.Concat(localizedJobs).ToList());

        // department restricted text
        return Loc.GetString("contraband-examine-text-Restricted-department", ("departments", list), ("type", itemType));
    }

    private FormattedMessage GetContrabandExamine(String deptMessage, String carryMessage)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(deptMessage);
        msg.PushNewline();
        msg.AddMarkupOrThrow(carryMessage);
        return msg;
    }

    private void SetContrabandExamine(bool val)
    {
        _contrabandExamineEnabled = val;
    }

    private void SetContrabandExamineOnlyInHUD(bool val)
    {
        _contrabandExamineOnlyInHudEnabled = val;
    }

    /// <summary>
    /// Determines if an item is contraband for a given player. If no player is provided, will just return if the item
    /// is contraband in general.
    /// </summary>
    /// <param name="contraband">The entity that we are checking for contraband.</param>
    /// <param name="player">The player that we are checking if they are allowed to have this contraband.</param>
    /// <param name="contraProtoId">The contraband ProtoId if the item is contraband.</param>
    /// <returns></returns>
    public bool IsContraband(Entity<ContrabandComponent?> contraband, EntityUid? player, [NotNullWhen(true)] out ProtoId<ContrabandSeverityPrototype>? contraProtoId)
    {
        contraProtoId = null;

        if (!Resolve(contraband.Owner, ref contraband.Comp, false))
            return false;

        contraProtoId = contraband.Comp.Severity;

        if (player == null)
            return true;

        List<ProtoId<DepartmentPrototype>> departments = new();
        var jobId = "";
        if (_id.TryFindIdCard(player.Value, out var id))
        {
            departments = id.Comp.JobDepartments;
            if (id.Comp.LocalizedJobTitle is not null)
                jobId = id.Comp.LocalizedJobTitle;
        }

        // DS14: use the injected prototype manager for the current engine branch.
        var jobs = contraband.Comp.AllowedJobs.Select(p => _proto.Index(p).LocalizedName).ToArray();
        // if it is fully restricted, you're department-less, or your department isn't in the allowed list, you cannot carry it. Otherwise, you can.
        if (departments.Intersect(contraband.Comp.AllowedDepartments).Any() || jobs.Contains(jobId))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a storage has contraband.
    /// </summary>
    /// <param name="contraband">The entity that we are checking for contraband.</param>
    /// <param name="player">The player that we are checking if they are allowed to have certain contraband.</param>
    /// <param name="contrabandList">All contraband prototypes present in storage.</param>
    public bool ContainerHasContraband(Entity<ContainerManagerComponent?> contraband, EntityUid? player, out List<ProtoId<ContrabandSeverityPrototype>> contrabandList)
    {
        contrabandList = [];

        if (!Resolve(contraband.Owner, ref contraband.Comp, false))
            return false;

        foreach (var container in contraband.Comp.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                if (IsContraband(ent, player, out var itemContraId))
                    contrabandList.Add((ProtoId<ContrabandSeverityPrototype>)itemContraId);

                ContainerHasContraband(ent, player, out var itemContraList);

                contrabandList = contrabandList.Concat(itemContraList).ToList();
            }
        }

        return contrabandList.Any();
    }
}

/// <summary>
/// The item type that the contraband text should follow in the description text.
/// </summary>
public enum ContrabandItemType
{
    Item,
    Reagent
}

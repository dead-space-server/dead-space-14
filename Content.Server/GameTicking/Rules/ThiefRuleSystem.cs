using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.DeadSpace.Thief;
using Content.Shared.Humanoid;
using Content.Shared.PDA;
using Content.Shared.Roles.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed class ThiefRuleSystem : GameRuleSystem<ThiefRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    // DS14: one of these tools (all fit in a utility tool belt) is chosen at random
    // each round as the exact tool the thief must insert to unlock ВорПРО.
    private static readonly EntProtoId[] UnlockToolOptions =
    {
        "Screwdriver", "Wrench", "Crowbar", "Wirecutter", "Multitool", "PowerDrill",
        "Welder", "RemoteSignaller",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<ThiefRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon thief activation
    private void AfterAntagSelected(Entity<ThiefRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;

        // DS14: ВорПРО wiring is disabled for normal players (kept for the future rework).
        // // pick the round's unlock tool once per rule, then mark the thief's own PDA(s)
        // // so ВорПРО can only be unlocked on the thief's PDA with exactly this tool.
        // if (mindId.Comp.UnlockTool == null)
        //     mindId.Comp.UnlockTool = _random.Pick(UnlockToolOptions);
        //
        // MarkThiefPdas(ent);

        _antag.SendBriefing(ent, MakeBriefing(ent), null, null);
    }

    /// <summary>
    /// DS14: Adds <see cref="ThiefPdaComponent"/> to every PDA found in the thief's
    /// inventory. Only such marked PDAs accept tools in their tool slot.
    /// </summary>
    private void MarkThiefPdas(EntityUid ent)
    {
        var contained = CollectContained(ent);
        foreach (var uid in contained)
        {
            if (HasComp<PdaComponent>(uid))
            {
                EnsureComp<ThiefPdaComponent>(uid);
            }
        }
    }

    /// <summary>
    /// DS14: Recursively collects all entities contained by the holder (inventory, bags, hands).
    /// </summary>
    private List<EntityUid> CollectContained(EntityUid holder)
    {
        var acc = new List<EntityUid>();
        if (!TryComp<ContainerManagerComponent>(holder, out var manager))
            return acc;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                acc.Add(contained);
                acc.AddRange(CollectContained(contained));
            }
        }

        return acc;
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<ThiefRoleComponent> role, ref GetBriefingEvent args)
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
            ? Loc.GetString("thief-role-greeting-human")
            : Loc.GetString("thief-role-greeting-animal");

        if (isHuman)
            briefing += "\n \n" + Loc.GetString("thief-role-greeting-equipment") + "\n";

        // DS14: no ВорПРО goal/unlock hint for normal players (kept for the future rework).
        // briefing += "\n" + Loc.GetString("thief-role-greeting-goal") + "\n";
        //
        // var tool = GetUnlockToolName();
        // briefing += "\n" + Loc.GetString("thief-role-greeting-unlock", ("tool", tool)) + "\n";

        return briefing;
    }

    /// <summary>
    /// DS14: Returns the localized name of the round's chosen unlock tool, or a fallback
    /// string if the rule/tool is not available.
    /// </summary>
    private string GetUnlockToolName()
    {
        foreach (var rule in _gameTicker.GetActiveGameRules())
        {
            if (!TryComp<ThiefRuleComponent>(rule, out var comp))
                continue;

            if (comp.UnlockTool == null)
                comp.UnlockTool = _random.Pick(UnlockToolOptions);

            return Loc.GetString(_proto.Index(comp.UnlockTool.Value).Name);
        }

        return Loc.GetString("thief-role-greeting-unlock-fallback");
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.PowerCell;
using Robust.Shared.Prototypes;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract partial class SharedSpiderOSSystem : EntitySystem
{
    [Dependency] protected readonly SharedTransformSystem SharedTransform = default!;
    [Dependency] protected readonly SharedSpaceNinjaSystem Ninja = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly InventorySystem Inventory = default!;
    [Dependency] protected readonly PowerCellSystem PowerCell = default!;

    public const float RollbackDelaySeconds = 10f;
    public const float ConfirmTimeoutSeconds = 50f;
    public const float LockTimeoutSeconds = 50f;
    public const float FinishDelaySeconds = 10f;

    public static readonly Dictionary<SpiderOSBootCheck, string> SuitHardwareSlots = new()
    {
        [SpiderOSBootCheck.VisorSecure] = "eyes",
        [SpiderOSBootCheck.HelmetSecure] = "head",
        [SpiderOSBootCheck.MaskSecure] = "mask",
        [SpiderOSBootCheck.GlovesSecure] = "gloves",
        [SpiderOSBootCheck.BootsSecure] = "shoes",
    };

    public bool RunBootCheck(EntityUid suitUid, EntityUid actor, SpiderOSBootCheck check, out LocId failReason)
    {
        failReason = string.Empty;
        TryComp<SpiderOSComponent>(suitUid, out var comp);

        switch (check)
        {
            case SpiderOSBootCheck.SelfTest:
                return true;
            case SpiderOSBootCheck.Authorized:
                if (IsAuthorized(suitUid, actor))
                    return true;

                failReason = "spider-os-boot-fail-auth";
                return false;
            case SpiderOSBootCheck.NotActivated:
                if (comp == null || !comp.SuitActivated)
                    return true;

                failReason = "spider-os-boot-fail-already-active";
                return false;
            case SpiderOSBootCheck.ModulesReady:
                if (comp != null && AllModulesSelected(comp))
                    return true;

                failReason = "spider-os-boot-fail-modules-incomplete";
                return false;
            case SpiderOSBootCheck.VisorSecure:
            case SpiderOSBootCheck.HelmetSecure:
            case SpiderOSBootCheck.MaskSecure:
            case SpiderOSBootCheck.GlovesSecure:
            case SpiderOSBootCheck.BootsSecure:
                if (IsAuthorized(suitUid, actor) && HasSuitHardware(actor, SuitHardwareSlots[check]))
                    return true;

                failReason = check switch
                {
                    SpiderOSBootCheck.VisorSecure => "spider-os-boot-fail-no-visor",
                    SpiderOSBootCheck.HelmetSecure => "spider-os-boot-fail-no-helmet",
                    SpiderOSBootCheck.MaskSecure => "spider-os-boot-fail-no-mask",
                    SpiderOSBootCheck.GlovesSecure => "spider-os-boot-fail-no-gloves",
                    _ => "spider-os-boot-fail-no-boots",
                };
                return false;
            case SpiderOSBootCheck.SuitFasten:
                if (IsAuthorized(suitUid, actor))
                    return true;

                failReason = "spider-os-boot-fail-not-worn";
                return false;
            case SpiderOSBootCheck.BatteryReport:
                if (PowerCell.TryGetBatteryFromSlotOrEntity(suitUid, out _))
                    return true;

                failReason = "spider-os-boot-fail-no-power";
                return false;
            default:
                return true;
        }
    }

    public bool RunBootScriptChecks(EntityUid suitUid, EntityUid actor, SpiderOSBootPrototype boot)
    {
        foreach (var step in boot.Steps)
        {
            if (step.Check is { } check && !RunBootCheck(suitUid, actor, check, out _))
            {
                return false;
            }
        }

        return true;
    }

    protected bool IsAuthorized(EntityUid suitUid, EntityUid actor)
    {
        if (!actor.IsValid() || actor != SharedTransform.GetParentUid(suitUid))
            return false;

        if (!Ninja.NinjaQuery.TryComp(actor, out var ninja))
            return false;

        return ninja.Suit == suitUid;
    }

    protected bool HasSuitHardware(EntityUid wearer, string slot)
    {
        return Inventory.TryGetSlotEntity(wearer, slot, out var item) &&
               TryComp<NinjaSuitItemComponent>(item.Value, out _);
    }

    protected bool AllModulesSelected(SpiderOSComponent comp)
    {
        if (!Proto.TryIndex(comp.Skills.Id, out SpiderOSPrototype? proto) || proto.AllSkills.Count == 0)
        {
            return false;
        }

        foreach (var skill in proto.AllSkills)
        {
            if (!comp.SelectedModules.ContainsKey(skill.Tier) && !comp.LockedTiers.Contains(skill.Tier))
            {
                return false;
            }
        }

        return true;
    }

    protected bool TryGetSkill(SpiderOSComponent comp, NinjaSkillsCategory category, int tier, [NotNullWhen(true)] out NinjaSkill skill)
    {
        skill = default;

        if (tier < 1 || !Enum.IsDefined(typeof(NinjaSkillsCategory), category))
        {
            return false;
        }

        if (!Proto.TryIndex(comp.Skills.Id, out SpiderOSPrototype? proto))
        {
            return false;
        }

        foreach (var candidate in proto.AllSkills)
        {
            if (candidate.Category == category && candidate.Tier == tier)
            {
                skill = candidate;
                return true;
            }
        }

        return false;
    }
}

[ByRefEvent]
public readonly record struct SpiderOSPowerChangedEvent(EntityUid Suit, EntityUid Wearer, bool Activated);
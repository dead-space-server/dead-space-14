// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Mobs;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

public sealed class SurgeryXrayCard
{
    public string Status = "";
    public string Overview = "";
    public string FocusTitle = "";
    public string Focus = "";
}

public static class SurgeryXrayReport
{
    public static string Life(MobState state)
    {
        return state switch
        {
            MobState.Dead => Loc.GetString("health-analyzer-window-entity-dead-text"),
            MobState.Critical => Loc.GetString("health-analyzer-window-entity-critical-text"),
            MobState.PreCritical => Loc.GetString("health-analyzer-window-entity-pre-critical-text"),
            _ => Loc.GetString("health-analyzer-window-entity-alive-text"),
        };
    }

    public static SurgeryXrayCard Make(LimbBodyComponent limbs, SurgeryCavityComponent? hold, LimbZone? zone, string status)
    {
        var issues = new List<LimbScanIssue>();
        LimbScan.Build(limbs.Zones, limbs.Organs, out var summary, issues);
        var card = new SurgeryXrayCard
        {
            Status = status,
            Overview = Overview(limbs, summary, issues, hold),
        };

        if (zone == null)
        {
            card.FocusTitle = Loc.GetString("surgery-xray-pick");
            card.Focus = Loc.GetString("surgery-xray-pick-hint");
            return card;
        }

        card.FocusTitle = Loc.GetString($"health-analyzer-zone-{(byte) zone.Value}");
        card.Focus = Focus(limbs, zone.Value, issues, hold);
        return card;
    }

    public static string Paper(string name, SurgeryXrayCard card, bool image)
    {
        var text = $"""
            [bold]МЕД[/bold]
            ═════════════════════════════════════
            [bold]{Loc.GetString("surgery-xray-title")}[/bold]
            ═════════════════════════════════════
            [bold]{Loc.GetString("surgery-xray-patient", ("name", name))}[/bold]
            [bold]{Loc.GetString("surgery-xray-status", ("state", card.Status))}[/bold]

            [bold]{Loc.GetString("surgery-xray-overview")}[/bold]
            {card.Overview}

            [bold]{card.FocusTitle}[/bold]
            {card.Focus}
            """;

        if (image)
            text += "\n{СЮДА КАРТИНКУ}";

        text += $"""

            ═════════════════════════════════════
            [italic]{Loc.GetString("surgery-xray-sign")}[/italic]
            """;
        return text;
    }

    private static string Overview(LimbBodyComponent limbs, LimbScanSummary summary, List<LimbScanIssue> issues, SurgeryCavityComponent? hold)
    {
        var bleeds = 0f;
        var count = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            if (limbs.Zones[i].Bleed > LimbWoundRules.TickBleed)
                bleeds += limbs.Zones[i].Bleed;
        }

        var foreign = HasForeign(hold);
        if (summary.Healthy && bleeds == 0 && !foreign)
            return Loc.GetString("surgery-xray-clear");

        var text = Loc.GetString("surgery-xray-count-fracture", ("n", summary.Fractures))
               + "\n" + Loc.GetString("surgery-xray-count-bleed", ("n", MathF.Round(bleeds)))
               + "\n" + Loc.GetString("surgery-xray-count-rupture", ("n", summary.Ruptures))
               + "\n" + Loc.GetString("surgery-xray-count-embedded", ("n", summary.Embedded));
        if (foreign)
            text += "\n" + Loc.GetString("surgery-xray-foreign");
        return text;
    }

    private static string Focus(LimbBodyComponent limbs, LimbZone zone, List<LimbScanIssue> issues, SurgeryCavityComponent? hold)
    {
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return Loc.GetString("surgery-xray-empty");

        var state = limbs.Zones[index];
        var findings = new List<string>();
        foreach (var issue in issues)
        {
            if (issue.Organ || issue.Index != (byte) zone || issue.Kind == LimbIssueKind.Severed)
                continue;

            findings.Add(Loc.GetString($"health-analyzer-issue-{(byte) issue.Kind}"));
        }

        foreach (var issue in issues)
        {
            if (!issue.Organ || issue.Kind != LimbIssueKind.Rupture)
                continue;

            if (LimbOrganHomes.Home((LimbOrgan) issue.Index) != zone)
                continue;

            var organ = Loc.GetString($"health-analyzer-organ-{issue.Index}");
            findings.Add(Loc.GetString("surgery-xray-rupture", ("organ", organ)));
        }

        var found = findings.Count == 0
            ? Loc.GetString("health-analyzer-limbs-part-ok")
            : string.Join(", ", findings);
        var text = Loc.GetString("surgery-xray-integrity", ("health", MathF.Round(state.Health)), ("pool", MathF.Round(LimbWoundRules.HealthFor(zone))));
        if (state.Bone >= LimbWoundRules.StrainBone)
            text += "\n" + Loc.GetString("surgery-xray-bone", ("n", MathF.Round(state.Bone)));
        if (state.Flesh >= LimbWoundRules.StrainFlesh)
            text += "\n" + Loc.GetString("surgery-xray-flesh", ("n", MathF.Round(state.Flesh)));
        if (state.Bleed > LimbWoundRules.TickBleed)
            text += "\n" + Loc.GetString("surgery-xray-bleed", ("n", MathF.Round(state.Bleed)));
        text += "\n" + Loc.GetString("surgery-xray-findings", ("text", found));
        if (zone is LimbZone.Head or LimbZone.Torso or LimbZone.Groin && ItemName(hold, zone).Length > 0)
            text += "\n" + Loc.GetString("surgery-xray-foreign");
        return text;
    }

    private static bool HasForeign(SurgeryCavityComponent? hold)
    {
        if (hold == null)
            return false;

        return hold.HeadProto.Length > 0 || hold.ChestProto.Length > 0 || hold.AbdomenProto.Length > 0;
    }

    private static string ItemName(SurgeryCavityComponent? hold, LimbZone zone)
    {
        if (hold == null)
            return "";

        var proto = zone switch
        {
            LimbZone.Head => hold.HeadProto,
            LimbZone.Groin => hold.AbdomenProto,
            LimbZone.Torso => hold.ChestProto,
            _ => "",
        };
        return ItemProto(proto);
    }

    private static string ItemProto(string proto)
    {
        if (proto.Length == 0)
            return "";

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        return prototypes.TryIndex<EntityPrototype>(proto, out var found) ? found.Name : proto;
    }
}

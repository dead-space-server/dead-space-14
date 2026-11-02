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

    public static SurgeryXrayCard Make(LimbBodyComponent limbs, SurgeryCavityComponent? hold, LimbZone? zone, string status, SurgeryInfectionComponent? infection = null, float vessel = 0f, bool machine = false)
    {
        var issues = new List<LimbScanIssue>();
        LimbScan.Build(limbs.Zones, limbs.Organs, out var summary, issues);
        var card = new SurgeryXrayCard
        {
            Status = status,
            Overview = Overview(limbs, summary, issues, hold, Foci(infection), vessel, machine),
        };

        if (zone == null)
        {
            card.FocusTitle = Loc.GetString("surgery-xray-pick");
            card.Focus = Loc.GetString("surgery-xray-pick-hint");
            return card;
        }

        card.FocusTitle = Loc.GetString($"health-analyzer-zone-{(byte) zone.Value}");
        card.Focus = Focus(limbs, zone.Value, issues, hold, machine);
        if (infection != null && infection.Focus(zone.Value))
            card.Focus += "\n" + Loc.GetString("surgery-xray-infection");
        return card;
    }

    private static int Foci(SurgeryInfectionComponent? infection)
    {
        if (infection == null)
            return 0;

        var zones = new HashSet<LimbZone>(infection.Rotting);
        zones.UnionWith(infection.Latent);
        return zones.Count;
    }

    public static string Paper(string name, SurgeryXrayCard card, bool image)
    {
        var text = $"""
            [bold]{Loc.GetString("surgery-xray-mark")}[/bold]
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

    private static string Overview(LimbBodyComponent limbs, LimbScanSummary summary, List<LimbScanIssue> issues, SurgeryCavityComponent? hold, int foci, float vessel, bool machine)
    {
        var bleeds = machine ? 0f : MathF.Max(0f, vessel);
        var count = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            if (machine || limbs.Zones[i].Severed)
                continue;

            if (limbs.Zones[i].Bleed > LimbWoundRules.TickBleed)
                bleeds += limbs.Zones[i].Bleed;
        }

        var foreign = HasForeign(hold);
        if (summary.Healthy && bleeds == 0 && !foreign && foci == 0)
            return Loc.GetString("surgery-xray-clear");

        var lines = new List<string>();
        if (summary.Fractures > 0)
            lines.Add(Loc.GetString("surgery-xray-count-fracture", ("n", summary.Fractures)));
        if (bleeds > LimbWoundRules.TickBleed)
            lines.Add(Loc.GetString("surgery-xray-bleed", ("level", BleedLevel(bleeds))));
        if (summary.Ruptures > 0)
            lines.Add(Loc.GetString("surgery-xray-count-rupture", ("n", summary.Ruptures)));
        if (summary.Embedded > 0)
            lines.Add(Loc.GetString("surgery-xray-count-embedded", ("n", summary.Embedded)));
        if (foci > 0)
            lines.Add(Loc.GetString("surgery-xray-count-infection", ("n", foci)));
        if (foreign)
            lines.Add(Loc.GetString("surgery-xray-foreign"));
        return lines.Count == 0 ? Loc.GetString("surgery-xray-clear") : string.Join("\n", lines);
    }

    public static string BleedLevel(float bleed)
    {
        var level = bleed < 1f ? 0 : bleed < 3f ? 1 : bleed < 6f ? 2 : 3;
        return Loc.GetString($"surgery-xray-bleed-level-{level}");
    }

    private static string Focus(LimbBodyComponent limbs, LimbZone zone, List<LimbScanIssue> issues, SurgeryCavityComponent? hold, bool machine)
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

            if (issue.Kind == LimbIssueKind.Bleed)
                continue;

            findings.Add(Loc.GetString("surgery-xray-found", ("what", IssueName(issue.Kind, machine))));
        }

        foreach (var issue in issues)
        {
            if (!issue.Organ || issue.Kind != LimbIssueKind.Rupture)
                continue;

            if (LimbOrganHomes.Home((LimbOrgan) issue.Index) != zone)
                continue;

            var organ = Loc.GetString($"health-analyzer-organ-{issue.Index}");
            findings.Add(Loc.GetString("surgery-xray-found", ("what", Loc.GetString("surgery-xray-rupture", ("organ", organ)))));
        }

        var text = Loc.GetString("surgery-xray-integrity", ("health", MathF.Round(state.Health)), ("pool", MathF.Round(LimbWoundRules.HealthFor(zone, limbs))));
        if (!machine && !state.Severed && state.Bleed > LimbWoundRules.TickBleed)
            text += "\n" + Loc.GetString("surgery-xray-bleed", ("level", BleedLevel(state.Bleed)));
        foreach (var line in findings)
            text += "\n" + line;
        if (zone is LimbZone.Head or LimbZone.Torso or LimbZone.Groin && ItemName(hold, zone) is { Length: > 0 } item)
            text += "\n" + Loc.GetString("surgery-xray-item", ("name", item));
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

    private static string IssueName(LimbIssueKind kind, bool machine)
    {
        if (machine)
            return Loc.GetString($"health-analyzer-issue-machine-{(byte) kind}");

        return Loc.GetString($"health-analyzer-issue-{(byte) kind}");
    }
}

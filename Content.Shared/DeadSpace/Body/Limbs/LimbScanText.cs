// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbScanText
{
    public static string Summary(LimbScanSummary summary)
    {
        var parts = new List<string>();
        if (summary.Fractures > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-fractures", ("count", summary.Fractures)));
        if (summary.Wounds > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-wounds", ("count", summary.Wounds)));
        if (summary.Ruptures > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-ruptures", ("count", summary.Ruptures)));
        if (summary.Embedded > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-embedded", ("count", summary.Embedded)));
        if (summary.RawSkin > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-skin", ("count", summary.RawSkin)));
        if (summary.Burning > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-fire", ("count", summary.Burning)));
        if (summary.Scars > 0)
            parts.Add(Loc.GetString("health-analyzer-limbs-scars", ("count", summary.Scars)));

        return string.Join(" ", parts);
    }

    public static void Collect(IReadOnlyList<byte> shades, int? only, List<(byte Index, bool Missing)> found)
    {
        found.Clear();
        var count = Math.Min(shades.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            if (only is { } zone && i != zone)
                continue;

            var shade = shades[i];
            if (shade == LimbScan.Fine)
                continue;

            found.Add(((byte) i, shade == LimbScan.Missing));
        }
    }

    public static List<string> Areas(IReadOnlyList<byte> shades, LimbZone? only)
    {
        var found = new List<(byte Index, bool Missing)>();
        Collect(shades, only is { } zone ? (int) zone : null, found);
        var lines = new List<string>(found.Count);
        foreach (var (index, missing) in found)
        {
            var name = Loc.GetString($"health-analyzer-zone-{index}");
            var issue = Loc.GetString(missing ? "health-analyzer-limb-gone" : "health-analyzer-limb-hurt");
            lines.Add(Loc.GetString("health-analyzer-limb-line", ("name", name), ("issues", issue)));
        }

        return lines;
    }

    public static List<string> Lines(IReadOnlyList<LimbScanIssue> issues)
    {
        var grouped = new List<(bool Organ, byte Index, List<LimbIssueKind> Kinds)>();
        foreach (var issue in issues)
        {
            var found = -1;
            for (var i = 0; i < grouped.Count; i++)
            {
                if (grouped[i].Organ == issue.Organ && grouped[i].Index == issue.Index)
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
            {
                grouped.Add((issue.Organ, issue.Index, new List<LimbIssueKind> { issue.Kind }));
                continue;
            }

            grouped[found].Kinds.Add(issue.Kind);
        }

        var lines = new List<string>(grouped.Count);
        foreach (var (organ, index, kinds) in grouped)
        {
            var name = organ
                ? Loc.GetString($"health-analyzer-organ-{index}")
                : Loc.GetString($"health-analyzer-zone-{index}");
            var text = new List<string>(kinds.Count);
            foreach (var kind in kinds)
            {
                if (kind == LimbIssueKind.Severed)
                    continue;

                text.Add(Loc.GetString($"health-analyzer-issue-{(byte) kind}"));
            }

            if (text.Count == 0)
                continue;

            lines.Add(Loc.GetString("health-analyzer-limb-line", ("name", name), ("issues", string.Join(", ", text))));
        }

        return lines;
    }
}

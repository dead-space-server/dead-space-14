// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Surgery;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbExamine
{
    private const string Warn = "[color=#c43838]{0}[/color]";

    public static void Write(FormattedMessage message, LimbBodyComponent limbs, bool machine, bool adrenaline)
    {
        var zones = limbs.Zones;
        var count = Math.Min(zones.Count, LimbWoundRules.BodyZoneCount);
        var limpLeft = false;
        var limpRight = false;
        var torsoGone = false;

        for (var i = 0; i < count; i++)
        {
            var zone = (LimbZone) i;
            var state = zones[i];
            if (zone is LimbZone.LegLeft or LimbZone.FootLeft or LimbZone.LegRight or LimbZone.FootRight)
            {
                Leg(message, zone, state, machine, adrenaline, ref limpLeft, ref limpRight);
                continue;
            }

            if (zone == LimbZone.Torso && state.Severed)
                torsoGone = true;

            Part(message, zone, state, machine);
        }

        if (limpLeft)
            Key(message, "limb-examine-limp-left", false);

        if (limpRight)
            Key(message, "limb-examine-limp-right", false);

        if (!torsoGone && limbs.LungWhistle)
            Key(message, machine ? "limb-examine-whistle-machine" : "limb-examine-whistle", true);

        Organs(message, limbs);
    }

    private static void Part(FormattedMessage message, LimbZone zone, in LimbZoneState state, bool machine)
    {
        var side = Side(zone);
        if (state.Severed)
        {
            if (zone is not (LimbZone.Groin or LimbZone.Torso))
                Key(message, $"limb-examine-missing-{side}", zone == LimbZone.Head);
            return;
        }

        if (state.Atrophied || state.Health <= 0f)
        {
            Key(message, $"limb-examine-dead-{side}", zone is LimbZone.Head or LimbZone.Torso);
            if (!machine)
                Bleed(message, side, state.Bleed);
            return;
        }

        if (zone == LimbZone.Torso)
            Ribs(message, state);
        else if (state.Bone >= LimbWoundRules.BreakBone && state.Flesh >= LimbWoundRules.StrainFlesh)
            Key(message, $"limb-examine-open-{side}", false);
        else if (state.Bone >= LimbWoundRules.BreakBone && zone is not (LimbZone.Head or LimbZone.Groin))
            Key(message, $"limb-examine-broken-{side}", false);
        else if (state.HitBlunt > SurgeryConditions.TraumaMin)
            Key(message, machine ? $"limb-examine-dent-{side}" : $"limb-examine-bruise-{side}", false);

        if (zone == LimbZone.Torso && state.HitBlunt > SurgeryConditions.TraumaMin)
            Key(message, machine ? "limb-examine-dent-torso" : "limb-examine-bruise-torso", false);

        if (!machine)
            Bleed(message, side, state.Bleed);
    }

    private static void Leg(
        FormattedMessage message,
        LimbZone zone,
        in LimbZoneState state,
        bool machine,
        bool adrenaline,
        ref bool limpLeft,
        ref bool limpRight)
    {
        var side = Side(zone);
        var left = zone is LimbZone.LegLeft or LimbZone.FootLeft;
        if (state.Severed)
        {
            Key(message, $"limb-examine-missing-{side}", false);
            return;
        }

        if (state.Atrophied || state.Health <= 0f)
        {
            Key(message, $"limb-examine-dead-{side}", false);
            if (!machine)
                Bleed(message, side, state.Bleed);
            return;
        }

        var open = state.Bone >= LimbWoundRules.BreakBone && state.Flesh >= LimbWoundRules.StrainFlesh;
        var fracture = state.Bone >= LimbWoundRules.BreakBone;
        if (open)
            Key(message, $"limb-examine-open-{side}", false);
        else if (fracture && !state.Splinted && !adrenaline)
        {
            if (left)
                limpLeft = true;
            else
                limpRight = true;
        }
        else if (fracture)
            Key(message, $"limb-examine-broken-{side}", false);
        else if (state.HitBlunt > SurgeryConditions.TraumaMin)
            Key(message, machine ? $"limb-examine-dent-{side}" : $"limb-examine-bruise-{side}", false);

        if (state.Embedded && !state.Splinted && !adrenaline)
        {
            if (left)
                limpLeft = true;
            else
                limpRight = true;
        }

        if (!machine)
            Bleed(message, side, state.Bleed);
    }

    private static void Bleed(FormattedMessage message, string side, float bleed)
    {
        if (bleed <= LimbWoundRules.TickBleed)
            return;

        var heavy = bleed >= 3f;
        Key(message, heavy ? $"limb-examine-bleed-heavy-{side}" : $"limb-examine-bleed-{side}", heavy);
    }

    private static void Ribs(FormattedMessage message, in LimbZoneState state)
    {
        if (state.Bone >= LimbWoundRules.BreakBone && state.Flesh >= LimbWoundRules.StrainFlesh)
            Key(message, "limb-examine-rib-open", true);
    }

    private static void Organs(FormattedMessage message, LimbBodyComponent limbs)
    {
        var index = (int) LimbOrgan.Eyes;
        if (limbs.Organs.Count <= index || (limbs.Present & LimbOrganMask.Eyes) == 0)
            return;

        var home = LimbWoundRules.Index(LimbOrganHomes.Home(LimbOrgan.Eyes));
        if ((uint) home < (uint) limbs.Zones.Count && limbs.Zones[home].Severed)
            return;

        var state = limbs.Organs[index];
        if (state.Missing)
            Key(message, "limb-examine-organ-missing-1", false);
        else if (state.Ruptured)
            Key(message, "limb-examine-organ-torn-1", false);
    }

    private static string Side(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => "head",
            LimbZone.Torso => "torso",
            LimbZone.ArmLeft => "arm-left",
            LimbZone.ArmRight => "arm-right",
            LimbZone.LegLeft => "leg-left",
            LimbZone.LegRight => "leg-right",
            LimbZone.HandLeft => "hand-left",
            LimbZone.HandRight => "hand-right",
            LimbZone.FootLeft => "foot-left",
            LimbZone.FootRight => "foot-right",
            _ => "groin",
        };
    }

    private static void Key(FormattedMessage message, string key, bool warn)
    {
        Text(message, Loc.GetString(key), warn);
    }

    private static void Text(FormattedMessage message, string line, bool warn)
    {
        message.PushNewline();
        if (warn)
            line = string.Format(Warn, line);

        message.AddMarkupOrThrow(line);
    }
}

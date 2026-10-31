// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Serializable, NetSerializable]
public enum LimbIssueKind : byte
{
    Severed = 0,
    Fracture = 1,
    BoneStrain = 2,
    OpenWound = 3,
    RawSkin = 4,
    Fire = 5,
    Embedded = 6,
    Scar = 7,
    Rupture = 8,
    BurnFirst = 9,
    BurnSecond = 10,
    BurnThird = 11,
    ClosedFracture = 12,
    OpenFracture = 13,
    RibCrack = 14,
    RibClosed = 15,
    RibOpen = 16,
    Atrophy = 17,
    Bleed = 18,
}

[Serializable, NetSerializable]
public struct LimbPartDamage
{
    public byte Zone;
    public float Blunt;
    public float Slash;
    public float Pierce;
    public float Heat;
    public float Caustic;
    public float Blood;
    public float Air;

    public LimbPartDamage(byte zone, float blunt, float slash, float pierce, float heat, float caustic, float blood, float air)
    {
        Zone = zone;
        Blunt = blunt;
        Slash = slash;
        Pierce = pierce;
        Heat = heat;
        Caustic = caustic;
        Blood = blood;
        Air = air;
    }
}

[Serializable, NetSerializable]
public struct LimbScanIssue
{
    public bool Organ;
    public byte Index;
    public LimbIssueKind Kind;

    public LimbScanIssue(bool organ, byte index, LimbIssueKind kind)
    {
        Organ = organ;
        Index = index;
        Kind = kind;
    }
}

[Serializable, NetSerializable]
public struct LimbScanSummary
{
    public int Missing;
    public int Fractures;
    public int Wounds;
    public int Ruptures;
    public int Embedded;
    public int RawSkin;
    public int Burning;
    public int Scars;

    public bool Healthy =>
        Missing == 0 && Fractures == 0 && Wounds == 0 && Ruptures == 0 &&
        Embedded == 0 && RawSkin == 0 && Burning == 0 && Scars == 0;
}

public static class LimbScan
{
    public const byte Missing = 0;
    public const byte Hurt = 1;
    public const byte Bad = 2;
    public const byte Critical = 3;
    public const byte Fine = 4;

    public static byte Shade(LimbZone zone, in LimbZoneState state)
    {
        if (state.Severed)
            return Missing;

        if (state.Atrophied || state.Health <= 0f)
            return Critical;

        var pool = LimbWoundRules.HealthFor(zone);
        var left = pool <= 0f ? 1f : state.Health / pool;
        if (left <= 0.34f)
            return Critical;

        if (left <= 0.67f)
            return Bad;

        var degree = LimbWoundRules.BurnDegree(state.Skin);
        if (degree >= 4)
            return Critical;

        if (degree >= 3)
            return Bad;

        if (left < 0.98f
            || state.Embedded
            || state.Bleed > LimbWoundRules.TickBleed
            || state.Bone >= LimbWoundRules.StrainBone
            || state.Flesh >= LimbWoundRules.StrainFlesh
            || degree > 0)
            return Hurt;

        return Fine;
    }

    public static void Build(
        IReadOnlyList<LimbZoneState> zones,
        IReadOnlyList<LimbOrganState> organs,
        out LimbScanSummary summary,
        List<LimbScanIssue> issues)
    {
        summary = default;
        issues.Clear();

        var count = Math.Min(zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            var zone = zones[i];
            if (zone.Severed)
            {
                summary.Missing++;
                issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.Severed));
            }
            else if (zone.Atrophied || zone.Health <= 0f)
            {
                summary.Missing++;
            }
            else
            {
                if (i == (int) LimbZone.Torso)
                    AddRibs(zone, ref summary, issues);
                else if (zone.Bone >= LimbWoundRules.BreakBone && zone.Flesh >= LimbWoundRules.StrainFlesh)
                {
                    summary.Fractures++;
                    issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.OpenFracture));
                }
                else if (zone.Bone >= LimbWoundRules.BreakBone)
                {
                    summary.Fractures++;
                    issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.ClosedFracture));
                }
                else if (zone.Bone >= LimbWoundRules.StrainBone)
                {
                    issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.BoneStrain));
                }

                if (zone.Flesh >= LimbWoundRules.StrainFlesh)
                {
                    summary.Wounds++;
                    issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.OpenWound));
                }

                var degree = LimbWoundRules.BurnDegree(zone.Skin);
                if (degree > 0)
                {
                    summary.RawSkin++;
                    var kind = degree switch
                    {
                        1 => LimbIssueKind.BurnFirst,
                        2 => LimbIssueKind.BurnSecond,
                        _ => LimbIssueKind.BurnThird,
                    };
                    issues.Add(new LimbScanIssue(false, (byte) i, kind));
                }

                if (zone.Embedded)
                {
                    summary.Embedded++;
                    issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.Embedded));
                }
            }

            if (zone.Severed)
                continue;

            if (zone.Bleed > LimbWoundRules.TickBleed)
                issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.Bleed));

            if (zone.Fire > 0f)
            {
                summary.Burning++;
                issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.Fire));
            }

            if (zone.Scar > 0)
            {
                summary.Scars++;
                issues.Add(new LimbScanIssue(false, (byte) i, LimbIssueKind.Scar));
            }
        }

        var organCount = Math.Min(organs.Count, LimbOrganRules.Count);
        for (var i = 0; i < organCount; i++)
        {
            if (!organs[i].Ruptured)
                continue;

            var home = LimbOrganHomes.Home((LimbOrgan) i);
            var homeIndex = LimbWoundRules.Index(home);
            if ((uint) homeIndex < (uint) zones.Count && zones[homeIndex].Severed)
                continue;

            summary.Ruptures++;
            issues.Add(new LimbScanIssue(true, (byte) i, LimbIssueKind.Rupture));
        }
    }

    private static void AddRibs(LimbZoneState zone, ref LimbScanSummary summary, List<LimbScanIssue> issues)
    {
        LimbIssueKind kind;
        if (zone.Bone >= LimbWoundRules.BreakBone && zone.Flesh >= LimbWoundRules.StrainFlesh)
            kind = LimbIssueKind.RibOpen;
        else if (zone.Bone >= LimbWoundRules.BreakBone)
            kind = LimbIssueKind.RibClosed;
        else if (zone.Bone >= LimbWoundRules.StrainBone)
            kind = LimbIssueKind.RibCrack;
        else
            return;

        if (kind != LimbIssueKind.RibCrack)
            summary.Fractures++;

        issues.Add(new LimbScanIssue(false, (byte) LimbZone.Torso, kind));
    }
}

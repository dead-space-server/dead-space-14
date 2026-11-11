// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.Surgery;

public static class SurgeryConditions
{
    public const float OrganCrackMin = 5f;
    public const float TraumaMin = 50f;
    public const float WornBelow = 0.95f;

    public static readonly string[] Order =
    {
        "Severed",
        "Infection",
        "OpenFracture",
        "OpenPelvis",
        "Shard",
        "Foreign",
        "Bleed",
        "ClosedFracture",
        "ClosedPelvis",
        "Fracture",
        "Crack",
        "MindRupture",
        "OrganCrack",
        "Bruised",
        "Lacerated",
        "Punctured",
        "Worn",
        "Scaffold",
        "Skin",
        "Damaged",
    };

    public static bool Has(string when, in LimbZoneState state, bool hasBone, bool infected, bool organCrack = false, float pool = 0f, bool rot = false, bool foreign = false, bool mind = false)
    {
        return when switch
        {
            "" => true,
            "Foreign" => foreign && !state.Severed,
            "Rot" => rot && !state.Severed,
            "Severed" => state.Severed,
            "Infection" => infected && !state.Severed,
            "OpenFracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone && (state.SkinBare || state.Flesh >= LimbWoundRules.StrainFlesh),
            "ClosedFracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone && !state.SkinBare && state.Flesh < LimbWoundRules.StrainFlesh,
            "OpenPelvis" => !state.Severed && state.Bone >= LimbWoundRules.BreakBone && (state.SkinBare || state.Flesh >= LimbWoundRules.StrainFlesh),
            "ClosedPelvis" => !state.Severed && state.Bone >= LimbWoundRules.BreakBone && !state.SkinBare && state.Flesh < LimbWoundRules.StrainFlesh,
            "Shard" => !state.Severed && state.Embedded,
            "Bleed" => !state.Severed && state.Bleed >= 1f,
            "Fracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone,
            "Crack" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.StrainBone && state.Bone < LimbWoundRules.BreakBone,
            "OrganCrack" => organCrack && !state.Severed,
            "MindRupture" => mind && !state.Severed,
            "Bruised" => !state.Severed && state.HitBlunt > TraumaMin,
            "Lacerated" => !state.Severed && (state.HitSlash > TraumaMin || OpenWound(state) && state.HitSlash >= state.HitPierce),
            "Punctured" => !state.Severed && (state.HitPierce > TraumaMin || OpenWound(state) && state.HitPierce > state.HitSlash),
            "Worn" => pool > 0f && !state.Severed && !state.Atrophied && state.Health < pool * WornBelow,
            "Scaffold" => !hasBone && !state.Severed && state.Flesh >= LimbWoundRules.StrainFlesh,
            "Skin" => !state.Severed && (state.SkinBare || state.Skin >= LimbWoundRules.BurnSecond),
            "Damaged" => !state.Severed && (state.Atrophied || state.Flesh >= LimbWoundRules.StrainFlesh || state.Bone >= LimbWoundRules.StrainBone || state.Skin >= LimbWoundRules.BurnFirst || state.Bleed >= 0.5f),
            _ => false,
        };
    }

    private static bool OpenWound(in LimbZoneState state)
    {
        return state.Flesh >= LimbWoundRules.StrainFlesh;
    }

    public static bool MindTorn(IReadOnlyList<LimbOrganState> organs, LimbZone zone, LimbOrganMask present)
    {
        var organ = zone == LimbZone.Head ? LimbOrgan.Brain
            : zone == LimbZone.Torso ? LimbOrgan.Core
            : (LimbOrgan?) null;
        if (organ == null)
            return false;

        var index = (int) organ.Value;
        if ((present & (LimbOrganMask) (1 << index)) == 0 || (uint) index >= (uint) organs.Count)
            return false;

        var state = organs[index];
        return !state.Missing && state.Ruptured && LimbOrganHomes.Home(organ.Value) == zone;
    }

    public static bool OrganCracked(IReadOnlyList<LimbOrganState> organs, LimbZone zone)
    {
        for (var i = 0; i < organs.Count && i < LimbOrganRules.Count; i++)
        {
            if (Mendable((LimbOrgan) i, zone, organs[i]))
                return true;
        }

        return false;
    }

    public static bool Mendable(LimbOrgan organ, LimbZone zone, in LimbOrganState state)
    {
        return organ != LimbOrgan.Eyes
               && LimbOrganHomes.Home(organ) == zone
               && !state.Missing
               && !state.Ruptured
               && state.Damage >= OrganCrackMin;
    }
}

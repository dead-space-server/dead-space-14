// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.Surgery;

public static class SurgeryConditions
{
    public const float OrganCrackMin = 5f;
    public const float TraumaMin = 50f;

    public static readonly string[] Order =
    {
        "Severed",
        "Infection",
        "OpenFracture",
        "Shard",
        "Bleed",
        "ClosedFracture",
        "Fracture",
        "OrganCrack",
        "Bruised",
        "Lacerated",
        "Punctured",
        "Scaffold",
        "Skin",
        "Damaged",
    };

    public static bool Has(string when, in LimbZoneState state, bool hasBone, bool infected, bool organCrack = false)
    {
        return when switch
        {
            "" => true,
            "Severed" => state.Severed,
            "Infection" => infected && !state.Severed,
            "OpenFracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone && (state.SkinBare || state.Flesh >= LimbWoundRules.StrainFlesh),
            "ClosedFracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone && !state.SkinBare && state.Flesh < LimbWoundRules.StrainFlesh,
            "Shard" => !state.Severed && state.Embedded,
            "Bleed" => !state.Severed && state.Bleed >= 1f,
            "Fracture" => hasBone && !state.Severed && state.Bone >= LimbWoundRules.BreakBone,
            "OrganCrack" => organCrack && !state.Severed,
            "Bruised" => !state.Severed && state.HitBlunt > TraumaMin,
            "Lacerated" => !state.Severed && state.HitSlash > TraumaMin,
            "Punctured" => !state.Severed && state.HitPierce > TraumaMin,
            "Scaffold" => !hasBone && !state.Severed && state.Flesh >= LimbWoundRules.StrainFlesh,
            "Skin" => !state.Severed && (state.SkinBare || state.Skin >= LimbWoundRules.BurnSecond),
            "Damaged" => !state.Severed && (state.Atrophied || state.Flesh >= LimbWoundRules.StrainFlesh || state.Bone >= LimbWoundRules.StrainBone || state.Skin >= LimbWoundRules.BurnFirst || state.Bleed >= 0.5f),
            _ => false,
        };
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

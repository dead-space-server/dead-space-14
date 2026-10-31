using Content.Shared.Humanoid;

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbWoundVisualRules
{
    public const string Brute = "Brute";
    public const string Burn = "Burn";

    public const string Bandage = "Bandage";

    public static readonly LimbZone[] Zones =
    {
        LimbZone.Head,
        LimbZone.Torso,
        LimbZone.Groin,
        LimbZone.ArmLeft,
        LimbZone.ArmRight,
        LimbZone.LegLeft,
        LimbZone.LegRight,
        LimbZone.HandLeft,
        LimbZone.HandRight,
        LimbZone.FootLeft,
        LimbZone.FootRight,
    };

    public static HumanoidVisualLayers? Layer(LimbZone zone) => zone switch
    {
        LimbZone.Head => HumanoidVisualLayers.Head,
        LimbZone.Torso => HumanoidVisualLayers.Chest,
        LimbZone.Groin => HumanoidVisualLayers.Chest,
        LimbZone.ArmLeft => HumanoidVisualLayers.LArm,
        LimbZone.ArmRight => HumanoidVisualLayers.RArm,
        LimbZone.LegLeft => HumanoidVisualLayers.LLeg,
        LimbZone.LegRight => HumanoidVisualLayers.RLeg,
        LimbZone.HandLeft => HumanoidVisualLayers.LHand,
        LimbZone.HandRight => HumanoidVisualLayers.RHand,
        LimbZone.FootLeft => HumanoidVisualLayers.LFoot,
        LimbZone.FootRight => HumanoidVisualLayers.RFoot,
        _ => null,
    };

    public static LimbZone Fallback(LimbZone zone) => zone switch
    {
        LimbZone.Groin => LimbZone.Torso,
        LimbZone.HandLeft => LimbZone.ArmLeft,
        LimbZone.HandRight => LimbZone.ArmRight,
        LimbZone.FootLeft => LimbZone.LegLeft,
        LimbZone.FootRight => LimbZone.LegRight,
        _ => zone,
    };

    public static string Prefix(LimbZone zone) => zone switch
    {
        LimbZone.Head => "Head",
        LimbZone.Torso => "Chest",
        LimbZone.Groin => "Groin",
        LimbZone.ArmLeft => "LArm",
        LimbZone.ArmRight => "RArm",
        LimbZone.LegLeft => "LLeg",
        LimbZone.LegRight => "RLeg",
        LimbZone.HandLeft => "LHand",
        LimbZone.HandRight => "RHand",
        LimbZone.FootLeft => "LFoot",
        LimbZone.FootRight => "RFoot",
        _ => "Chest",
    };

    public static float Severity(string group, LimbZone zone, in LimbZoneState state)
    {
        var pool = LimbWoundRules.HealthFor(zone);
        if (pool <= 0f)
            return 0f;

        float value;
        switch (group)
        {
            case Brute:
                value = MathF.Max(MathF.Max(state.Flesh, state.Bone),
                    MathF.Max(MathF.Max(state.Cut, state.Pierce), state.Sever));
                break;
            case Burn:
                value = MathF.Max(MathF.Max(state.Skin, state.Heat), state.Caustic);
                break;
            default:
                return 0f;
        }

        return Math.Clamp(value / pool, 0f, 1f);
    }

    public static bool BodyVisible(in LimbZoneState state)
    {
        return !state.Severed;
    }

    public static int Stage(float fraction, IReadOnlyList<float> thresholds)
    {
        var stage = 0;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (fraction < thresholds[i])
                break;

            stage = i + 1;
        }

        return stage;
    }

    public static int BandageStage(in LimbZoneState state) => state.Bandaged ? 1 : 0;
}

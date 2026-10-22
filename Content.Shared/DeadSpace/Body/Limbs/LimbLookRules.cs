// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;

namespace Content.Shared.DeadSpace.Body.Limbs;

public readonly record struct LimbLookSlot(LimbZone Zone, HumanoidVisualLayers Layer, bool NeedsEyes);

public static class LimbLookRules
{
    public static readonly LimbLookSlot[] Slots =
    {
        new(LimbZone.Head, HumanoidVisualLayers.Hair, false),
        new(LimbZone.Head, HumanoidVisualLayers.FacialHair, false),
        new(LimbZone.Head, HumanoidVisualLayers.Head, false),
        new(LimbZone.Head, HumanoidVisualLayers.HeadTop, false),
        new(LimbZone.Head, HumanoidVisualLayers.HeadSide, false),
        new(LimbZone.Head, HumanoidVisualLayers.Snout, false),
        new(LimbZone.Head, HumanoidVisualLayers.SnoutCover, false),
        new(LimbZone.Head, HumanoidVisualLayers.Special, false),
        new(LimbZone.Head, HumanoidVisualLayers.Eyes, true),
        new(LimbZone.Torso, HumanoidVisualLayers.Chest, false),
        new(LimbZone.Torso, HumanoidVisualLayers.Tail, false),
        new(LimbZone.ArmLeft, HumanoidVisualLayers.LArm, false),
        new(LimbZone.ArmRight, HumanoidVisualLayers.RArm, false),
        new(LimbZone.HandLeft, HumanoidVisualLayers.LHand, false),
        new(LimbZone.HandRight, HumanoidVisualLayers.RHand, false),
        new(LimbZone.LegLeft, HumanoidVisualLayers.LLeg, false),
        new(LimbZone.LegRight, HumanoidVisualLayers.RLeg, false),
        new(LimbZone.FootLeft, HumanoidVisualLayers.LFoot, false),
        new(LimbZone.FootRight, HumanoidVisualLayers.RFoot, false),
    };

    public static bool Fits(LimbZone zone, HumanoidVisualLayers layer, bool eyesRemain)
    {
        foreach (var slot in Slots)
        {
            if (slot.Zone != zone || slot.Layer != layer)
                continue;

            return !slot.NeedsEyes || eyesRemain;
        }

        return false;
    }

    public static void Collect(LimbZone zone, List<HumanoidVisualLayers> layers)
    {
        foreach (var slot in Slots)
        {
            if (slot.Zone == zone)
                layers.Add(slot.Layer);
        }

        if (LimbWoundRules.TryCarried(zone, out var child))
            Collect(child, layers);
    }
}

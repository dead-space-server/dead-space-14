// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Body.Limbs;

public enum LimbDollShapeKind : byte
{
    Circle,
    Box,
}

public readonly record struct LimbDollShape(LimbZone Zone, LimbDollShapeKind Kind, float X, float Y, float W, float H);

public readonly record struct LimbDollPart(LimbZone Zone, string Name, string Short, int Order);

public readonly record struct LimbDollIcon(LimbZone Zone, string Body, string Hot, string Health, string Lost);

public static class LimbDollPalette
{
    public static readonly Color Backdrop = Color.FromHex("#14181d");
    public static readonly Color Line = Color.FromHex("#c5ced6");
    public static readonly Color Hot = Color.FromHex("#e23b3b");
    public static readonly Color HotLine = Color.FromHex("#ffd2d2");
    public static readonly Color Idle = Color.FromHex("#2c3440");
    public static readonly Color Picked = Color.FromHex("#c43838");
    public static readonly Color Hover = Color.FromHex("#e23b3b");

    public const int RadialRadius = 96;
    public const int Corner = 8;
    public const int AboveHotbar = 88;
}

public static class LimbDollLayout
{
    public const float Width = 32f;
    public const float Height = 32f;
    public const string Texture = "/Textures/_DeadSpace/Interface/LimbDoll/zone.rsi";
    public const string Block = "block";

    public const string ClearShort = "limb-doll-short-clear";
    public const string EmptyName = "limb-doll-empty";

    public static readonly LimbDollPart[] Parts =
    {
        new(LimbZone.Head, "limb-target-head", "limb-doll-short-head", 0),
        new(LimbZone.Eyes, "limb-target-eyes", "limb-doll-short-eyes", 1),
        new(LimbZone.Mouth, "limb-target-mouth", "limb-doll-short-mouth", 2),
        new(LimbZone.Torso, "limb-target-torso", "limb-doll-short-torso", 3),
        new(LimbZone.Groin, "limb-target-groin", "limb-doll-short-groin", 4),
        new(LimbZone.ArmLeft, "limb-target-arm-left", "limb-doll-short-arm-left", 5),
        new(LimbZone.HandLeft, "limb-target-hand-left", "limb-doll-short-hand-left", 6),
        new(LimbZone.LegLeft, "limb-target-leg-left", "limb-doll-short-leg-left", 7),
        new(LimbZone.FootLeft, "limb-target-foot-left", "limb-doll-short-foot-left", 8),
        new(LimbZone.ArmRight, "limb-target-arm-right", "limb-doll-short-arm-right", 9),
        new(LimbZone.HandRight, "limb-target-hand-right", "limb-doll-short-hand-right", 10),
        new(LimbZone.LegRight, "limb-target-leg-right", "limb-doll-short-leg-right", 11),
        new(LimbZone.FootRight, "limb-target-foot-right", "limb-doll-short-foot-right", 12),
    };

    public static readonly LimbDollIcon[] Icons =
    {
        new(LimbZone.Head, "head_idle", "head", "head", "headDEAD"),
        new(LimbZone.Torso, "chest_idle", "chest", "chest", "chestDEAD"),
        new(LimbZone.ArmRight, "r_arm_idle", "r_arm", "r_arm", "r_armDEAD"),
        new(LimbZone.ArmLeft, "l_arm_idle", "l_arm", "l_arm", "l_armDEAD"),
        new(LimbZone.LegRight, "r_leg_idle", "r_leg", "r_leg", "r_legDEAD"),
        new(LimbZone.LegLeft, "l_leg_idle", "l_leg", "l_leg", "l_legDEAD"),
    };

    public static readonly LimbDollIcon[] Marks =
    {
        new(LimbZone.Eyes, "eyes_idle", "eyes", "", ""),
        new(LimbZone.Mouth, "mouth_idle", "mouth", "", ""),
        new(LimbZone.Groin, "groin_idle", "groin", "", ""),
    };

    public static readonly LimbDollShape[] Shapes =
    {
        new(LimbZone.Eyes, LimbDollShapeKind.Box, 13f, 5f, 5f, 3f),
        new(LimbZone.Mouth, LimbDollShapeKind.Box, 14f, 7f, 3f, 3f),
        new(LimbZone.Groin, LimbDollShapeKind.Box, 11f, 19f, 9f, 4f),
        new(LimbZone.ArmRight, LimbDollShapeKind.Box, 7f, 11f, 5f, 11f),
        new(LimbZone.ArmLeft, LimbDollShapeKind.Box, 19f, 11f, 5f, 11f),
        new(LimbZone.LegRight, LimbDollShapeKind.Box, 9f, 23f, 6f, 9f),
        new(LimbZone.LegLeft, LimbDollShapeKind.Box, 16f, 23f, 6f, 9f),
        new(LimbZone.Torso, LimbDollShapeKind.Box, 11f, 10f, 9f, 9f),
        new(LimbZone.Head, LimbDollShapeKind.Box, 11f, 3f, 9f, 7f),
    };

    public static Vector2 Center(LimbDollShape shape)
    {
        if (shape.Kind == LimbDollShapeKind.Circle)
            return new Vector2(shape.X, shape.Y);

        return new Vector2(shape.X + shape.W / 2f, shape.Y + shape.H / 2f);
    }

    public static bool TryPart(LimbZone zone, out LimbDollPart part)
    {
        foreach (var candidate in Parts)
        {
            if (candidate.Zone != zone)
                continue;

            part = candidate;
            return true;
        }

        part = default;
        return false;
    }

    public static bool Contains(LimbDollShape shape, Vector2 point)
    {
        switch (shape.Kind)
        {
            case LimbDollShapeKind.Circle:
                var dx = point.X - shape.X;
                var dy = point.Y - shape.Y;
                return dx * dx + dy * dy <= shape.W * shape.W;
            default:
                return point.X >= shape.X
                       && point.Y >= shape.Y
                       && point.X < shape.X + shape.W
                       && point.Y < shape.Y + shape.H;
        }
    }

    public static LimbZone? Hit(Vector2 point)
    {
        foreach (var shape in Shapes)
        {
            if (Contains(shape, point))
                return shape.Zone;
        }

        return null;
    }

    public static readonly LimbDollShape[] SpriteHits =
    {
        new(LimbZone.Eyes, LimbDollShapeKind.Circle, 12.5f, 8f, 1.6f, 0f),
        new(LimbZone.Eyes, LimbDollShapeKind.Circle, 19.5f, 8f, 1.6f, 0f),
        new(LimbZone.Mouth, LimbDollShapeKind.Box, 14f, 10.5f, 4f, 2f),
        new(LimbZone.HandRight, LimbDollShapeKind.Box, 6f, 18f, 6f, 5f),
        new(LimbZone.HandLeft, LimbDollShapeKind.Box, 19f, 18f, 6f, 5f),
        new(LimbZone.FootRight, LimbDollShapeKind.Box, 8f, 29f, 7f, 3f),
        new(LimbZone.FootLeft, LimbDollShapeKind.Box, 16f, 29f, 7f, 3f),
        new(LimbZone.ArmRight, LimbDollShapeKind.Box, 6f, 10f, 6f, 8f),
        new(LimbZone.ArmLeft, LimbDollShapeKind.Box, 19f, 10f, 6f, 8f),
        new(LimbZone.LegRight, LimbDollShapeKind.Box, 10f, 22f, 5f, 7f),
        new(LimbZone.LegLeft, LimbDollShapeKind.Box, 16f, 22f, 5f, 7f),
        new(LimbZone.Head, LimbDollShapeKind.Box, 11f, 3f, 10f, 10f),
        new(LimbZone.Torso, LimbDollShapeKind.Box, 11f, 12f, 10f, 6f),
        new(LimbZone.Groin, LimbDollShapeKind.Box, 11f, 18f, 10f, 4f),
    };

    public static LimbZone? SpriteHit(Vector2 pixel)
    {
        foreach (var shape in SpriteHits)
        {
            if (Contains(shape, pixel))
                return shape.Zone;
        }

        return null;
    }

    public static string HealthState(LimbDollIcon icon, int tone)
    {
        return tone < 0 || icon.Health.Length == 0 ? icon.Lost : icon.Health + tone;
    }

    public static bool TryIcon(LimbZone zone, out LimbDollIcon icon)
    {
        foreach (var candidate in Icons)
        {
            if (candidate.Zone != zone)
                continue;

            icon = candidate;
            return true;
        }

        foreach (var candidate in Marks)
        {
            if (candidate.Zone != zone)
                continue;

            icon = candidate;
            return true;
        }

        icon = default;
        return false;
    }
}

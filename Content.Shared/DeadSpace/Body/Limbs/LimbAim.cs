// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Body.Limbs;

public readonly record struct LimbAimBand(
    LimbZone Zone,
    float XFrom,
    float XTo,
    float HeightFrom,
    float HeightTo,
    bool XFromOpen = false,
    bool HeightFromOpen = false)
{
    public bool Holds(float x, float height)
    {
        if (XFromOpen ? x <= XFrom : x < XFrom)
            return false;

        if (x >= XTo)
            return false;

        if (HeightFromOpen ? height <= HeightFrom : height < HeightFrom)
            return false;

        return height < HeightTo;
    }
}

public readonly record struct LimbAimNear(LimbZone Called, LimbZone Natural);

public static class LimbAim
{
    public const float Hip = 0.24f;
    public const float Circle = 0.35f;
    public const float SpriteHalf = 0.5f;

    public static readonly LimbAimBand[] Shot =
    {
        new(LimbZone.Head, float.NegativeInfinity, float.PositiveInfinity, 0.52f, float.PositiveInfinity, HeightFromOpen: true),
        new(LimbZone.FootRight, 0f, float.PositiveInfinity, float.NegativeInfinity, -0.38f),
        new(LimbZone.FootLeft, float.NegativeInfinity, 0f, float.NegativeInfinity, -0.38f),
        new(LimbZone.LegRight, 0f, float.PositiveInfinity, float.NegativeInfinity, -0.16f),
        new(LimbZone.LegLeft, float.NegativeInfinity, 0f, float.NegativeInfinity, -0.16f),
        new(LimbZone.Groin, -0.32f, 0.32f, -0.16f, 0.12f),
        new(LimbZone.HandRight, 0.32f, float.PositiveInfinity, float.NegativeInfinity, 0.08f, XFromOpen: true),
        new(LimbZone.ArmRight, 0.32f, float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity, XFromOpen: true),
        new(LimbZone.HandLeft, float.NegativeInfinity, -0.32f, float.NegativeInfinity, 0.08f),
        new(LimbZone.ArmLeft, float.NegativeInfinity, -0.32f, float.NegativeInfinity, float.PositiveInfinity),
    };

    public static readonly LimbAimBand[] Doll =
    {
        new(LimbZone.Head, float.NegativeInfinity, float.PositiveInfinity, 0.35f, float.PositiveInfinity, HeightFromOpen: true),
        new(LimbZone.FootRight, 0f, float.PositiveInfinity, float.NegativeInfinity, -0.7f),
        new(LimbZone.FootLeft, float.NegativeInfinity, 0f, float.NegativeInfinity, -0.7f),
        new(LimbZone.LegRight, 0f, float.PositiveInfinity, float.NegativeInfinity, -0.35f),
        new(LimbZone.LegLeft, float.NegativeInfinity, 0f, float.NegativeInfinity, -0.35f),
        new(LimbZone.Groin, -0.45f, 0.45f, -0.35f, -0.05f),
        new(LimbZone.HandRight, 0.45f, float.PositiveInfinity, 0f, float.PositiveInfinity, XFromOpen: true, HeightFromOpen: true),
        new(LimbZone.ArmRight, 0.45f, float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity, XFromOpen: true),
        new(LimbZone.HandLeft, float.NegativeInfinity, -0.45f, 0f, float.PositiveInfinity, HeightFromOpen: true),
        new(LimbZone.ArmLeft, float.NegativeInfinity, -0.45f, float.NegativeInfinity, float.PositiveInfinity),
    };

    public static readonly LimbAimNear[] Near =
    {
        new(LimbZone.Torso, LimbZone.ArmLeft),
        new(LimbZone.Torso, LimbZone.ArmRight),
        new(LimbZone.Torso, LimbZone.Groin),
        new(LimbZone.Groin, LimbZone.Torso),
        new(LimbZone.Groin, LimbZone.LegLeft),
        new(LimbZone.Groin, LimbZone.LegRight),
        new(LimbZone.LegLeft, LimbZone.Groin),
        new(LimbZone.LegRight, LimbZone.Groin),
        new(LimbZone.Head, LimbZone.Torso),
        new(LimbZone.ArmLeft, LimbZone.Torso),
        new(LimbZone.ArmLeft, LimbZone.HandLeft),
        new(LimbZone.ArmRight, LimbZone.Torso),
        new(LimbZone.ArmRight, LimbZone.HandRight),
        new(LimbZone.LegLeft, LimbZone.Torso),
        new(LimbZone.LegLeft, LimbZone.FootLeft),
        new(LimbZone.LegRight, LimbZone.Torso),
        new(LimbZone.LegRight, LimbZone.FootRight),
        new(LimbZone.HandLeft, LimbZone.ArmLeft),
        new(LimbZone.HandRight, LimbZone.ArmRight),
        new(LimbZone.FootLeft, LimbZone.LegLeft),
        new(LimbZone.FootRight, LimbZone.LegRight),
        new(LimbZone.Eyes, LimbZone.Head),
        new(LimbZone.Eyes, LimbZone.Torso),
        new(LimbZone.Mouth, LimbZone.Head),
        new(LimbZone.Mouth, LimbZone.Torso),
    };

    public static Vector2 Landing(Vector2 from, Vector2 hit, Vector2 aim, Vector2 target)
    {
        var aimed = aim - target;
        if (aimed.LengthSquared() <= 1f)
            return aimed;

        var travel = hit - from;
        if (MathF.Abs(travel.Y) > MathF.Abs(travel.X) || travel.LengthSquared() < 0.0001f)
            return aimed;

        var local = hit - target;
        if (local.LengthSquared() < 0.0001f)
            return local;

        return local * (SpriteHalf / Circle);
    }

    public static Vector2 Body(Vector2 local, Direction facing)
    {
        var forward = facing.ToVec();
        var right = new Vector2(forward.Y, -forward.X);
        return new Vector2(Vector2.Dot(local, right), local.Y);
    }

    public static LimbZone Zone(float x, float y, LimbAimBand[] bands, float lift)
    {
        var height = y + lift;
        foreach (var band in bands)
        {
            if (band.Holds(x, height))
                return band.Zone;
        }

        return LimbZone.Torso;
    }

    public static bool Covers(LimbZone called, LimbZone natural)
    {
        if (called == natural)
            return true;

        foreach (var near in Near)
        {
            if (near.Called == called && near.Natural == natural)
                return true;
        }

        return false;
    }
}

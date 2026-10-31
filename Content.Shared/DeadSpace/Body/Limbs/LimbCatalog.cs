// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbHooks
{
    public const string Organ = "organ";
    public const string Concussion = "concussion";
    public const string Crit = "crit";
    public const string Air = "air";
    public const string Blind = "blind";
    public const string Whistle = "whistle";
    public const string Rib = "rib";
}

public readonly record struct LimbRow(
    LimbZone? Zone,
    LimbMark? Mark,
    string Hook,
    float Factor = 1f,
    LimbOrgan Organ = default,
    bool ProjectileOnly = false);

public static class LimbCatalog
{
    public static readonly LimbRow[] Rows =
    {
        new(LimbZone.Head, LimbMark.Blunt, LimbHooks.Organ, 1f, LimbOrgan.Brain),
        new(LimbZone.Head, LimbMark.Blunt, LimbHooks.Concussion),
        new(LimbZone.Head, LimbMark.Point, LimbHooks.Organ, 1.25f, LimbOrgan.Brain),
        new(LimbZone.Head, LimbMark.Point, LimbHooks.Concussion),
        new(LimbZone.Head, LimbMark.Edge, LimbHooks.Organ, 0.4f, LimbOrgan.Brain),
        new(LimbZone.Eyes, LimbMark.Blunt, LimbHooks.Organ, 0.7f, LimbOrgan.Eyes),
        new(LimbZone.Eyes, LimbMark.Point, LimbHooks.Organ, 1f, LimbOrgan.Eyes),
        new(LimbZone.Eyes, LimbMark.Point, LimbHooks.Blind, ProjectileOnly: true),
        new(LimbZone.Eyes, LimbMark.Edge, LimbHooks.Organ, 1f, LimbOrgan.Eyes),
        new(LimbZone.Eyes, LimbMark.Heat, LimbHooks.Organ, 0.45f, LimbOrgan.Eyes),
        new(LimbZone.Eyes, LimbMark.Liquid, LimbHooks.Organ, 0.45f, LimbOrgan.Eyes),
        new(LimbZone.Mouth, LimbMark.Liquid, LimbHooks.Organ, 1f, LimbOrgan.Liver),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Organ, 1f, LimbOrgan.Heart),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Organ, 0.8f, LimbOrgan.Lungs),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Crit),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Whistle),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Rib, 0.45f),
        new(LimbZone.Torso, LimbMark.Edge, LimbHooks.Organ, 0.8f, LimbOrgan.Lungs),
        new(LimbZone.Torso, LimbMark.Edge, LimbHooks.Air),
        new(LimbZone.Torso, LimbMark.Edge, LimbHooks.Rib, 0.25f),
        new(LimbZone.Torso, LimbMark.Blunt, LimbHooks.Organ, 0.85f, LimbOrgan.Kidneys),
        new(LimbZone.Torso, LimbMark.Blunt, LimbHooks.Organ, 0.4f, LimbOrgan.Stomach),
        new(LimbZone.Torso, LimbMark.Blunt, LimbHooks.Organ, 0.6f, LimbOrgan.Lungs),
        new(LimbZone.Torso, LimbMark.Blunt, LimbHooks.Organ, 1f, LimbOrgan.Core),
        new(LimbZone.Torso, LimbMark.Point, LimbHooks.Organ, 1f, LimbOrgan.Core),
        new(LimbZone.Torso, LimbMark.Liquid, LimbHooks.Organ, 0.5f, LimbOrgan.Core),
        new(LimbZone.Groin, LimbMark.Point, LimbHooks.Organ, 1f, LimbOrgan.Intestines),
        new(LimbZone.Groin, LimbMark.Edge, LimbHooks.Organ, 0.8f, LimbOrgan.Intestines),
        new(LimbZone.Groin, LimbMark.Blunt, LimbHooks.Organ, 0.5f, LimbOrgan.Intestines),
        new(LimbZone.Groin, LimbMark.Liquid, LimbHooks.Organ, 0.6f, LimbOrgan.Intestines),
    };

    public static bool Fits(in LimbRow row, LimbZone zone, LimbMark mark, bool projectile)
    {
        if (row.Zone is { } wantedZone && wantedZone != zone)
            return false;

        if (row.Mark is { } wantedMark && wantedMark != mark)
            return false;

        return !row.ProjectileOnly || projectile;
    }
}

[ByRefEvent]
public readonly record struct LimbHookEvent(string Hook, LimbZone Zone, LimbMark Mark, float Amount, LimbOrgan Organ);

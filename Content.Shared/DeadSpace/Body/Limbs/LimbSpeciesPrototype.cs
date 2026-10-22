// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

[Prototype]
public sealed partial class LimbSpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<LimbOrgan> Organs = new();

    [DataField]
    public float Wound = 1f;

    [DataField]
    public float LiquidSever;
}

public static class LimbSpeciesRules
{
    public static readonly Color Bare = Color.FromHex("#8a8a8a");

    public static LimbOrganMask Mask(IReadOnlyList<LimbOrgan> organs)
    {
        var mask = LimbOrganMask.None;
        foreach (var organ in organs)
            mask |= (LimbOrganMask) (1 << (int) organ);

        return mask;
    }

    public static Color Look(bool bare, Color skin)
    {
        return bare ? Bare : skin;
    }
}

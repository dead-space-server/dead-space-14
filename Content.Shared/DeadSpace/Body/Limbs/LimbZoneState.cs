// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[DataDefinition, Serializable, NetSerializable]
public partial struct LimbZoneState
{
    [DataField]
    public float Flesh;

    [DataField]
    public float Bone;

    [DataField]
    public float Cut;

    [DataField]
    public float Pierce;

    [DataField]
    public float Heat;

    [DataField]
    public float Caustic;

    [DataField]
    public float Skin;

    [DataField]
    public float Sever;

    [DataField]
    public int Scar;

    [DataField]
    public float Fire;

    [DataField]
    public float Cloth;

    [DataField]
    public float Bleed;

    [DataField]
    public bool Bandaged;

    [DataField]
    public bool Splinted;

    [DataField]
    public bool Severed;

    [DataField]
    public bool SkinBare;

    [DataField]
    public bool Atrophied;

    [DataField]
    public float Health = LimbWoundRules.LimbHealth;

    [DataField]
    public float HitBlunt;

    [DataField]
    public float HitSlash;

    [DataField]
    public float HitPierce;

    [DataField]
    public float HitHeat;

    [DataField]
    public float HitCaustic;

    [DataField]
    public float HitBlood;

    [DataField]
    public float HitAir;

    [DataField]
    public bool Embedded;

    public static List<LimbZoneState> CreateAll()
    {
        var list = new List<LimbZoneState>(LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < LimbWoundRules.BodyZoneCount; i++)
            list.Add(new LimbZoneState { Health = LimbWoundRules.HealthFor((LimbZone) i) });

        return list;
    }
}

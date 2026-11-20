// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.Body.Limbs;

public struct LimbEdit
{
    public float Flesh;
    public float Bone;
    public float Skin;
    public float Bleed;
    public int Scar;
    public float Health;
    public bool SetHealth;
    public bool SetBone;
    public bool SetFlesh;
    public bool SetBleed;
    public bool SetEmbedded;
    public bool Embedded;
    public bool ClearAtrophy;
    public bool SetAtrophy;
    public bool ClearEmbedded;
    public bool ClearSevered;
    public bool ClearSkinBare;
    public bool ClearBurn;
}

public struct LimbOrganEdit
{
    public float Damage;
    public bool SetDamage;
    public bool Ruptured;
    public bool SetRuptured;
    public bool BleedStopped;
    public bool SetBleedStopped;
    public bool Missing;
    public bool SetMissing;
    public int EyesGone;
}

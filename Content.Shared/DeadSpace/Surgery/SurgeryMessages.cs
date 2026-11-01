// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public sealed partial class SurgeryStepDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public byte Verb;

    [DataField]
    public byte Organ = 255;
}

[Serializable, NetSerializable]
public sealed class SurgeryBoardMessage : EntityEventArgs
{
    public NetEntity Patient;
    public byte Zone;
    public float Wobble;
    public float Lag;
    public float SurfaceSlip;
    public float BoneFloor;
    public byte Asleep;
    public string Line = string.Empty;
    public byte[] Verbs = Array.Empty<byte>();
    public byte[] LayerKinds = Array.Empty<byte>();
    public float[] Integrity = Array.Empty<float>();
    public byte[] Retracted = Array.Empty<byte>();
    public byte[] Organs = Array.Empty<byte>();
    public byte[] Empty = Array.Empty<byte>();
    public byte[] Examined = Array.Empty<byte>();
    public byte Play = 1;
    public byte Embedded;
    public byte StepVerb = 255;
    public byte StepLayer = 255;
    public byte Chosen = 255;
    public byte StepGame = 255;
    public string Part = "";
    public byte CloseVerb = 255;
    public byte UndoVerb = 255;
    public byte SawHits = 3;
    public byte Drill;
    public byte Across;
    public byte Finished;
    public string Lodged = "";
    public NetEntity LodgedItem;
    public string StepHint = "";
    public byte Bleeds;
}

[Serializable, NetSerializable]
public sealed class SurgeryCutMessage : EntityEventArgs
{
    public NetEntity Patient;
    public byte Verb;
    public byte Layer;
    public byte Organ = 255;
    public float Progress;
    public byte Slips;
    public byte Yank;
}

[Serializable, NetSerializable]
public sealed class SurgeryZoneMessage : EntityEventArgs
{
    public NetEntity Patient;
    public byte Zone;
    public string Part = "";
    public string Operation = "";
}

[Serializable, NetSerializable]
public sealed class SurgeryCancelMessage : EntityEventArgs
{
    public NetEntity Patient;
}

[Serializable, NetSerializable]
public sealed class SurgeryPickMessage : EntityEventArgs
{
    public NetEntity Patient;
    public byte Organ;
}

[Serializable, NetSerializable]
public sealed class SurgeryFocusMessage : EntityEventArgs
{
    public byte On;
}

public struct SurgeryRequest
{
    public SurgeryVerbKind Verb;
    public LimbZone Zone;
    public SurgeryLayerKind Kind;
    public float Integrity;
    public bool Retracted;
    public bool Intact;
    public bool DeeperCut;
    public bool CavityReady;
    public bool LayersOpen;
    public float Progress;
    public int Slips;
    public bool Yank;
    public float ToolPrecision;
    public float ToolSlip;
    public float SurfaceSlip;
    public float BoneFloor;
    public float CurrentBone;
    public float CurrentFlesh;
    public float CurrentBleed;
    public float CurrentSkin;
    public bool SkinBare;
    public bool Atrophied;
    public bool Embedded;
    public int Scar;
    public bool Implant;
}

public sealed partial class SurgeryBitePillEvent : InstantActionEvent
{
}

public struct SurgeryResult
{
    public float Integrity;
    public bool Retracted;
    public float NextLoss;
    public float Bleed;
    public float Flesh;
    public float Bone;
    public float Skin;
    public int Scar;
    public bool SetHealth;
    public float Health;
    public bool ClearAtrophy;
    public bool ClearEmbedded;
    public bool ClearSkinBare;
    public bool Sever;
    public bool Attach;
    public bool Finished;
    public bool Crude;
    public bool RemoveOrgan;
    public bool PlaceOrgan;
    public bool RemoveImplant;
    public bool PlaceImplant;
    public bool Rupture;
    public float OrganDamage;
    public int Eyes;
    public bool Examined;
    public float Germs;
    public bool Gel;
    public bool Rejected;
    public string Key;
}

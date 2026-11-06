// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[Prototype]
public sealed partial class SurgeryAnatomyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<SurgeryLayerKind> Layers = new();

    [DataField]
    public ProtoId<SurgeryOperationPrototype>? Operation;
}

[Prototype]
public sealed partial class SurgeryMarkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Prefix = string.Empty;

    [DataField]
    public float Ax;

    [DataField]
    public float Ay;

    [DataField]
    public float Bx;

    [DataField]
    public float By;

    [DataField]
    public float Cx;

    [DataField]
    public float Cy;

    [DataField]
    public float Dx;

    [DataField]
    public float Dy;

    [DataField]
    public int Hx;

    [DataField]
    public int Hy;

    [DataField]
    public int Hw;

    [DataField]
    public int Hh;

    [DataField]
    public List<SurgeryVerbKind> Start = new();

    [DataField]
    public bool Self = true;

    [DataField]
    public bool Ribs;

    [DataField]
    public bool Brain;

    [DataField]
    public bool Halves;

    public static bool SelfAllowed(IPrototypeManager prototypes, LimbZone zone, string? operation)
    {
        if (!prototypes.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark) || mark.Self)
            return true;

        return operation != null
               && prototypes.TryIndex<SurgeryOperationPrototype>(operation, out var proto)
               && proto.Self;
    }
}

[Prototype]
public sealed partial class SurgeryMissPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}

[Prototype]
public sealed partial class SurgeryHintPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<SurgeryHintIcon> Icons = new();

    [DataField]
    public List<SurgeryHintIcon> MachineIcons = new();

    [DataField]
    public string Name = "";

    [DataField]
    public Color Color = Color.White;
}

[DataDefinition]
public sealed partial class SurgeryHintIcon
{
    [DataField(required: true)]
    public string Sprite = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;
}

[Prototype]
public sealed partial class SurgerySpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Limb;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Torso;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Head;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Groin;

    [DataField]
    public ProtoId<SurgeryAnatomyPrototype>? Eyes;

    [DataField]
    public bool Wounds = true;

    [DataField]
    public bool Operable = true;

    [DataField]
    public bool Graft = true;

    [DataField]
    public string Lineage = "";

    [DataField]
    public bool Machine;

    [DataField]
    public bool Painless;

    [DataField]
    public bool Soft;

    [DataField]
    public bool Plant;

    [DataField]
    public string GraftStack = "";

    [DataField]
    public List<string>? Vital;

    [DataField]
    public Dictionary<string, LocId> OrganNames = new();

    [DataField]
    public string OrganSprite = "";

    [DataField]
    public Dictionary<string, string> OrganStates = new();

    [DataField]
    public string WoundSprite = "";

    [DataField]
    public SurgeryInk Ink = SurgeryInk.Flesh;

    [DataField]
    public Dictionary<string, List<SurgeryOrganSpot>> OrganSpots = new();

    [DataField]
    public Dictionary<string, ComponentRegistry> Lacking = new();

    public static SurgeryInk InkOf(IPrototypeManager prototypes, string species)
    {
        if (!prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto))
            return SurgeryInk.Flesh;

        return proto.Machine ? SurgeryInk.Machine : proto.Ink;
    }

    public static bool IsMachine(IPrototypeManager prototypes, string species)
    {
        return prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto) && proto.Machine;
    }

    public static bool IsSoft(IPrototypeManager prototypes, string species)
    {
        return prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto) && proto.Soft;
    }

    public static bool IsPlant(IPrototypeManager prototypes, string species)
    {
        return prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto) && proto.Plant;
    }

    public static string OrganName(IPrototypeManager prototypes, string species, string organ)
    {
        if (prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto) && proto.OrganNames.TryGetValue(organ, out var name))
            return Loc.GetString(name);

        return Loc.GetString("surgery-organ-" + organ.ToLowerInvariant());
    }

    public static bool CanOperate(IPrototypeManager prototypes, string species)
    {
        return !prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto) || proto.Operable;
    }

    public static string? LineageOf(IPrototypeManager prototypes, string species)
    {
        if (!prototypes.TryIndex<SurgerySpeciesPrototype>(species, out var proto))
            return species;

        if (!proto.Operable || !proto.Graft)
            return null;

        return proto.Lineage.Length > 0 ? proto.Lineage : species;
    }
}

[Prototype]
public sealed partial class SurgeryRepairPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public DamageSpecifier Heal = new();
}

[Prototype]
public sealed partial class SurgeryGameRollPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<SurgeryGameKind> Games = new();
}

[Prototype]
public sealed partial class SurgerySoundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}

[Prototype]
public sealed partial class SurgeryGraftPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Cells = "GraftCells";

    [DataField]
    public string Mutagen = "GraftMutagen";

    [DataField]
    public float CellsKeep = 1f;

    [DataField]
    public float CellsTop = 2f;

    [DataField]
    public float Amount = 1f;

    [DataField]
    public float Early = 90f;

    [DataField]
    public float Death = 300f;

    [DataField]
    public float Black = 240f;

    [DataField]
    public float RotHealth = 0.04f;

    [DataField]
    public float Poison = 0.4f;

    [DataField]
    public Dictionary<float, string> Stages = new();

    public static string PairId(string prefix, string a, string b)
    {
        return string.CompareOrdinal(a, b) <= 0 ? prefix + a + b : prefix + b + a;
    }
}

[Prototype]
public sealed partial class SurgeryOrganPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Slot = string.Empty;

    [DataField]
    public string Zone = "Torso";

    [DataField]
    public string MachineZone = "";

    [DataField]
    public bool Vital;

    [DataField]
    public bool Pair;

    [DataField]
    public List<SurgeryOrganSpot> Spots = new();

    [DataField]
    public List<SurgeryOrganSpot> MachineSpots = new();

    [DataField]
    public List<SurgeryFilmShape> Film = new();

    public bool InZone(LimbZone zone, bool machine)
    {
        var home = machine && MachineZone.Length > 0 ? MachineZone : Zone;
        return home == zone.ToString();
    }

    public IReadOnlyList<SurgeryOrganSpot> SpotsFor(bool machine)
    {
        return machine && MachineSpots.Count > 0 ? MachineSpots : Spots;
    }
}

[DataDefinition]
public sealed partial class SurgeryOrganSpot
{
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public int X;

    [DataField]
    public int Y;

    [DataField]
    public int W = 1;

    [DataField]
    public int H = 1;

    [DataField]
    public float Cx = 0.5f;

    [DataField]
    public float Cy = 0.5f;

    [DataField]
    public float Height = 0.2f;

    [DataField]
    public int HitX;

    [DataField]
    public int HitY;

    [DataField]
    public int HitW;

    [DataField]
    public int HitH;
}

[Prototype]
public sealed partial class SurgeryPlacePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float Slip = 0.7f;

    [DataField]
    public float BoneFloor = 25f;

    [DataField]
    public float Jerk = 0.35f;
}

[Prototype]
public sealed partial class SurgeryOperationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<SurgeryStep> Steps = new();

    [DataField]
    public string Zone = "";

    [DataField]
    public string Name = "";

    [DataField]
    public List<SurgeryStep> Close = new();

    [DataField]
    public bool CloseRetract = true;

    [DataField]
    public bool Across;

    [DataField]
    public bool Field;

    [DataField]
    public bool Whole;

    [DataField]
    public bool Stump;

    [DataField]
    public string When = "";

    [DataField]
    public SurgeryHintIcon? Icon;

    [DataField]
    public bool Limb;

    [DataField]
    public int Order;

    [DataField]
    public bool Sterile = true;

    [DataField]
    public bool Machine;

    [DataField]
    public bool Soft;

    [DataField]
    public bool Plant;

    [DataField]
    public SurgerySide Side = SurgerySide.Front;

    [DataField]
    public List<string> Nearby = new();

    [DataField]
    public float NearbyRange = 1.5f;

    [DataField]
    public string Appendage = "";

    [DataField]
    public bool Attach;

    [DataField]
    public bool Refill;

    [DataField]
    public List<string> Zones = new();

    [DataField]
    public string Implant = "";

    [DataField]
    public bool ImplantPut;

    [DataField]
    public bool Self;

    public bool Faces(bool prone)
    {
        return Side == SurgerySide.Any || Side == SurgerySide.Back == prone;
    }

    public bool Suits(bool machine, bool soft, bool plant)
    {
        return Machine == machine && Soft == soft && Plant == plant;
    }

    public bool Fits(LimbZone zone)
    {
        if (Zones.Count > 0)
            return Zones.Contains(zone.ToString());

        if (Zone.Length > 0)
            return Zone == zone.ToString();

        return !Limb || LimbWoundRules.CanSever(zone);
    }
}

[Prototype]
public sealed partial class SurgeryWrapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Tags = new();
}

[Prototype]
public sealed partial class SurgerySterilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public List<string> Gloves = new();

    [DataField]
    public List<string> Mask = new();

    [DataField]
    public List<string> Head = new();

    [DataField]
    public float NoGloves = 8f;

    [DataField]
    public float NoMask = 5f;

    [DataField]
    public float NoHead = 2f;

    [DataField]
    public float Puddle = 3f;

    [DataField]
    public float PuddleRange = 2f;

    [DataField]
    public float OffTable = 6f;

    [DataField]
    public float Threshold = 12f;

    [DataField]
    public float DelayMin = 240f;

    [DataField]
    public float DelayMax = 480f;

    [DataField]
    public float RotEvery = 20f;

    [DataField]
    public float RotHealth = 3f;

    [DataField]
    public float RotFlesh = 1f;

    [DataField]
    public float RotPoison = 1.5f;
}

[Prototype]
public sealed partial class SurgeryCurePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Reagents = new();

    [DataField]
    public float Amount = 5f;
}

[Prototype]
public sealed partial class SurgeryPartPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Zone = "";

    [DataField(required: true)]
    public string Anatomy = "";

    [DataField]
    public string Operation = "";

    [DataField]
    public bool Machine;

    [DataField]
    public string Sprite = "/Textures/Mobs/Species/Human/organs.rsi";

    [DataField]
    public bool Wound = true;

    [DataField]
    public string State = "";

    [DataField]
    public List<string> States = new();

    [DataField]
    public string Organ = "";

    [DataField]
    public string Implant = "";

    [DataField]
    public string Put = "";

    [DataField]
    public float Ax;

    [DataField]
    public float Ay;

    [DataField]
    public float Bx;

    [DataField]
    public float By;

    [DataField]
    public int Order;

    [DataField]
    public bool Pill;

    [DataField]
    public LimbZone? Cover;

    public bool Tracked => Put.Length > 0 && Organ.Length == 0 && Implant.Length == 0;

    public static LimbZone? CoverOf(IPrototypeManager prototypes, string part, string? operation)
    {
        if (part.Length > 0 && prototypes.TryIndex<SurgeryPartPrototype>(part, out var found))
            return found.Cover;

        if (string.IsNullOrEmpty(operation))
            return null;

        foreach (var each in prototypes.EnumeratePrototypes<SurgeryPartPrototype>())
        {
            if (each.Operation == operation || each.Put == operation)
                return each.Cover;
        }

        return null;
    }

    public static string For(IPrototypeManager prototypes, string operation)
    {
        foreach (var part in prototypes.EnumeratePrototypes<SurgeryPartPrototype>())
        {
            if (part.Tracked && (part.Operation == operation || part.Put == operation))
                return part.ID;
        }

        return "";
    }

    public static bool Blocks(IPrototypeManager prototypes, IEntityManager entities, EntityUid patient, string operation)
    {
        entities.TryGetComponent<SurgeryPartsGoneComponent>(patient, out var lost);
        foreach (var part in prototypes.EnumeratePrototypes<SurgeryPartPrototype>())
        {
            if (!part.Tracked)
                continue;

            var gone = lost != null && lost.Parts.Contains(part.ID);
            if (part.Operation == operation && gone || part.Put == operation && !gone)
                return true;
        }

        return false;
    }
}

[Prototype]
public sealed partial class SurgeryTeethPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Max = 3;
}

[Prototype]
public sealed partial class SurgerySpeechPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Accent = "Mumble";
}

[Prototype]
public sealed partial class SurgeryLodgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Blocked = new();
}

[DataDefinition]
public sealed partial class SurgeryStep
{
    [DataField(required: true)]
    public SurgeryVerbKind Verb;

    [DataField(required: true)]
    public SurgeryLayerKind Layer;

    [DataField]
    public string Organ = "";

    [DataField]
    public string Implant = "";

    [DataField]
    public string Game = "";

    [DataField]
    public string Speech = "";

    [DataField]
    public string Undo = "";

    [DataField]
    public bool Intact;

    [DataField]
    public string Hint = "";

    [DataField]
    public bool Flinch = true;

    [DataField]
    public EntProtoId? Drop;

    [DataField]
    public Dictionary<string, EntProtoId> DropBySpecies = new();

    [DataField]
    public List<EntProtoId> Need = new();

    [DataField]
    public string Effect = "";

    [DataField]
    public bool Optional;

    [DataField]
    public int Order = -1;
}

[Prototype]
public sealed partial class SurgeryHealthPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<SurgeryVerbKind, float> Cost = new();

    [DataField]
    public List<SurgeryVerbKind> Restore = new();

    [DataField]
    public float Slip = 0.06f;

    [DataField]
    public float Flinch = 0.03f;

    [DataField]
    public float Keep = 0.1f;
}

[Prototype]
public sealed partial class SurgeryAnesthesiaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public List<string> Reagents = new();

    [DataField]
    public float Amount = 1f;

    [DataField]
    public List<string> Calm = new();

    [DataField]
    public float FlinchChance = 0.35f;

    [DataField]
    public float FlinchProgress = 0.5f;

    [DataField]
    public float FlinchBleed = 1f;

    [DataField]
    public float TearBleed = 3f;

    [DataField]
    public float SleepAmount = 2f;

    [DataField]
    public float SleepTime = 4f;

    [DataField]
    public float Hold = 0.6f;

    [DataField]
    public string Alcohol = "";

    [DataField]
    public float AlcoholClash = 50f;

    [DataField]
    public float AlcoholNumb = 120f;

    [DataField]
    public float ClashPoison = 2f;

    [DataField]
    public List<string> Wake = new();

    [DataField]
    public float WakeAmount = 1f;

    [DataField]
    public float WakePurge = 2f;
}

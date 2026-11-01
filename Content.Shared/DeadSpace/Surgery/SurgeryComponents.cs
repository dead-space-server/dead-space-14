// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[DataDefinition, Serializable, NetSerializable]
public partial struct SurgeryLayerState
{
    public SurgeryLayerState()
    {
    }

    [DataField]
    public SurgeryLayerKind Kind;

    [DataField]
    public float Integrity = 1f;

    [DataField]
    public bool Retracted;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBusyComponent : Component
{
    public EntityUid Patient;

    public TimeSpan Until;
}

[RegisterComponent]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField]
    public List<SurgeryVerbKind> Verbs = new();

    [DataField]
    public float Precision = 0.8f;

    [DataField]
    public float Slip = 0.15f;

    [DataField]
    public float Wobble = 16f;

    [DataField]
    public List<string> Games = new();

    [DataField]
    public int SawHits = 3;

    [DataField]
    public bool Passive;

    [DataField]
    public bool Probe;

    [DataField]
    public List<SurgeryVerbKind> MachineVerbs = new();

    [DataField]
    public List<SurgeryVerbKind> PlantVerbs = new();

    [DataField]
    public List<SurgeryVerbKind> SoftVerbs = new();

    private static readonly SurgeryVerbKind[] SoftBanned =
    {
        SurgeryVerbKind.SawBone,
        SurgeryVerbKind.Drill,
        SurgeryVerbKind.Amputate,
        SurgeryVerbKind.Cauterize,
        SurgeryVerbKind.Stanch,
        SurgeryVerbKind.MendBone,
    };

    private static readonly SurgeryVerbKind[] PlantBanned =
    {
        SurgeryVerbKind.Incise,
        SurgeryVerbKind.SawBone,
        SurgeryVerbKind.Drill,
        SurgeryVerbKind.Amputate,
        SurgeryVerbKind.Cauterize,
        SurgeryVerbKind.Stanch,
        SurgeryVerbKind.MendBone,
    };

    public List<SurgeryVerbKind> VerbsFor(bool machine, bool plant = false, bool soft = false)
    {
        if (machine)
            return MachineVerbs;

        if (plant)
            return PlantVerbs.Count > 0 ? PlantVerbs : Without(Verbs, PlantBanned);

        if (soft)
            return SoftVerbs.Count > 0 ? SoftVerbs : Without(Verbs, SoftBanned);

        return Verbs;
    }

    private static List<SurgeryVerbKind> Without(List<SurgeryVerbKind> verbs, SurgeryVerbKind[] banned)
    {
        var list = new List<SurgeryVerbKind>(verbs.Count);
        foreach (var verb in verbs)
        {
            if (Array.IndexOf(banned, verb) >= 0)
                continue;

            list.Add(verb);
        }

        return list;
    }
}

[RegisterComponent]
public sealed partial class SurgerySlimeGelComponent : Component
{
    [DataField]
    public int Uses = 5;
}

[RegisterComponent]
public sealed partial class SurgeryKeepOrganComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryBoneGelComponent : Component
{
    [DataField]
    public int Uses = 10;
}

[RegisterComponent]
public sealed partial class SurgeryDionaSapComponent : Component
{
    [DataField]
    public int Uses = 5;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganKindComponent : Component
{
    [DataField(required: true)]
    public string Organ = "";

    [DataField]
    public bool Machine;

    public static bool Matches(IEntityManager entities, EntityUid item, string organ, bool machine)
    {
        if (entities.TryGetComponent<SurgeryOrganKindComponent>(item, out var kind))
            return kind.Organ == organ && kind.Machine == machine;

        return !machine
               && entities.TryGetComponent<MetaDataComponent>(item, out var meta)
               && meta.EntityPrototype != null
               && meta.EntityPrototype.ID.Contains(organ, StringComparison.OrdinalIgnoreCase);
    }
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryVoiceModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStatusModuleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryNoHealthIconComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryMarkerComponent : Component
{
}

[RegisterComponent]
public sealed partial class SurgerySkinRollComponent : Component
{
    [DataField]
    public int Uses = 4;
}

[RegisterComponent]
public sealed partial class SurgeryGiftComponent : Component
{
    [DataField]
    public ComponentRegistry Grant = new();
}

[RegisterComponent]
public sealed partial class SurgeryGiftHostComponent : Component
{
    [DataField]
    public HashSet<string> Granted = new();
}

[RegisterComponent]
public sealed partial class SurgeryTeethComponent : Component
{
    public EntityUid? Bite;

    public int Max = 3;
}

[RegisterComponent]
public sealed partial class SurgerySurfaceComponent : Component
{
    [DataField]
    public ProtoId<SurgeryPlacePrototype> Place = "Table";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class SurgerySiteComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbZone Zone = LimbZone.Torso;

    [DataField, AutoNetworkedField]
    public string Part = "";

    [DataField, AutoNetworkedField]
    public string Queued = "";

    [DataField, AutoNetworkedField]
    public bool Back;

    public byte Rolled = 255;

    public int RolledStep = -1;

    [DataField, AutoNetworkedField]
    public List<SurgeryLayerState> Layers = new();

    [DataField]
    public float Germs;

    [DataField]
    public bool Gel;

    [DataField]
    public float SpringBone;

    [DataField]
    public TimeSpan SpringAt;

    [DataField]
    public HashSet<LimbOrgan> Examined = new();

    [DataField]
    public int Step;

    [DataField]
    public byte Chosen = 255;

    [DataField]
    public bool Clamped;

    public float Cut;

    public float Accumulator;

    public bool Done;

    public int CloseStep;

    public bool Sterile = true;

    [DataField, AutoNetworkedField]
    public Dictionary<string, SurgeryKept> Kept = new();
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class SurgeryKept
{
    [DataField]
    public LimbZone Zone;

    [DataField]
    public string Part = "";

    [DataField]
    public string Queued = "";

    [DataField]
    public bool Back;

    [DataField]
    public List<SurgeryLayerState> Layers = new();

    public int Step;
    public byte Chosen = 255;
    public bool Clamped;
    public float Germs;
    public bool Gel;
    public float Cut;
    public int CloseStep;
    public HashSet<LimbOrgan> Examined = new();
}

public static class SurgeryWounds
{
    public static bool Open(IReadOnlyList<SurgeryLayerState> layers)
    {
        foreach (var layer in layers)
        {
            if (layer.Integrity < 0.9f || layer.Retracted)
                return true;
        }

        return false;
    }

    public static string Key(LimbZone zone, string part, bool back)
    {
        return zone + ":" + part + (back ? ":back" : "");
    }

    public static void Collect(SurgerySiteComponent site, List<SurgeryKept> into)
    {
        into.Clear();
        var current = Key(site.Zone, site.Part, site.Back);
        if (!site.Done && Open(site.Layers))
        {
            into.Add(new SurgeryKept
            {
                Zone = site.Zone,
                Part = site.Part,
                Queued = site.Queued,
                Back = site.Back,
                Layers = site.Layers,
            });
        }

        foreach (var (key, kept) in site.Kept)
        {
            if (key == current || !Open(kept.Layers))
                continue;

            into.Add(new SurgeryKept
            {
                Zone = ZoneOf(key),
                Part = kept.Part,
                Queued = kept.Queued,
                Back = kept.Back,
                Layers = kept.Layers,
            });
        }
    }

    public static LimbZone ZoneOf(string key)
    {
        var colon = key.IndexOf(':');
        var name = colon < 0 ? key : key[..colon];
        return Enum.TryParse(name, out LimbZone zone) ? zone : LimbZone.Torso;
    }
}

[RegisterComponent]
public sealed partial class OrganFindingComponent : Component
{
    [DataField]
    public string Owner = string.Empty;

    [DataField]
    public float Damage;

    [DataField]
    public bool Ruptured;

    [DataField]
    public string Organ = string.Empty;
}

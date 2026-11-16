// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public enum RuneTargeting : byte
{
    Self,
    Point,
    Entity,
    EntityOrSelf,
}

[Prototype("wizardRuneSpell")]
public sealed partial class RuneSpellPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<RunePrototype> Form;

    [DataField(required: true)]
    public ProtoId<RunePrototype> Element;

    [DataField]
    public RuneTargeting Targeting = RuneTargeting.Point;

    [DataField]
    public float Range = 10f;

    [DataField]
    public float Strain = 20f;

    [DataField]
    public float Cooldown = 5f;

    [DataField]
    public int Loudness = 1;

    [DataField]
    public int Absurdity;

    [DataField]
    public bool RequiresClothes;

    [DataField]
    public float RequiresWounded;

    [DataField]
    public bool OncePerRound;

    [DataField]
    public List<ProtoId<RunePrototype>> Forbids = new();

    [DataField]
    public List<ProtoId<RunePrototype>> Allows = new();

    [DataField(required: true)]
    public List<RuneSpellEffect> Effects = new();
}

public sealed class RuneSpell
{
    public string ID = string.Empty;
    public LocId Name;
    public ProtoId<RunePrototype> Form;
    public ProtoId<RunePrototype> Element;
    public RuneTargeting Targeting = RuneTargeting.Point;
    public float Range = 10f;
    public float Strain = 20f;
    public float Cooldown = 5f;
    public int Loudness = 1;
    public int Absurdity;
    public bool RequiresClothes;
    public float RequiresWounded;
    public bool OncePerRound;
    public HashSet<string> Forbids = new();
    public List<RuneSpellEffect> Effects = new();

    public static RuneSpell From(RuneSpellPrototype proto, RunePrototype? form = null)
    {
        var forbids = new HashSet<string>();
        if (form != null)
        {
            foreach (var id in form.Forbids)
                forbids.Add(id);
        }

        foreach (var id in proto.Forbids)
            forbids.Add(id);
        foreach (var id in proto.Allows)
            forbids.Remove(id);

        return new RuneSpell
        {
            Forbids = forbids,
            ID = proto.ID,
            Name = proto.Name,
            Form = proto.Form,
            Element = proto.Element,
            Targeting = proto.Targeting,
            Range = proto.Range,
            Strain = proto.Strain,
            Cooldown = proto.Cooldown,
            Loudness = proto.Loudness,
            Absurdity = proto.Absurdity,
            RequiresClothes = proto.RequiresClothes,
            RequiresWounded = proto.RequiresWounded,
            OncePerRound = proto.OncePerRound,
            Effects = proto.Effects,
        };
    }

    public static RuneSpell? Fuse(RunePrototype form, RuneFusionPrototype fusion)
    {
        if (form.Ultimate && !fusion.FormEffects.ContainsKey(form.ID))
            return null;

        return new RuneSpell
        {
            ID = form.ID + "+" + fusion.ID,
            Name = fusion.Name,
            Form = form.ID,
            Element = fusion.First,
            Targeting = form.Targeting,
            Range = form.SpellRange,
            Strain = form.SpellStrain + fusion.Strain,
            Cooldown = fusion.Cooldown ?? form.SpellCooldown,
            Loudness = form.SpellLoudness + fusion.Loudness,
            Absurdity = fusion.Absurdity,
            Effects = fusion.FormEffects.TryGetValue(form.ID, out var own) ? own : fusion.Effects,
        };
    }
}

[Prototype("wizardRuneFusion")]
public sealed partial class RuneFusionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<RunePrototype> First;

    [DataField(required: true)]
    public ProtoId<RunePrototype> Second;

    [DataField]
    public float Strain = 10f;

    [DataField]
    public float? Cooldown;

    [DataField]
    public int Loudness;

    [DataField]
    public int Absurdity;

    [DataField(required: true)]
    public List<RuneSpellEffect> Effects = new();

    [DataField]
    public Dictionary<ProtoId<RunePrototype>, List<RuneSpellEffect>> FormEffects = new();
}

[Prototype("wizardRuneSurge")]
public sealed partial class RuneSurgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<RuneSurgeEntry> Entries = new();
}

[DataDefinition]
public sealed partial class RuneSurgeEntry
{
    [DataField]
    public float Weight = 1f;

    [DataField(required: true)]
    public LocId Popup;

    [DataField(required: true)]
    public List<RuneSpellEffect> Effects = new();
}

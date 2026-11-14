// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed class RuneComposition
{
    public readonly RunePrototype Form;
    public readonly RunePrototype Element;
    public readonly RunePrototype? Second;
    public readonly List<RunePrototype> Modifiers;
    public readonly RuneSpellPrototype? Spell;

    public RuneComposition(RunePrototype form, RunePrototype element, RunePrototype? second, List<RunePrototype> modifiers, RuneSpellPrototype? spell)
    {
        Form = form;
        Element = element;
        Second = second;
        Modifiers = modifiers;
        Spell = spell;
    }

    public float Power
    {
        get
        {
            var power = 1f;
            foreach (var modifier in Modifiers)
                power *= modifier.Power;

            return power;
        }
    }

    public int Extra
    {
        get
        {
            var extra = 0;
            foreach (var modifier in Modifiers)
                extra += modifier.Extra;

            return extra;
        }
    }

    public float RangeScale
    {
        get
        {
            var range = 1f;
            foreach (var modifier in Modifiers)
                range *= modifier.Range;

            return range;
        }
    }

    public float DurationScale
    {
        get
        {
            var duration = 1f;
            foreach (var modifier in Modifiers)
                duration *= modifier.Duration;

            return duration;
        }
    }

    public bool Echo => Modifiers.Exists(m => m.Echo);

    public float BloodPrice
    {
        get
        {
            var price = 0f;
            foreach (var modifier in Modifiers)
                price = MathF.Max(price, modifier.BloodPrice);

            return price;
        }
    }

    public string Key
    {
        get
        {
            var key = Spell?.ID ?? Form.ID + Element.ID;
            foreach (var modifier in Modifiers)
                key += "+" + modifier.ID;

            return key;
        }
    }
}

public abstract class SharedRuneCasterSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public const int MaxLoudness = 5;
    public const int MaxAbsurdity = 5;

    private readonly Dictionary<(string, string), RuneSpellPrototype> _spells = new();
    private readonly Dictionary<(string, string, string), RuneSpellPrototype> _fusions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReloaded);
        SubscribeLocalEvent<RuneScrollComponent, ExaminedEvent>(OnScrollExamined);
        SubscribeLocalEvent<RuneWardComponent, DamageModifyEvent>(OnWardDamage);
        CacheSpells();
    }

    private void OnWardDamage(Entity<RuneWardComponent> ent, ref DamageModifyEvent args)
    {
        if (Proto.TryIndex(ent.Comp.Modifier, out var modifier))
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
    }

    private void OnReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<RuneSpellPrototype>() || args.WasModified<RuneFusionPrototype>() || args.WasModified<RunePrototype>())
            CacheSpells();
    }

    private void CacheSpells()
    {
        _spells.Clear();
        _fusions.Clear();
        foreach (var spell in Proto.EnumeratePrototypes<RuneSpellPrototype>())
            _spells[(spell.Form.Id, spell.Element.Id)] = spell;

        foreach (var fusion in Proto.EnumeratePrototypes<RuneFusionPrototype>())
        {
            foreach (var form in Proto.EnumeratePrototypes<RunePrototype>())
            {
                if (form.Kind != RuneKind.Form)
                    continue;

                if (RuneSpellPrototype.Fuse(form, fusion) is not { } spell)
                    continue;

                _fusions[(form.ID, fusion.First.Id, fusion.Second.Id)] = spell;
                _fusions[(form.ID, fusion.Second.Id, fusion.First.Id)] = spell;
            }
        }
    }

    public bool IsFusion(string rune)
    {
        return Proto.TryIndex<RunePrototype>(rune, out var proto) && proto.Fusion;
    }

    public float GetStrain(RuneCasterComponent caster)
    {
        var idle = (float) (Timing.CurTime - caster.StrainChanged).TotalSeconds - caster.DecayDelay;
        if (idle <= 0f)
            return caster.Strain;

        return MathF.Max(0f, caster.Strain - idle * caster.Decay);
    }

    public float GetStrainFraction(RuneCasterComponent caster)
    {
        return caster.MaxStrain <= 0f ? 0f : GetStrain(caster) / caster.MaxStrain;
    }

    public bool TryGetCooldown(RuneCasterComponent caster, string key, out float left, out float fraction)
    {
        left = 0f;
        fraction = 0f;
        if (!caster.Cooldowns.TryGetValue(key, out var cooldown))
            return false;

        return Remaining(cooldown.Ready, cooldown.Length, out left, out fraction);
    }

    public List<(string Name, float Left, float Fraction)> ActiveCooldowns(RuneCasterComponent caster)
    {
        var list = new List<(string Name, float Left, float Fraction)>();
        foreach (var cooldown in caster.Cooldowns.Values)
        {
            if (Remaining(cooldown.Ready, cooldown.Length, out var left, out var fraction))
                list.Add((Loc.GetString(cooldown.Name), left, fraction));
        }

        if (Remaining(caster.UltimateReady, caster.UltimateCooldown, out var ultimateLeft, out var ultimateFraction))
            list.Add((Loc.GetString("rune-circle-ultimate-cooldown"), ultimateLeft, ultimateFraction));

        list.Sort((a, b) => a.Left.CompareTo(b.Left));
        return list;
    }

    public bool TryGetUltimateCooldown(RuneCasterComponent caster, out float left, out float fraction)
    {
        return Remaining(caster.UltimateReady, caster.UltimateCooldown, out left, out fraction);
    }

    private bool Remaining(TimeSpan ready, float length, out float left, out float fraction)
    {
        left = (float) (ready - Timing.CurTime).TotalSeconds;
        fraction = 0f;
        if (left <= 0f)
            return false;

        fraction = length > 0f ? Math.Clamp(left / length, 0f, 1f) : 1f;
        return true;
    }

    protected void SetStrain(EntityUid uid, RuneCasterComponent caster, float strain)
    {
        caster.Strain = MathF.Max(0f, strain);
        caster.StrainChanged = Timing.CurTime;
        Dirty(uid, caster);
    }

    public bool Knows(RuneCasterComponent caster, string rune)
    {
        return caster.Known.Contains(rune) || caster.InBlood && caster.BloodRune == rune;
    }

    public bool TryCompose(IReadOnlyList<string> chain, [NotNullWhen(true)] out RuneComposition? composition)
    {
        composition = null;
        if (chain.Count < 2 ||
            !Proto.TryIndex<RunePrototype>(chain[0], out var form) || form.Kind != RuneKind.Form ||
            !Proto.TryIndex<RunePrototype>(chain[1], out var element) || element.Kind != RuneKind.Element)
            return false;

        var modifiers = new List<RunePrototype>();
        RunePrototype? second = null;
        for (var i = 2; i < chain.Count; i++)
        {
            if (!Proto.TryIndex<RunePrototype>(chain[i], out var rune) || rune.Kind != ExpectedKind(chain, i))
                return false;

            if (rune.Fusion && i != 2)
                return false;

            if (rune.Kind == RuneKind.Element)
            {
                second = rune;
                continue;
            }

            modifiers.Add(rune);
        }

        RuneSpellPrototype? spell;
        if (second != null)
            _fusions.TryGetValue((form.ID, element.ID, second.ID), out spell);
        else if (modifiers.Count > 0 && modifiers[0].Fusion)
            spell = null;
        else
            _spells.TryGetValue((form.ID, element.ID), out spell);

        composition = new RuneComposition(form, element, second, modifiers, spell);
        return true;
    }

    public const int MaxModifiers = 2;

    public RuneKind? NextKind(IReadOnlyList<string> chain, int maxRunes)
    {
        if (chain.Count >= maxRunes)
            return null;

        var kind = ExpectedKind(chain, chain.Count);
        if (kind != RuneKind.Modifier)
            return kind;

        var modifiers = 0;
        for (var i = 2; i < chain.Count; i++)
        {
            if (!IsFusion(chain[i]) && Proto.TryIndex<RunePrototype>(chain[i], out var rune) && rune.Kind == RuneKind.Modifier)
                modifiers++;
        }

        return modifiers < MaxModifiers ? kind : null;
    }

    public RuneKind ExpectedKind(IReadOnlyList<string> chain, int index)
    {
        return index switch
        {
            0 => RuneKind.Form,
            1 => RuneKind.Element,
            3 when IsFusion(chain[2]) => RuneKind.Element,
            _ => RuneKind.Modifier,
        };
    }

    public float ComputeStrain(RuneCasterComponent caster, RuneComposition composition)
    {
        var strain = composition.Spell?.Strain ?? 10f;
        var scale = 1f;
        strain += composition.Form.Strain + composition.Element.Strain + (composition.Second?.Strain ?? 0f);
        foreach (var modifier in composition.Modifiers)
        {
            strain += modifier.Strain;
            scale *= modifier.StrainScale;
        }

        if (caster.Affinity == composition.Element.ID)
            scale *= 0.75f;
        else if (caster.Weakness == composition.Element.ID)
            scale *= 1.35f;

        if (caster.LastElement == composition.Element.ID)
            scale *= 1f + caster.FatigueStep * caster.ElementStreak;

        return strain * scale;
    }

    public int ComputeLoudness(IReadOnlyList<RunePrototype> runes, RuneSpellPrototype? spell)
    {
        var loudness = spell?.Loudness ?? 0;
        foreach (var rune in runes)
            loudness += rune.Loudness;

        return Math.Clamp(loudness, 0, MaxLoudness);
    }

    public int ComputeAbsurdity(IReadOnlyList<RunePrototype> runes, RuneSpellPrototype? spell)
    {
        var absurdity = spell?.Absurdity ?? 0;
        foreach (var rune in runes)
            absurdity += rune.Absurdity;

        return Math.Clamp(absurdity, 0, MaxAbsurdity);
    }

    public List<RunePrototype> ResolveRunes(IEnumerable<string> chain)
    {
        var runes = new List<RunePrototype>();
        foreach (var id in chain)
        {
            if (Proto.TryIndex<RunePrototype>(id, out var rune))
                runes.Add(rune);
        }

        return runes;
    }

    private void OnScrollExamined(Entity<RuneScrollComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Runes.Count == 0)
            return;

        var names = new List<string>();
        foreach (var rune in ResolveRunes(ent.Comp.Runes.ConvertAll(r => r.Id)))
            names.Add(Loc.GetString(rune.Name));

        args.PushMarkup(Loc.GetString("rune-scroll-examine", ("runes", string.Join(" · ", names))));

        if (ent.Comp.BoundTo is { } bound && bound == args.Examiner)
            args.PushMarkup(Loc.GetString("rune-scroll-examine-bound"));
    }
}

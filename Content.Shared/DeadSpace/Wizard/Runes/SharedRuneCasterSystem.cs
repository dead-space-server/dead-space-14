// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed class RuneComposition
{
    public readonly RunePrototype Form;
    public readonly RunePrototype Element;
    public readonly RunePrototype? Second;
    public readonly List<RunePrototype> Modifiers;
    public readonly RuneSpell? Spell;

    public RuneComposition(RunePrototype form, RunePrototype element, RunePrototype? second, List<RunePrototype> modifiers, RuneSpell? spell)
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

    public string Key => Spell?.CooldownKey ?? Spell?.ID ?? Form.ID + Element.ID;
}

public abstract class SharedRuneCasterSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public const int MaxLoudness = 5;
    public const int MaxAbsurdity = 5;

    private readonly Dictionary<(string, string), RuneSpell> _spells = new();
    private readonly Dictionary<(string, string, string), RuneSpell> _fusions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReloaded);
        SubscribeLocalEvent<RuneScrollComponent, ExaminedEvent>(OnScrollExamined);
        SubscribeLocalEvent<RuneWardComponent, DamageModifyEvent>(OnWardDamage);
        SubscribeLocalEvent<RuneStoneFistsComponent, GetMeleeDamageEvent>(OnStoneFists);
        SubscribeLocalEvent<RuneExamineNoteComponent, ExaminedEvent>(OnNoteExamined);
        CacheSpells();
    }

    private void OnNoteExamined(Entity<RuneExamineNoteComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Text));
    }

    private void OnStoneFists(Entity<RuneStoneFistsComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon == ent.Owner)
            args.Damage = new DamageSpecifier(ent.Comp.Damage);
    }

    private void OnWardDamage(Entity<RuneWardComponent> ent, ref DamageModifyEvent args)
    {
        if (!Proto.TryIndex(ent.Comp.Modifier, out var modifier))
            return;

        if (MathF.Abs(ent.Comp.Strength - 1f) <= 0.01f)
        {
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
            return;
        }

        var damage = new DamageSpecifier(args.Damage);
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > FixedPoint2.Zero && modifier.Coefficients.TryGetValue(type, out var coefficient))
                damage.DamageDict[type] = value * MathF.Max(0f, 1f - (1f - coefficient) * ent.Comp.Strength);
        }

        args.Damage = damage;
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
            _spells[(spell.Form.Id, spell.Element.Id)] = RuneSpell.From(spell, Proto.TryIndex(spell.Form, out var form) ? form : null);

        foreach (var fusion in Proto.EnumeratePrototypes<RuneFusionPrototype>())
        {
            foreach (var form in Proto.EnumeratePrototypes<RunePrototype>())
            {
                if (form.Kind != RuneKind.Form)
                    continue;

                if (RuneSpell.Fuse(form, fusion) is not { } spell)
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

    public bool TryGetCooldown(RuneCasterComponent caster, string key, out float left, out float fraction, out bool active)
    {
        left = 0f;
        fraction = 0f;
        active = false;
        if (!caster.Cooldowns.TryGetValue(key, out var cooldown))
            return false;

        return Phase(cooldown, out left, out fraction, out active);
    }

    private bool Phase(RuneCooldown cooldown, out float left, out float fraction, out bool active)
    {
        active = cooldown.Active > Timing.CurTime;
        return active
            ? Remaining(cooldown.Active, cooldown.ActiveLength, out left, out fraction)
            : Remaining(cooldown.Ready, cooldown.Length, out left, out fraction);
    }

    public List<(string Name, float Left, float Fraction, bool Active)> ActiveCooldowns(RuneCasterComponent caster)
    {
        var list = new List<(string Name, float Left, float Fraction, bool Active)>();
        foreach (var cooldown in caster.Cooldowns.Values)
        {
            if (Phase(cooldown, out var left, out var fraction, out var active))
                list.Add((Loc.GetString(cooldown.Name), left, fraction, active));
        }

        if (Remaining(caster.UltimateReady, caster.UltimateCooldown, out var ultimateLeft, out var ultimateFraction))
            list.Add((Loc.GetString("rune-circle-ultimate-cooldown"), ultimateLeft, ultimateFraction, false));

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

        RuneSpell? spell;
        if (second != null)
            _fusions.TryGetValue((form.ID, element.ID, second.ID), out spell);
        else if (modifiers.Count > 0 && modifiers[0].Fusion)
            spell = null;
        else
            _spells.TryGetValue((form.ID, element.ID), out spell);

        if (spell != null && modifiers.Exists(m => spell.Forbids.Contains(m.ID)))
            spell = null;

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

        if (RepeatsElement(caster, composition))
            scale *= 1f + caster.FatigueStep * (caster.ElementStreak + 1);

        return strain * scale;
    }

    public static (float Caster, float Point) EffectAreas(RuneSpell spell, RuneComposition composition)
    {
        var caster = 0f;
        var point = 0f;
        foreach (var effect in spell.Effects)
            AddArea(effect, composition, ref caster, ref point);

        return spell.Targeting == RuneTargeting.Self ? (MathF.Max(caster, point), 0f) : (caster, point);
    }

    private static void AddArea(RuneSpellEffect effect, RuneComposition composition, ref float caster, ref float point)
    {
        var form = composition.Form;
        var scale = composition.RangeScale;
        switch (effect)
        {
            case RuneTargetedEffect targeted:
            {
                var mode = targeted.Target == RuneEffectTarget.Auto ? form.AutoTarget : targeted.Target;
                if (mode == RuneEffectTarget.AroundCaster)
                    caster = MathF.Max(caster, targeted.Radius * scale);
                else if (mode == RuneEffectTarget.AroundPoint)
                    point = MathF.Max(point, targeted.Radius * scale);
                break;
            }
            case RunePushEffect push:
            {
                var atPoint = push.Auto ? form.AutoTarget is not (RuneEffectTarget.AroundCaster or RuneEffectTarget.Caster) : push.AtPoint;
                if (atPoint)
                    point = MathF.Max(point, push.Radius * scale);
                else
                    caster = MathF.Max(caster, push.Radius * scale);
                break;
            }
            case RuneSpawnEffect spawn:
                AddPatternArea(spawn.Pattern, spawn.Size + composition.Extra, form, ref caster, ref point);
                break;
            case RuneSpawnRandomEffect spawn:
                AddPatternArea(spawn.Pattern, spawn.Size + composition.Extra, form, ref caster, ref point);
                break;
            case RuneDecayEffect decay:
                if (decay.AroundCaster)
                    caster = MathF.Max(caster, decay.Radius * scale);
                else
                    point = MathF.Max(point, decay.Radius * scale);
                break;
            case RuneLightningEffect lightning:
                point = MathF.Max(point, lightning.Range * scale);
                break;
            case RuneTimeStopEffect timeStop:
                point = MathF.Max(point, timeStop.Radius * scale);
                break;
            case RuneRandomEffect random:
                foreach (var option in random.Options)
                    AddArea(option, composition, ref caster, ref point);
                break;
            case RuneProjectileEffect projectile:
                foreach (var hit in projectile.OnHit)
                    AddArea(hit, composition, ref caster, ref point);
                break;
            case RuneMirrorCrowdEffect crowd:
                caster = MathF.Max(caster, crowd.Radius * scale);
                break;
        }
    }

    private static void AddPatternArea(RuneSpawnPattern pattern, int size, RunePrototype form, ref float caster, ref float point)
    {
        if (pattern == RuneSpawnPattern.Auto)
            pattern = form.AutoPattern;

        switch (pattern)
        {
            case RuneSpawnPattern.RingAroundCaster:
                caster = MathF.Max(caster, Math.Max(1, size) + 0.5f);
                break;
            case RuneSpawnPattern.RingAtPoint:
                point = MathF.Max(point, Math.Max(1, size) + 0.5f);
                break;
            case RuneSpawnPattern.AreaAtPoint when size > 1:
                point = MathF.Max(point, size - 0.5f);
                break;
        }
    }

    public static bool RepeatsElement(RuneCasterComponent caster, RuneComposition composition)
    {
        return Matches(caster, composition.Element.ID) || composition.Second is { } second && Matches(caster, second.ID);
    }

    private static bool Matches(RuneCasterComponent caster, string element)
    {
        return caster.LastElement == element || caster.LastSecond == element;
    }

    public int ComputeLoudness(IReadOnlyList<RunePrototype> runes, RuneSpell? spell)
    {
        var loudness = spell?.Loudness ?? 0;
        foreach (var rune in runes)
            loudness += rune.Loudness;

        return Math.Clamp(loudness, 0, MaxLoudness);
    }

    public int ComputeAbsurdity(IReadOnlyList<RunePrototype> runes, RuneSpell? spell)
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
        if (HasComp<RuneCasterComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString("rune-scroll-examine-use"));
    }
}

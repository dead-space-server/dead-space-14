// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneOrbitShieldComponent : Component
{
    public int Count;

    public TimeSpan End;

    public float MinDamage = 5f;

    public float MinStamina = 15f;

    public ProtoId<AlertPrototype>? Alert;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly HashSet<string> ShieldedDamage = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Shock",
        "Caustic",
        "Structural",
    };

    private void InitializeShields()
    {
        SubscribeLocalEvent<RuneOrbitShieldComponent, BeforeDamageChangedEvent>(OnShieldDamage);
        SubscribeLocalEvent<RuneOrbitShieldComponent, BeforeStaminaDamageEvent>(OnShieldStamina);
        SubscribeLocalEvent<RuneOrbitShieldComponent, ComponentShutdown>(OnShieldShutdown);
    }

    private void ApplyOrbitShield(RuneCastContext ctx, RuneOrbitShieldEffect effect)
    {
        var comp = EnsureComp<RuneOrbitShieldComponent>(ctx.Caster);
        comp.End = Timing.CurTime + TimeSpan.FromSeconds(effect.Duration * ctx.DurationScale);
        comp.MinDamage = effect.MinDamage;
        comp.MinStamina = effect.MinStamina;
        comp.Count = Math.Min(effect.MaxShields, comp.Count + effect.Count + ctx.Extra + (ctx.Power > 1f ? 1 : 0));
        SetAuraShields(ctx.Caster, comp.Count);

        comp.Alert = effect.Alert;
        if (effect.Alert is { } alert)
            ShowCharm(ctx.Caster, alert, comp.End);
    }

    private void OnShieldDamage(Entity<RuneOrbitShieldComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin == ent.Owner)
            return;

        var incoming = 0f;
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0 && ShieldedDamage.Contains(type))
                incoming += value.Float();
        }

        if (incoming >= ent.Comp.MinDamage && TryBreakShield(ent))
            args.Cancelled = true;
    }

    private void OnShieldStamina(Entity<RuneOrbitShieldComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Cancelled || args.Value < ent.Comp.MinStamina)
            return;

        if (TryBreakShield(ent))
            args.Cancelled = true;
    }

    private bool TryBreakShield(Entity<RuneOrbitShieldComponent> ent)
    {
        if (ent.Comp.Count <= 0)
        {
            RemCompDeferred<RuneOrbitShieldComponent>(ent);
            return false;
        }

        ent.Comp.Count--;
        SetAuraShields(ent, ent.Comp.Count);
        _popup.PopupEntity(Loc.GetString("rune-shield-break", ("left", ent.Comp.Count)), ent, ent, PopupType.SmallCaution);
        if (ent.Comp.Count == 0)
            RemCompDeferred<RuneOrbitShieldComponent>(ent);

        return true;
    }

    private void OnShieldShutdown(Entity<RuneOrbitShieldComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Count = 0;
        SetAuraShields(ent, 0);
        if (ent.Comp.Alert is { } alert)
            ClearCharm(ent.Owner, alert);
    }

    private void UpdateShields()
    {
        var query = EntityQueryEnumerator<RuneOrbitShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Timing.CurTime >= comp.End)
                RemCompDeferred<RuneOrbitShieldComponent>(uid);
        }
    }

    private void ShowCharm(EntityUid target, ProtoId<AlertPrototype> alert, TimeSpan end)
    {
        _alerts.ShowAlert(target, alert, cooldown: (Timing.CurTime, end));
        var aura = EnsureComp<RuneAuraComponent>(target);
        if (aura.Charms.Contains(alert.Id))
            return;

        aura.Charms.Add(alert.Id);
        Dirty(target, aura);
    }

    private void ClearCharm(EntityUid target, ProtoId<AlertPrototype> alert)
    {
        _alerts.ClearAlert(target, alert);
        if (!TryComp<RuneAuraComponent>(target, out var aura) || !aura.Charms.Remove(alert.Id))
            return;

        Dirty(target, aura);
        if (aura.Charms.Count == 0 && aura.Shields == 0)
            RemCompDeferred<RuneAuraComponent>(target);
    }

    private void SetAuraShields(EntityUid target, int count)
    {
        if (count <= 0 && !HasComp<RuneAuraComponent>(target))
            return;

        var aura = EnsureComp<RuneAuraComponent>(target);
        if (aura.Shields == count)
            return;

        aura.Shields = count;
        Dirty(target, aura);
        if (aura.Charms.Count == 0 && aura.Shields == 0)
            RemCompDeferred<RuneAuraComponent>(target);
    }
}

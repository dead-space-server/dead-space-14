// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Follower;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneOrbitShieldComponent : Component
{
    public readonly List<EntityUid> Shields = new();

    public TimeSpan End;

    public float MinDamage = 5f;

    public float MinStamina = 15f;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly FollowerSystem _follower = default!;
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

        var coords = Transform(ctx.Caster).Coordinates;
        var count = effect.Count + ctx.Extra + (ctx.Power > 1f ? 1 : 0);
        for (var i = 0; i < count; i++)
        {
            var shield = Spawn(effect.Prototype, coords);
            _follower.StartFollowingEntity(shield, ctx.Caster);
            comp.Shields.Add(shield);
        }

        while (comp.Shields.Count > effect.MaxShields)
        {
            QueueDel(comp.Shields[0]);
            comp.Shields.RemoveAt(0);
        }
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
        while (ent.Comp.Shields.Count > 0)
        {
            var shield = ent.Comp.Shields[^1];
            ent.Comp.Shields.RemoveAt(ent.Comp.Shields.Count - 1);
            if (TerminatingOrDeleted(shield))
                continue;

            QueueDel(shield);
            _popup.PopupEntity(Loc.GetString("rune-shield-break", ("left", ent.Comp.Shields.Count)), ent, ent, PopupType.SmallCaution);
            if (ent.Comp.Shields.Count == 0)
                RemCompDeferred<RuneOrbitShieldComponent>(ent);

            return true;
        }

        RemCompDeferred<RuneOrbitShieldComponent>(ent);
        return false;
    }

    private void OnShieldShutdown(Entity<RuneOrbitShieldComponent> ent, ref ComponentShutdown args)
    {
        foreach (var shield in ent.Comp.Shields)
            QueueDel(shield);

        ent.Comp.Shields.Clear();
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
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Surgery;
using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneRegenerationComponent : Component
{
    public readonly List<(DamageSpecifier PerSecond, TimeSpan End, ProtoId<AlertPrototype>? Alert)> Entries = new();

    public TimeSpan NextTick;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SurgeryLimbPort _limbPort = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    private const float LimbMendShare = 0.6f;
    private const float OrganMendShare = 1.5f;
    private const string BloodlossDamage = "Bloodloss";

    private void ApplyDamage(RuneCastContext ctx, RuneDamageEffect effect)
    {
        var damage = effect.Damage * ctx.Power;
        var heal = -damage.GetTotal().Float();
        var deep = effect.MendInternal || ctx.Power > 1f;

        foreach (var target in Targets(ctx, effect))
        {
            _damageable.TryChangeDamage(target, damage, effect.IgnoreResistances, origin: ctx.Caster);
            if (damage.DamageDict.TryGetValue(BloodlossDamage, out var bloodloss) && bloodloss != FixedPoint2.Zero)
                _bloodstream.TryModifyBloodLevel(target, -bloodloss);
            if (heal > 0f)
                MendLimbs(target, heal, deep);
        }
    }

    private void MendLimbs(EntityUid target, float heal, bool deep)
    {
        if (!TryComp<LimbBodyComponent>(target, out var limbs))
            return;

        var amount = heal * LimbMendShare;
        for (var i = 0; i < limbs.Zones.Count && i < LimbWoundRules.BodyZoneCount; i++)
        {
            if (limbs.Zones[i].Severed)
                continue;

            var edit = new LimbEdit
            {
                Flesh = -amount,
                Bone = -amount,
                Skin = -amount,
                Bleed = -amount * 0.2f,
                Health = amount,
            };
            _limbs.ApplyEdit(target, (LimbZone) i, edit);
        }

        if (!deep)
            return;

        foreach (var organ in Enum.GetValues<LimbOrgan>())
        {
            var edit = new LimbOrganEdit
            {
                Damage = -heal * OrganMendShare,
                Ruptured = false,
                SetRuptured = true,
            };
            _limbs.ApplyOrganEdit(target, organ, edit);
        }
    }

    private void StartRegeneration(EntityUid target, DamageSpecifier perSecond, float duration, ProtoId<AlertPrototype>? alert = null)
    {
        var comp = EnsureComp<RuneRegenerationComponent>(target);
        var end = Timing.CurTime + TimeSpan.FromSeconds(duration);
        comp.Entries.Add((perSecond, end, alert));
        if (comp.NextTick < Timing.CurTime)
            comp.NextTick = Timing.CurTime + TimeSpan.FromSeconds(1);

        if (alert is { } shown)
            ShowCharm(target, shown, end);
    }

    private void ClearRegenerationAlerts(EntityUid uid, RuneRegenerationComponent comp, bool all)
    {
        foreach (var (_, end, alert) in comp.Entries)
        {
            if (alert is not { } shown || !all && end > Timing.CurTime)
                continue;

            if (all || !comp.Entries.Exists(e => e.Alert == shown && e.End > Timing.CurTime))
                ClearCharm(uid, shown);
        }
    }

    private void UpdateRegeneration()
    {
        var query = EntityQueryEnumerator<RuneRegenerationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Timing.CurTime < comp.NextTick)
                continue;

            comp.NextTick = Timing.CurTime + TimeSpan.FromSeconds(1);
            var dead = _mobState.IsDead(uid);
            ClearRegenerationAlerts(uid, comp, dead);
            comp.Entries.RemoveAll(e => e.End <= Timing.CurTime);
            if (comp.Entries.Count == 0 || dead)
            {
                RemCompDeferred<RuneRegenerationComponent>(uid);
                continue;
            }

            foreach (var (perSecond, _, _) in comp.Entries)
            {
                _damageable.TryChangeDamage(uid, perSecond, true, interruptsDoAfters: false);
                var heal = -perSecond.GetTotal().Float();
                if (heal > 0f)
                    MendLimbs(uid, heal, false);
            }
        }
    }

    private void RestoreFully(EntityUid target)
    {
        _limbPort.Restore(target);
        _rejuvenate.PerformRejuvenate(target);
    }
}

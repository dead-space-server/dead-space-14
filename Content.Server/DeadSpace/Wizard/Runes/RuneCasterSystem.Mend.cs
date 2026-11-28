// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Surgery;
using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneRegenerationComponent : Component
{
    public readonly List<RuneRegenerationEntry> Entries = new();

    public TimeSpan NextTick;
}

public sealed record RuneRegenerationEntry(
    DamageSpecifier PerSecond,
    TimeSpan End,
    ProtoId<AlertPrototype>? Alert,
    float Heal,
    float OrganMend,
    string? Group = null);

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SurgeryLimbPort _limbPort = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;

    private const float LimbMendShare = 0.6f;
    private const float OrganMendShare = 1.5f;
    private const string BloodlossDamage = "Bloodloss";
    private const float CauterizeBleed = 1000f;

    private static readonly LimbOrgan[] OrganDanger =
    {
        LimbOrgan.Brain,
        LimbOrgan.Heart,
        LimbOrgan.Lungs,
        LimbOrgan.Liver,
        LimbOrgan.Kidneys,
        LimbOrgan.Core,
        LimbOrgan.Stomach,
        LimbOrgan.Eyes,
    };

    private void ApplyDamage(RuneCastContext ctx, RuneDamageEffect effect)
    {
        var damage = effect.Damage * (ctx.Power * ctx.DamageScale);
        var heal = -damage.GetTotal().Float();
        var deep = effect.MendInternal || ctx.Power > 1f;
        var rest = SplitGroupHeals(damage, out var groupHeals);

        foreach (var target in Targets(ctx, effect))
        {
            var soaked = effect.SoakedScale > 1f && HasComp<RuneSoakedComponent>(target) ? effect.SoakedScale : 1f;
            var dealt = soaked > 1f ? rest * soaked : rest;
            var dealtGroups = groupHeals;
            if (soaked > 1f)
            {
                dealtGroups = new List<(ProtoId<DamageGroupPrototype> Group, FixedPoint2 Amount)>();
                foreach (var pair in groupHeals)
                    dealtGroups.Add((pair.Group, FixedPoint2.New(pair.Amount.Float() * soaked)));
            }
            if (!dealt.Empty)
                _damageable.TryChangeDamage(target, dealt, effect.IgnoreResistances, origin: ctx.Caster);
            foreach (var (group, amount) in dealtGroups)
                _damageable.HealDistributed(target, amount, group, ctx.Caster);
            if (damage.DamageDict.TryGetValue(BloodlossDamage, out var bloodloss) && bloodloss != FixedPoint2.Zero)
                _bloodstream.TryModifyBloodLevel(target, -bloodloss * soaked);
            if (heal > 0f)
                MendLimbs(target, heal * soaked, deep);
        }
    }

    private DamageSpecifier SplitGroupHeals(DamageSpecifier damage, out List<(ProtoId<DamageGroupPrototype> Group, FixedPoint2 Amount)> heals)
    {
        heals = new List<(ProtoId<DamageGroupPrototype>, FixedPoint2)>();
        var rest = new DamageSpecifier(damage);
        foreach (var group in Proto.EnumeratePrototypes<DamageGroupPrototype>())
        {
            var common = FixedPoint2.Zero;
            var whole = group.DamageTypes.Count > 0;
            foreach (var type in group.DamageTypes)
            {
                if (!rest.DamageDict.TryGetValue(type.Id, out var value) || value >= FixedPoint2.Zero)
                {
                    whole = false;
                    break;
                }

                common = common == FixedPoint2.Zero ? value : FixedPoint2.Max(common, value);
            }

            if (!whole)
                continue;

            foreach (var type in group.DamageTypes)
            {
                rest.DamageDict[type.Id] -= common;
                if (rest.DamageDict[type.Id] == FixedPoint2.Zero)
                    rest.DamageDict.Remove(type.Id);
            }

            heals.Add((group.ID, common * group.DamageTypes.Count));
        }

        return rest;
    }

    private void MendLimbs(EntityUid target, float heal, bool deep)
    {
        if (!TryComp<LimbBodyComponent>(target, out var limbs))
            return;

        var count = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        var total = 0f;
        for (var i = 0; i < count; i++)
        {
            if (!limbs.Zones[i].Severed)
                total += Wound(limbs.Zones[i]);
        }

        if (total > 0f)
        {
            var budget = heal * LimbMendShare;
            for (var i = 0; i < count; i++)
            {
                var state = limbs.Zones[i];
                var wound = Wound(state);
                if (state.Severed || wound <= 0f)
                    continue;

                var amount = budget * wound / total;
                var edit = new LimbEdit
                {
                    Flesh = -amount,
                    Bone = -amount,
                    Skin = -amount,
                    Bleed = -amount * 0.2f,
                };
                _limbs.ApplyEdit(target, (LimbZone) i, edit);
            }
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

    private static float Wound(LimbZoneState state)
    {
        return MathF.Max(0f, state.Flesh) + MathF.Max(0f, state.Bone) + MathF.Max(0f, state.Skin) + MathF.Max(0f, state.Bleed);
    }

    private void StartRegeneration(EntityUid target, RuneRegenerationEntry entry)
    {
        var comp = EnsureComp<RuneRegenerationComponent>(target);
        comp.Entries.Add(entry);
        if (comp.NextTick < Timing.CurTime)
            comp.NextTick = Timing.CurTime + TimeSpan.FromSeconds(1);

        if (entry.Alert is { } shown)
            ShowCharm(target, shown, entry.End);
    }

    private void ClearRegenerationAlerts(EntityUid uid, RuneRegenerationComponent comp, bool all)
    {
        foreach (var entry in comp.Entries)
        {
            if (entry.Alert is not { } shown || !all && entry.End > Timing.CurTime)
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

            foreach (var entry in comp.Entries)
            {
                if (!entry.PerSecond.Empty)
                    _damageable.TryChangeDamage(uid, entry.PerSecond, true, interruptsDoAfters: false);
                if (entry.Heal > 0f)
                    _damageable.HealDistributed(uid, FixedPoint2.New(-entry.Heal));
                if (entry.OrganMend > 0f)
                    MendWorstOrgan(uid, entry.OrganMend);

                var heal = entry.Heal - entry.PerSecond.GetTotal().Float();
                if (heal > 0f)
                    MendLimbs(uid, heal, false);
            }
        }
    }

    private void MendWorstOrgan(EntityUid target, float share)
    {
        if (!TryComp<LimbBodyComponent>(target, out var body))
            return;

        foreach (var organ in OrganDanger)
        {
            var index = (int) organ;
            if (index >= body.Organs.Count || (body.Present & (LimbOrganMask) (1 << index)) == 0)
                continue;

            var state = body.Organs[index];
            if (state.Missing || state.Damage <= 0f && !state.Ruptured)
                continue;

            _limbs.ApplyOrganEdit(target, organ, new LimbOrganEdit
            {
                Damage = -LimbOrganRules.Rupture * share,
                Ruptured = false,
                SetRuptured = true,
            });
            return;
        }
    }

    private void Cauterize(EntityUid target)
    {
        _bloodstream.TryModifyBleedAmount(target, -CauterizeBleed);
        if (!TryComp<LimbBodyComponent>(target, out var limbs))
            return;

        for (var i = 0; i < limbs.Zones.Count && i < LimbWoundRules.BodyZoneCount; i++)
        {
            if (!limbs.Zones[i].Severed)
                _limbs.ApplyEdit(target, (LimbZone) i, new LimbEdit { Bleed = 0f, SetBleed = true });
        }
    }

    private void RestoreFully(EntityUid target)
    {
        _limbPort.Restore(target);
        _rejuvenate.PerformRejuvenate(target);
    }
}

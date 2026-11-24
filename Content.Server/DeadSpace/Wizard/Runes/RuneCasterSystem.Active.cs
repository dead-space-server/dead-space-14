// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Shared.Alert;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Wizard.Runes;

public enum RuneTrack : byte
{
    Exists,
    Living,
    Animated,
}

public sealed class RuneActiveSpell
{
    public readonly string Key;
    public readonly LocId Name;
    public readonly float Cooldown;
    public readonly string Form;
    public readonly TimeSpan Limit;
    public readonly Dictionary<EntityUid, RuneTrack> Tracked = new();
    public readonly HashSet<EntProtoId> Statuses = new();
    public readonly HashSet<string> Alerts = new();
    public float Length;

    public RuneActiveSpell(string key, LocId name, float cooldown, string form, TimeSpan limit)
    {
        Key = key;
        Name = name;
        Cooldown = cooldown;
        Form = form;
        Limit = limit;
    }
}

[RegisterComponent]
public sealed partial class RuneActiveSpellsComponent : Component
{
    public readonly List<RuneActiveSpell> Spells = new();

    public TimeSpan NextCheck;
}

public sealed partial class RuneCasterSystem
{
    private const float ActiveCheckInterval = 0.5f;
    private const float ActiveLimit = 300f;
    private const float CooldownDrift = 0.4f;

    private void InitializeActive()
    {
        SubscribeLocalEvent<RuneCasterComponent, RuneDispelAlertEvent>(OnDispelAlert);
    }

    private RuneActiveSpell BeginActive(Entity<RuneCasterComponent> caster, RuneComposition composition, RuneSpell spell)
    {
        if (spell.MaxActive > 0 && TryComp<RuneActiveSpellsComponent>(caster, out var active))
        {
            var same = active.Spells.FindAll(s => s.Form == spell.Form.Id);
            for (var i = 0; i <= same.Count - spell.MaxActive; i++)
            {
                Dispel(caster, active, same[i]);
                _popup.PopupEntity(Loc.GetString("rune-active-replaced", ("spell", Loc.GetString(same[i].Name))), caster, caster, PopupType.SmallCaution);
            }
        }

        return new RuneActiveSpell(composition.Key, spell.Name, spell.Cooldown, spell.Form.Id, Timing.CurTime + TimeSpan.FromSeconds(ActiveLimit));
    }

    private void SettleActive(Entity<RuneCasterComponent> caster, RuneActiveSpell entry)
    {
        if (!ActiveRemaining(caster, entry, out var remaining))
        {
            SetCooldown(caster, entry.Key, entry.Cooldown, entry.Cooldown, entry.Name);
            return;
        }

        entry.Length = remaining;
        EnsureComp<RuneActiveSpellsComponent>(caster).Spells.Add(entry);
        SetCooldown(caster, entry.Key, remaining + entry.Cooldown, entry.Cooldown, entry.Name, remaining, entry.Length);
    }

    private void Track(RuneCastContext ctx, EntityUid uid, RuneTrack kind)
    {
        ctx.Active?.Tracked.TryAdd(uid, kind);
    }

    private void TrackStatus(RuneCastContext ctx, EntityUid target, EntProtoId status)
    {
        if (target == ctx.Caster)
            ctx.Active?.Statuses.Add(status);
    }

    private static string? GroupOf(RuneCastContext ctx, EntityUid target)
    {
        return target == ctx.Caster ? ctx.Active?.Key : null;
    }

    private void NoteCharm(EntityUid target, ProtoId<AlertPrototype> alert)
    {
        if (_scope is { Active: { } active } ctx && ctx.Caster == target)
            active.Alerts.Add(alert.Id);
    }

    private void UpdateActive()
    {
        var now = Timing.CurTime;
        var query = AllEntityQuery<RuneActiveSpellsComponent, RuneCasterComponent>();
        while (query.MoveNext(out var uid, out var active, out var caster))
        {
            if (now < active.NextCheck)
                continue;

            active.NextCheck = now + TimeSpan.FromSeconds(ActiveCheckInterval);
            for (var i = active.Spells.Count - 1; i >= 0; i--)
            {
                var entry = active.Spells[i];
                if (now >= entry.Limit || !ActiveRemaining(uid, entry, out var remaining))
                {
                    active.Spells.RemoveAt(i);
                    SetCooldown((uid, caster), entry.Key, entry.Cooldown, entry.Cooldown, entry.Name);
                    continue;
                }

                var ready = now + TimeSpan.FromSeconds(remaining + entry.Cooldown);
                if (!caster.Cooldowns.TryGetValue(entry.Key, out var cooldown) ||
                    Math.Abs((cooldown.Ready - ready).TotalSeconds) > CooldownDrift)
                {
                    entry.Length = MathF.Max(entry.Length, remaining);
                    caster.Cooldowns[entry.Key] = new RuneCooldown(ready, entry.Cooldown, entry.Name, now + TimeSpan.FromSeconds(remaining), entry.Length);
                    Dirty(uid, caster);
                }
            }

            if (active.Spells.Count == 0)
                RemCompDeferred<RuneActiveSpellsComponent>(uid);
        }
    }

    private bool ActiveRemaining(EntityUid caster, RuneActiveSpell entry, out float remaining)
    {
        remaining = 0f;
        var alive = false;
        var now = Timing.CurTime;

        foreach (var (uid, kind) in entry.Tracked)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (kind == RuneTrack.Living && _mobState.IsDead(uid))
                continue;

            if (kind == RuneTrack.Animated)
            {
                if (!TryComp<RuneAnimatedComponent>(uid, out var animated))
                    continue;

                remaining = MathF.Max(remaining, (float) (animated.End - now).TotalSeconds);
                alive = true;
                continue;
            }

            if (TryComp<PolymorphedEntityComponent>(uid, out var morph))
            {
                if (morph.Reverted)
                    continue;

                if (morph.Configuration.Duration is { } duration)
                    remaining = MathF.Max(remaining, duration - morph.Time);
            }

            if (TryComp<TimedDespawnComponent>(uid, out var despawn))
                remaining = MathF.Max(remaining, despawn.Lifetime);

            alive = true;
        }

        foreach (var status in entry.Statuses)
        {
            if (!_status.TryGetTime(caster, status, out var time))
                continue;

            alive = true;
            if (time.EndEffectTime is { } end)
                remaining = MathF.Max(remaining, (float) (end - now).TotalSeconds);
        }

        if (GroupRemaining(caster, entry.Key, out var group))
        {
            alive = true;
            remaining = MathF.Max(remaining, group);
        }

        remaining = MathF.Max(0f, remaining);
        return alive;
    }

    private bool GroupRemaining(EntityUid caster, string key, out float remaining)
    {
        remaining = 0f;
        var alive = false;
        var now = Timing.CurTime;

        if (TryComp<RuneTimedComponentsComponent>(caster, out var timed))
        {
            foreach (var entry in timed.Entries)
            {
                if (entry.Group != key)
                    continue;

                alive = true;
                remaining = MathF.Max(remaining, (float) (entry.End - now).TotalSeconds);
            }
        }

        if (TryComp<RuneRegenerationComponent>(caster, out var regeneration))
        {
            foreach (var entry in regeneration.Entries)
            {
                if (entry.Group != key || entry.End <= now)
                    continue;

                alive = true;
                remaining = MathF.Max(remaining, (float) (entry.End - now).TotalSeconds);
            }
        }

        if (TryComp<RuneOrbitShieldComponent>(caster, out var shield) && shield.Group == key && shield.Count > 0)
        {
            alive = true;
            remaining = MathF.Max(remaining, (float) (shield.End - now).TotalSeconds);
        }

        return alive;
    }

    private void OnDispelAlert(Entity<RuneCasterComponent> ent, ref RuneDispelAlertEvent args)
    {
        if (!TryComp<RuneActiveSpellsComponent>(ent, out var active))
            return;

        var alert = args.AlertId.Id;
        var entry = active.Spells.Find(s => s.Alerts.Contains(alert));
        if (entry == null)
            return;

        args.Handled = true;
        Dispel(ent, active, entry);
        _popup.PopupEntity(Loc.GetString("rune-active-dispelled", ("spell", Loc.GetString(entry.Name))), ent, ent);
    }

    private void Dispel(Entity<RuneCasterComponent> caster, RuneActiveSpellsComponent active, RuneActiveSpell entry)
    {
        if (TryComp<RuneTimedComponentsComponent>(caster, out var timed))
        {
            for (var i = timed.Entries.Count - 1; i >= 0; i--)
            {
                if (timed.Entries[i].Group == entry.Key)
                    EndTimedEntry(caster, timed, i);
            }
        }

        if (TryComp<RuneRegenerationComponent>(caster, out var regeneration))
        {
            var removed = regeneration.Entries.FindAll(e => e.Group == entry.Key);
            regeneration.Entries.RemoveAll(e => e.Group == entry.Key);
            foreach (var gone in removed)
            {
                if (gone.Alert is { } alert && !regeneration.Entries.Exists(e => e.Alert == alert))
                    ClearCharm(caster, alert);
            }
        }

        if (TryComp<RuneOrbitShieldComponent>(caster, out var shield) && shield.Group == entry.Key)
            RemComp<RuneOrbitShieldComponent>(caster);

        foreach (var status in entry.Statuses)
            _status.TryRemoveStatusEffect(caster, status);

        foreach (var (uid, kind) in entry.Tracked)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (kind == RuneTrack.Animated && TryComp<RuneAnimatedComponent>(uid, out var animated))
                RevertAnimated(uid, animated);
            else if (HasComp<PolymorphedEntityComponent>(uid))
                _polymorph.Revert(uid);
        }

        active.Spells.Remove(entry);
        SetCooldown(caster, entry.Key, entry.Cooldown, entry.Cooldown, entry.Name);
    }

    private void SetCooldown(Entity<RuneCasterComponent> caster, string key, float seconds, float length, string name, float active = 0f, float activeLength = 0f)
    {
        var now = Timing.CurTime;
        foreach (var (old, cooldown) in caster.Comp.Cooldowns.ToArray())
        {
            if (cooldown.Ready <= now)
                caster.Comp.Cooldowns.Remove(old);
        }

        caster.Comp.Cooldowns[key] = new RuneCooldown(now + TimeSpan.FromSeconds(seconds), length, name, now + TimeSpan.FromSeconds(active), activeLength);
        Dirty(caster);
    }
}

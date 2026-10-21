// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// A linked memento mori keeps its wearer from dying - but not from a boss. Once a player on a boss arena has
/// taken <see cref="ExecuteDamage"/> damage under a memento mori, the boss drops everything, walks up to them and
/// tears them apart: they go to ash as if the pendant came off.
/// </summary>
public sealed class LavalandBossExecutionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly LavalandBossArenaSystem _arenas = default!;
    [Dependency] private readonly LavalandNecropolisArtifactSystem _artifacts = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>Damage on the memento mori's wearer at which the boss comes for them.</summary>
    private const float ExecuteDamage = 350f;

    /// <summary>Tiles per second the boss walks at its victim.</summary>
    private const float ApproachSpeed = 4.5f;

    /// <summary>This close it strikes.</summary>
    private const float Reach = 1.3f;

    /// <summary>Not there by then (walls, a pit in the way): it strikes all the same.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    private static readonly SoundSpecifier ExecuteSound = new SoundPathSpecifier("/Audio/Effects/demon_dies.ogg");

    private readonly HashSet<EntityUid> _targeted = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        ProcessExecutions(now, frameTime);

        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        FindVictims(now);
    }

    private void ProcessExecutions(TimeSpan now, float frameTime)
    {
        _targeted.Clear();
        var query = EntityQueryEnumerator<LavalandBossExecutingComponent, LavalandBossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var executing, out var boss, out var xform))
        {
            var target = executing.Target;
            if (TerminatingOrDeleted(target) ||
                !HasComp<LavalandMementoBoundComponent>(target) ||
                _mobState.IsDead(uid) ||
                !InActiveFight(boss, out _) ||
                Transform(target).MapID != xform.MapID)
            {
                Finish(uid, boss);
                continue;
            }

            _targeted.Add(target);
            var position = _transform.GetWorldPosition(xform);
            var offset = _transform.GetWorldPosition(target) - position;
            var distance = offset.Length();
            if (distance > Reach && now < executing.Until)
            {
                var step = MathF.Min(ApproachSpeed * frameTime, distance - Reach);
                _transform.SetWorldPosition(uid, position + offset / distance * step);
                continue;
            }

            _popup.PopupEntity(Loc.GetString("lavaland-boss-execute", ("boss", uid), ("target", target)), target,
                PopupType.LargeCaution);
            _audio.PlayPvs(ExecuteSound, target);
            _artifacts.TryMori(target);
            Finish(uid, boss);
        }
    }

    private void FindVictims(TimeSpan now)
    {
        var bosses = EntityQueryEnumerator<LavalandBossComponent>();
        while (bosses.MoveNext(out var uid, out var boss))
        {
            if (HasComp<LavalandBossExecutingComponent>(uid) ||
                _mobState.IsDead(uid) ||
                !InActiveFight(boss, out var arena))
            {
                continue;
            }

            var victims = EntityQueryEnumerator<LavalandMementoBoundComponent, DamageableComponent>();
            while (victims.MoveNext(out var victim, out _, out var damageable))
            {
                if (_targeted.Contains(victim) ||
                    damageable.TotalDamage.Float() < ExecuteDamage ||
                    !_arenas.IsEntityInsideInnerArena(victim, arena))
                {
                    continue;
                }

                var executing = EnsureComp<LavalandBossExecutingComponent>(uid);
                executing.Target = victim;
                executing.Until = now + Timeout;
                _targeted.Add(victim);
                SetAi(uid, false);
                _popup.PopupEntity(Loc.GetString("lavaland-boss-execute-start", ("boss", uid), ("target", victim)),
                    uid, PopupType.LargeCaution);
                break;
            }
        }
    }

    private bool InActiveFight(LavalandBossComponent boss, out LavalandBossArenaComponent arena)
    {
        arena = default!;
        if (boss.Arena is not { Valid: true } arenaUid ||
            !TryComp<LavalandBossArenaComponent>(arenaUid, out var found) ||
            found.Ended ||
            !found.FightStarted)
        {
            return false;
        }

        arena = found;
        return true;
    }

    /// <summary>Done: back to its own fight, if the fight is still on.</summary>
    private void Finish(EntityUid uid, LavalandBossComponent boss)
    {
        RemComp<LavalandBossExecutingComponent>(uid);
        if (!TerminatingOrDeleted(uid) && !_mobState.IsDead(uid) && InActiveFight(boss, out _))
            SetAi(uid, true);
    }

    private void SetAi(EntityUid uid, bool enabled)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), enabled, enabled ? 0.75f : 0f);
    }
}

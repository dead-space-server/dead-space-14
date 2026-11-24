// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.PlanetSurvey;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDebugSystem : EntitySystem
{
    private const float ViewRange = 25f;
    private static readonly TimeSpan Rate = TimeSpan.FromSeconds(0.5);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private readonly HashSet<ICommonSession> _viewers = new();
    private readonly StringBuilder _text = new();
    private TimeSpan _next;

    public bool Toggle(ICommonSession session)
    {
        if (_viewers.Remove(session))
        {
            RaiseNetworkEvent(new SurveyDebugMessage(new List<SurveyDebugEntry>()), session);
            return false;
        }

        _viewers.Add(session);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_viewers.Count == 0 || _timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Rate;
        _viewers.RemoveWhere(s => s.Status == Robust.Shared.Enums.SessionStatus.Disconnected);

        foreach (var session in _viewers)
        {
            var entries = new List<SurveyDebugEntry>();
            if (session.AttachedEntity is { } viewer && Transform(viewer).MapUid is { } map)
                Collect(viewer, map, entries);

            RaiseNetworkEvent(new SurveyDebugMessage(entries), session);
        }
    }

    private void Collect(EntityUid viewer, EntityUid map, List<SurveyDebugEntry> entries)
    {
        TryComp<SurveyDirectorComponent>(map, out var director);
        var eye = _xform.GetWorldPosition(viewer);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SurveyMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            if (Vector2.Distance(pos, eye) > ViewRange)
                continue;

            _text.Clear();
            var alert = false;
            var proto = MetaData(uid).EntityPrototype?.ID ?? "?";
            _text.Append(proto.Replace("SurveyMob", string.Empty));
            if (member.Squad >= 0)
                _text.Append($" #{member.Squad}");

            if (director != null && member.Squad >= 0 && member.Squad < director.Squads.Count && director.Squads[member.Squad].Engaged)
                _text.Append(" бой");

            _text.Append($" {Health(uid)}%");
            if (!_mobState.IsAlive(uid))
                _text.Append(_mobState.IsDead(uid) ? " МЁРТВ" : " КРИТ");

            if (TryComp<HTNComponent>(uid, out var htn))
            {
                var board = htn.Blackboard;
                _text.Append('\n').Append(board.GetValueOrDefault<SurveyRole>(SurveyKeys.Role, EntityManager));
                if (htn.Plan is { } plan && plan.Index < plan.Tasks.Count)
                {
                    var op = plan.Tasks[plan.Index].Operator.GetType().Name.Replace("Operator", string.Empty);
                    _text.Append($" {op} {plan.Index + 1}/{plan.Tasks.Count}");
                }
                else
                {
                    _text.Append(" план: нет");
                    alert = _mobState.IsAlive(uid);
                }

                if (board.TryGetValue<EntityUid>(SurveyKeys.Target, out var target, EntityManager) && Exists(target))
                    _text.Append($"\nцель {Name(target)} {Vector2.Distance(pos, _xform.GetWorldPosition(target)):0.0}");
            }

            if (TryComp<NPCSteeringComponent>(uid, out var steering))
            {
                _text.Append($"\nход {steering.Status} путь {steering.CurrentPath.Count}");
                if (steering.Pathfind)
                    _text.Append(" ждёт путь");
                if (steering.Status == SteeringStatus.NoPath)
                    alert = true;
            }

            if (!HasComp<ActiveNPCComponent>(uid))
                _text.Append(" спит");

            var flags = new List<string>();
            if (now < member.CoverUntil)
                flags.Add("укрытие");
            if (now < member.DodgeUntil)
                flags.Add("уклон");
            if (member.Approach)
                flags.Add("обход");
            if (member.Patient != null)
                flags.Add("лечит");
            if (member.SelfHealAt != null)
                flags.Add("перевязка");
            if (member.ReplanPending)
                flags.Add("ждёт план");
            if (member.BlockedTicks > 0)
                flags.Add($"нет линии {member.BlockedTicks}");
            if (member.DisarmedAt != null)
                flags.Add("без ствола");

            if (flags.Count > 0)
                _text.Append('\n').Append(string.Join(", ", flags));

            entries.Add(new SurveyDebugEntry(GetNetEntity(uid), _text.ToString(), alert));
        }
    }

    private int Health(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 100;

        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var limit) &&
            !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out limit))
        {
            return 100;
        }

        var max = limit.Value.Float();
        return max <= 0f ? 100 : (int) MathF.Round(Math.Clamp(1f - damageable.TotalDamage.Float() / max, 0f, 1f) * 100f);
    }
}

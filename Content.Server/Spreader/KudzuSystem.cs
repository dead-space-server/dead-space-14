using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Spreader;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Spreader;

public sealed partial class KudzuSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly ProtoId<EdgeSpreaderPrototype> KudzuGroup = "Kudzu";

    // DS14-start
    private const int GrowthProcessBudget = 128;
    private static readonly TimeSpan GrowthTickInterval = TimeSpan.FromSeconds(0.5);
    private readonly PriorityQueue<GrowthScheduleEntry, TimeSpan> _growthQueue = new();

    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<KudzuComponent> _kudzuQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<GrowingKudzuComponent> _growingQuery;
    // DS14-end

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // DS14-start
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _kudzuQuery = GetEntityQuery<KudzuComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _growingQuery = GetEntityQuery<GrowingKudzuComponent>();
        // DS14-end

        SubscribeLocalEvent<KudzuComponent, ComponentStartup>(SetupKudzu);
        SubscribeLocalEvent<KudzuComponent, SpreadNeighborsEvent>(OnKudzuSpread);
        SubscribeLocalEvent<KudzuComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<GrowingKudzuComponent, ComponentStartup>(OnGrowingStartup); // DS14
    }

    private void OnDamageChanged(EntityUid uid, KudzuComponent component, DamageChangedEvent args)
    {
        // Every time we take any damage, we reduce growth depending on all damage over the growth impact
        //   So the kudzu gets slower growing the more it is hurt.
        var growthDamage = (int) (args.Damageable.TotalDamage / component.GrowthHealth);
        if (growthDamage > 0)
        {
            if (!EnsureComp<GrowingKudzuComponent>(uid, out _))
                component.GrowthLevel = 3;

            component.GrowthLevel = Math.Max(1, component.GrowthLevel - growthDamage);
            UpdateActiveSpreader(uid, component); // DS14

            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                _appearance.SetData(uid, KudzuVisuals.GrowthLevel, component.GrowthLevel, appearance);
            }
        }
    }

    private void OnKudzuSpread(EntityUid uid, KudzuComponent component, ref SpreadNeighborsEvent args)
    {
        if (component.GrowthLevel < 3)
        {
            UpdateActiveSpreader(uid, component); // DS14
            return;
        }

        if (args.NeighborFreeTiles.Count == 0)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(uid);
            return;
        }

        if (!_robustRandom.Prob(component.SpreadChance))
            return;

        var prototype = MetaData(uid).EntityPrototype?.ID;

        if (prototype == null)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(uid);
            return;
        }

        foreach (var neighbor in args.NeighborFreeTiles)
        {
            var neighborUid = Spawn(prototype, _map.GridTileToLocal(neighbor.Tile.GridUid, neighbor.Grid, neighbor.Tile.GridIndices));
            DebugTools.Assert(HasComp<EdgeSpreaderComponent>(neighborUid));
            DebugTools.Assert(Comp<EdgeSpreaderComponent>(neighborUid).Id == KudzuGroup);

            // DS14-start
            if (!TryComp<KudzuComponent>(neighborUid, out var neighborKudzu))
                DebugTools.Assert(false);
            else
                DebugTools.Assert(HasComp<ActiveEdgeSpreaderComponent>(neighborUid) == (neighborKudzu.GrowthLevel >= 3));
            // DS14-end

            args.Updates--;
            if (args.Updates <= 0)
                return;
        }
    }

    private void SetupKudzu(EntityUid uid, KudzuComponent component, ComponentStartup args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
        {
            UpdateActiveSpreader(uid, component); // DS14
            return;
        }

        _appearance.SetData(uid, KudzuVisuals.Variant, _robustRandom.Next(1, component.SpriteVariants), appearance);
        _appearance.SetData(uid, KudzuVisuals.GrowthLevel, 1, appearance);
        UpdateActiveSpreader(uid, component); // DS14
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        // DS14-start
        var curTime = _timing.CurTime;
        var processed = 0;
        var dequeued = 0;
        var maxDequeues = GrowthProcessBudget * 4;

        while (processed < GrowthProcessBudget &&
               dequeued < maxDequeues &&
               _growthQueue.TryPeek(out var entry, out var dueTime) &&
               dueTime <= curTime)
        {
            _growthQueue.Dequeue();
            dequeued++;

            if (!_growingQuery.TryGetComponent(entry.Uid, out var grow) ||
                grow.ScheduleGeneration != entry.Generation ||
                TerminatingOrDeleted(entry.Uid))
            {
                continue;
            }

            if (grow.NextTick > curTime)
            {
                ScheduleGrowth(entry.Uid, grow, grow.NextTick);
                continue;
            }

            processed++;
            ProcessGrowing(entry.Uid, grow, curTime);
        }
        // DS14-end
    }

    // DS14-start
    private void OnGrowingStartup(Entity<GrowingKudzuComponent> ent, ref ComponentStartup args)
    {
        var nextTick = ent.Comp.NextTick > TimeSpan.Zero ? ent.Comp.NextTick : _timing.CurTime;
        ScheduleGrowth(ent.Owner, ent.Comp, nextTick);
    }

    private void ProcessGrowing(EntityUid uid, GrowingKudzuComponent grow, TimeSpan curTime)
    {
        grow.NextTick = curTime + GrowthTickInterval;

        if (!_kudzuQuery.TryGetComponent(uid, out var kudzu))
        {
            RemCompDeferred(uid, grow);
            return;
        }

        if (!_robustRandom.Prob(kudzu.GrowthTickChance))
        {
            ScheduleGrowth(uid, grow, grow.NextTick);
            return;
        }

        if (_damageableQuery.TryGetComponent(uid, out var damage))
        {
            if (damage.TotalDamage > 1.0)
            {
                if (kudzu.DamageRecovery != null)
                {
                    // Healing kudzu recovers damage before growth checks.
                    _damageable.TryChangeDamage(uid, kudzu.DamageRecovery, true);
                }
                if (damage.TotalDamage >= kudzu.GrowthBlock)
                {
                    // Don't grow when quite damaged
                    if (_robustRandom.Prob(0.95f))
                    {
                        ScheduleGrowth(uid, grow, grow.NextTick);
                        return;
                    }
                }
            }
        }

        kudzu.GrowthLevel += 1;

        if (kudzu.GrowthLevel >= 3)
        {
            // Mature kudzu no longer needs scheduled growth.
            RemCompDeferred(uid, grow);
        }
        else
        {
            ScheduleGrowth(uid, grow, grow.NextTick);
        }

        UpdateActiveSpreader(uid, kudzu);

        if (_appearanceQuery.TryGetComponent(uid, out var appearance))
        {
            _appearance.SetData(uid, KudzuVisuals.GrowthLevel, kudzu.GrowthLevel, appearance);
        }
    }

    private void ScheduleGrowth(EntityUid uid, GrowingKudzuComponent grow, TimeSpan when)
    {
        if (TerminatingOrDeleted(uid))
            return;

        grow.ScheduleGeneration++;
        _growthQueue.Enqueue(new GrowthScheduleEntry(uid, grow.ScheduleGeneration), when);
    }

    private void UpdateActiveSpreader(EntityUid uid, KudzuComponent kudzu)
    {
        if (kudzu.GrowthLevel >= 3)
            EnsureComp<ActiveEdgeSpreaderComponent>(uid);
        else
            RemComp<ActiveEdgeSpreaderComponent>(uid);
    }

    private readonly record struct GrowthScheduleEntry(EntityUid Uid, int Generation);
    // DS14-end
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Growth;

/// <summary>
/// Moves animals through their growth stages as time passes.
/// </summary>
public sealed class GrowthSystem : EntitySystem
{
    [Dependency] private readonly AnimalSexSystem _sex = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrowthComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GrowthComponent> ent, ref MapInitEvent args)
    {
        // The sex is needed to name the stage.
        if (TryComp<AnimalSexComponent>(ent, out var sex))
            _sex.EnsureRolled((ent, sex));

        var stages = ent.Comp.Stages;
        if (stages.Count == 0)
            return;

        var initial = stages.Count - 1;
        if (ent.Comp.InitialStage != null)
        {
            var index = stages.FindIndex(stage => stage.Id == ent.Comp.InitialStage);
            if (index >= 0)
                initial = index;
            else
                Log.Error($"{ToPrettyString(ent)} has no growth stage {ent.Comp.InitialStage}");
        }

        SetStage(ent, initial);
    }

    /// <summary>
    /// Puts the animal into the stage with the given index.
    /// </summary>
    public void SetStage(Entity<GrowthComponent> ent, int index)
    {
        var stage = ent.Comp.Stages[index];
        ent.Comp.CurrentStage = index;
        ent.Comp.StageEndTime = stage.Duration == null ? null : _timing.CurTime + stage.Duration;

        if (stage.Components.Count > 0)
            EntityManager.AddComponents(ent, stage.Components, removeExisting: false);

        var ev = new GrowthStageChangedEvent(index, stage);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Whether the animal is in the given stage or a later one. Null asks for the last stage. An animal without
    /// growth stages is always grown up.
    /// </summary>
    public bool IsGrownUp(EntityUid uid, string? stageId)
    {
        if (!TryComp<GrowthComponent>(uid, out var growth) || growth.Stages.Count == 0)
            return true;

        var required = growth.Stages.Count - 1;
        if (stageId != null)
        {
            var index = growth.Stages.FindIndex(stage => stage.Id == stageId);
            if (index >= 0)
                required = index;
        }

        return growth.CurrentStage >= required;
    }

    /// <summary>
    /// Puts the animal into the stage with the given id. Without an id: the first stage if
    /// <paramref name="first"/> is set, the last one otherwise.
    /// </summary>
    public void SetStageById(Entity<GrowthComponent> ent, string? stageId, bool first = false)
    {
        var stages = ent.Comp.Stages;
        if (stages.Count == 0)
            return;

        var index = stageId == null ? (first ? 0 : stages.Count - 1) : stages.FindIndex(stage => stage.Id == stageId);
        if (index < 0)
        {
            Log.Error($"{ToPrettyString(ent)} has no growth stage {stageId}");
            return;
        }

        SetStage(ent, index);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GrowthComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.StageEndTime is not { } end || _timing.CurTime < end)
                continue;

            if (_mobState.IsDead(uid))
                continue;

            if (GrowthRules.TryGetNext(comp.CurrentStage, comp.Stages.Count, out var next))
                SetStage((uid, comp), next);
            else
                comp.StageEndTime = null;
        }
    }
}

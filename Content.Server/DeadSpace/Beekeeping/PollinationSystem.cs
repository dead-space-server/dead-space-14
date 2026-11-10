// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Popups;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;

/// <summary>
/// What a bee does to a plant, see <see cref="PollinationComponent"/>.
/// </summary>
public sealed class PollinationSystem : EntitySystem
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PlantHolderSystem _holder = default!;
    [Dependency] private readonly PlantSystem _plant = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollinationComponent, PlantPollinatedEvent>(OnPollinated);
        SubscribeLocalEvent<PollinationComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Whether a bee can pollinate the plant now: it flowers, lives, and was not visited a short while ago.
    /// </summary>
    public bool CanPollinate(Entity<PollinationComponent> plant)
    {
        if (!plant.Comp.IsFlowering || _timing.CurTime < plant.Comp.NextAvailable)
            return false;

        return !TryComp<PlantHolderComponent>(plant, out var holder) || !holder.Dead;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only the plants that carry a boost have anything to do, and they are checked once a second.
        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + SweepInterval;

        var query = EntityQueryEnumerator<PollinationComponent>();
        while (query.MoveNext(out var uid, out var pollination))
        {
            if (pollination.BoostEnd != TimeSpan.Zero && now >= pollination.BoostEnd)
                EndBoost((uid, pollination));
        }
    }

    private void OnPollinated(Entity<PollinationComponent> ent, ref PlantPollinatedEvent args)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;
        if (!CanPollinate(ent))
            return;

        comp.NextAvailable = now + comp.Cooldown;

        if (comp.AgeBonus != 0)
            _holder.AdjustsAge(ent.Owner, comp.AgeBonus);

        // A new visit while the boost is on does not stack, it only extends it.
        if (comp.AppliedPotencyDelta == 0f && comp.PotencyBonus > 0f && TryComp<PlantComponent>(ent, out var plant))
        {
            var delta = plant.Potency * comp.PotencyBonus;
            _plant.AdjustPotency((ent.Owner, plant), delta);
            comp.AppliedPotencyDelta = delta;
        }

        comp.BoostEnd = now + comp.BoostDuration;

        _audio.PlayPvs(comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("pollination-success-popup"), ent, Filter.Pvs(ent), true);
    }

    private void EndBoost(Entity<PollinationComponent> ent)
    {
        if (ent.Comp.AppliedPotencyDelta != 0f && TryComp<PlantComponent>(ent, out var plant))
            _plant.AdjustPotency((ent.Owner, plant), -ent.Comp.AppliedPotencyDelta);

        ent.Comp.AppliedPotencyDelta = 0f;
        ent.Comp.BoostEnd = TimeSpan.Zero;
    }

    private void OnExamined(Entity<PollinationComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!ent.Comp.IsFlowering)
        {
            args.PushMarkup(Loc.GetString("pollination-examine-not-flowering"));
            return;
        }

        if (ent.Comp.BoostEnd != TimeSpan.Zero)
        {
            var remaining = Math.Max(0, (int) (ent.Comp.BoostEnd - _timing.CurTime).TotalSeconds);
            args.PushMarkup(Loc.GetString("pollination-examine-boosted", ("time", remaining)));
            return;
        }

        args.PushMarkup(Loc.GetString(_timing.CurTime < ent.Comp.NextAvailable
            ? "pollination-examine-pollinated"
            : "pollination-examine-flowering"));
    }
}

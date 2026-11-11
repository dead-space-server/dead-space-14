// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;

/// <summary>
/// The hive, see <see cref="BeeHiveComponent"/>. The bees fly (see <see cref="BeeAISystem"/>) and bring the pollen.
/// </summary>
public sealed class BeeHiveSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly BeeAISystem _beeAI = default!;
    [Dependency] private readonly HiveFrameSystem _frames = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeeHiveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BeeHiveComponent, BeePollenDepositedEvent>(OnPollenDeposited);
        SubscribeLocalEvent<BeeHiveComponent, EntRemovedFromContainerMessage>(OnQueenRemoved);
    }

    /// <summary>
    /// A hive works only with a queen in its slot.
    /// </summary>
    public bool IsActive(EntityUid uid, BeeHiveComponent? hive = null)
    {
        if (!Resolve(uid, ref hive, false))
            return false;

        var queen = _slots.GetItemOrNull(uid, hive.QueenSlotId);
        return queen != null && HasComp<QueenBeeComponent>(queen.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        var dt = (float) TickInterval.TotalSeconds;

        var query = EntityQueryEnumerator<BeeHiveComponent>();
        while (query.MoveNext(out var uid, out var hive))
        {
            if (!IsActive(uid, hive))
                continue;

            hive.SpawnTimer += dt;
            if (hive.SpawnTimer >= hive.SpawnInterval && hive.Bees.Count < hive.MaxBees)
            {
                hive.SpawnTimer = 0f;
                SpawnBee((uid, hive));
            }

            hive.ProductionTimer += dt;
            if (hive.ProductionTimer < hive.ProductionInterval)
                continue;

            hive.ProductionTimer = 0f;

            TryProduce((uid, hive));
        }
    }

    private void SpawnBee(Entity<BeeHiveComponent> hive)
    {
        var bee = Spawn(hive.Comp.BeePrototype, Transform(hive).Coordinates);
        if (!TryComp<BeeComponent>(bee, out var beeComp))
        {
            Log.Error($"The bee prototype {hive.Comp.BeePrototype} of {ToPrettyString(hive)} has no Bee component.");
            QueueDel(bee);
            return;
        }

        beeComp.HiveOwner = hive;
        hive.Comp.Bees.Add(bee);
    }

    // The queen was taken out: the bees go home.
    private void OnQueenRemoved(Entity<BeeHiveComponent> hive, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != hive.Comp.QueenSlotId)
            return;

        foreach (var bee in hive.Comp.Bees)
        {
            if (TryComp<BeeComponent>(bee, out var beeComp))
                _beeAI.RecallToHive((bee, beeComp));
        }
    }

    private void OnPollenDeposited(Entity<BeeHiveComponent> hive, ref BeePollenDepositedEvent args)
    {
        hive.Comp.PollenStored = MathF.Min(hive.Comp.PollenStored + args.Amount, hive.Comp.MaxPollen);
    }

    private void TryProduce(Entity<BeeHiveComponent> hive)
    {
        var comp = hive.Comp;
        if (comp.PollenStored < comp.PollenPerHoney || !TryComp<StorageComponent>(hive, out var storage))
            return;

        foreach (var item in storage.StoredItems.Keys)
        {
            if (!TryComp<HiveFrameComponent>(item, out var frame) || frame.HoneycombAmount >= frame.MaxCapacity)
                continue;

            var honey = MathF.Floor(comp.PollenStored / comp.PollenPerHoney);
            var produced = MathF.Min(honey, frame.MaxCapacity - frame.HoneycombAmount);
            if (produced < 1f)
                return;

            frame.HoneycombAmount += produced;
            comp.PollenStored -= produced * comp.PollenPerHoney;
            _frames.UpdateVisuals((item, frame));
            return;
        }
    }

    private void OnExamined(Entity<BeeHiveComponent> hive, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("beehive-examine-pollen",
            ("current", (int) hive.Comp.PollenStored),
            ("max", (int) hive.Comp.MaxPollen)));

        args.PushMarkup(Loc.GetString(IsActive(hive, hive.Comp) ? "beehive-examine-active" : "beehive-examine-inactive"));
    }
}

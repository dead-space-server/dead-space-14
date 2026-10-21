// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Stacks;

namespace Content.Shared.Salvage.Fulton;

/// <summary>
/// DS14: a stack of fultons can hold fultons linked to different beacons (<see cref="FultonComponent.BeaconQueue"/>).
/// Stacks linked to different beacons don't merge on their own; merged on purpose (<see cref="ForceMergeBeacons"/>),
/// they keep each fulton's beacon and are used one after another.
/// </summary>
public abstract partial class SharedFultonSystem
{
    /// <summary>While set, fulton stacks linked to different beacons may merge.</summary>
    protected bool ForceMergeBeacons;

    /// <summary>Merges a fulton stack into another linked to other beacons: each fulton keeps its beacon.</summary>
    public void ForceMerge(EntityUid donor, EntityUid recipient)
    {
        ForceMergeBeacons = true;
        try
        {
            _stack.TryMergeStacks(donor, recipient, out _);
        }
        finally
        {
            ForceMergeBeacons = false;
        }
    }

    private void InitializeBeaconQueue()
    {
        SubscribeLocalEvent<FultonComponent, StackMergeAttemptEvent>(OnFultonMergeAttempt);
        SubscribeLocalEvent<FultonComponent, StackMergedEvent>(OnFultonMerged);
    }

    /// <summary>The beacon the next fulton of the stack goes to.</summary>
    public EntityUid? NextBeacon(FultonComponent fulton)
    {
        return fulton.BeaconQueue.Count > 0 ? OrNull(fulton.BeaconQueue[0]) : fulton.Beacon;
    }

    /// <summary>The queue keeps "no beacon" as <see cref="EntityUid.Invalid"/>: a nullable list can't be networked.</summary>
    private static EntityUid? OrNull(EntityUid uid) => uid.IsValid() ? uid : null;

    /// <summary>Every fulton of the stack's beacon, in the order they are used.</summary>
    public List<EntityUid?> ExpandBeacons(Entity<FultonComponent> fulton)
    {
        var count = TryComp<StackComponent>(fulton, out var stack) ? stack.Count : 1;
        var list = fulton.Comp.BeaconQueue.Take(count).Select(OrNull).ToList();
        while (list.Count < count)
            list.Add(fulton.Comp.Beacon);
        return list;
    }

    /// <summary>Stores per-fulton beacons on the stack: the common tail as its beacon, the rest as the queue.</summary>
    protected void SetBeacons(Entity<FultonComponent> fulton, List<EntityUid?> beacons)
    {
        if (beacons.Count == 0)
        {
            fulton.Comp.BeaconQueue.Clear();
            Dirty(fulton);
            return;
        }

        var last = beacons[^1];
        var tail = beacons.Count;
        while (tail > 0 && beacons[tail - 1] == last)
            tail--;

        fulton.Comp.Beacon = last;
        fulton.Comp.BeaconQueue = beacons.Take(tail).Select(b => b ?? EntityUid.Invalid).ToList();
        Dirty(fulton);
    }

    private EntityUid? TakeNextBeacon(Entity<FultonComponent> fulton)
    {
        if (fulton.Comp.BeaconQueue.Count == 0)
            return fulton.Comp.Beacon;

        var beacon = OrNull(fulton.Comp.BeaconQueue[0]);
        fulton.Comp.BeaconQueue.RemoveAt(0);
        Dirty(fulton);
        return beacon;
    }

    /// <summary>Same beacon for every fulton of both stacks.</summary>
    public bool SameBeacons(Entity<FultonComponent> a, Entity<FultonComponent> b)
    {
        return ExpandBeacons(a).Concat(ExpandBeacons(b)).Distinct().Count() <= 1;
    }

    private void OnFultonMergeAttempt(Entity<FultonComponent> ent, ref StackMergeAttemptEvent args)
    {
        if (ForceMergeBeacons || !TryComp<FultonComponent>(args.Recipient, out var recipient))
            return;

        if (!SameBeacons(ent, (args.Recipient, recipient)))
            args.Cancelled = true;
    }

    /// <summary>The fultons moved go to the end of the recipient's line, with their own beacons.</summary>
    private void OnFultonMerged(Entity<FultonComponent> ent, ref StackMergedEvent args)
    {
        if (!TryComp<FultonComponent>(args.Recipient, out var recipient))
            return;

        var donor = ExpandBeacons(ent);
        var moved = Math.Min(args.Amount, donor.Count);
        var into = ExpandBeacons((args.Recipient, recipient));
        into.AddRange(donor.Take(moved));
        SetBeacons((args.Recipient, recipient), into);
        SetBeacons(ent, donor.Skip(moved).ToList());
    }

    /// <summary>A split takes the last fultons of the line with their beacons.</summary>
    private void SplitBeaconQueue(Entity<FultonComponent> source, Entity<FultonComponent> split)
    {
        if (source.Comp.BeaconQueue.Count == 0)
        {
            split.Comp.BeaconQueue.Clear();
            return;
        }

        var kept = TryComp<StackComponent>(source, out var sourceStack) ? sourceStack.Count : 1;
        var taken = TryComp<StackComponent>(split, out var splitStack) ? splitStack.Count : 1;
        var all = source.Comp.BeaconQueue.Select(OrNull).ToList();
        while (all.Count < kept + taken)
            all.Add(source.Comp.Beacon);

        SetBeacons(source, all.Take(kept).ToList());
        SetBeacons(split, all.Skip(kept).Take(taken).ToList());
    }
}

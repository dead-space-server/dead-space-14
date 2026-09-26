using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Fluids.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    private static readonly TimeSpan EvaporationCooldown = TimeSpan.FromSeconds(1);

    private void OnEvaporationMapInit(Entity<EvaporationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + EvaporationCooldown;
        Dirty(ent);
    }

    private void UpdateEvaporation(EntityUid uid, Solution solution)
    {
        if (!HasEvaporatingReagents(solution))
        {
            RemComp<EvaporationComponent>(uid);
            return;
        }

        if (_evaporationQuery.HasComp(uid))
            return;

        var evaporation = AddComp<EvaporationComponent>(uid);
        evaporation.NextTick = _timing.CurTime + EvaporationCooldown;
        Dirty(uid, evaporation);
    }

    private void TickEvaporation()
    {
        var query = EntityQueryEnumerator<EvaporationComponent, PuddleComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var evaporation, out var puddle))
        {
            if (evaporation.NextTick > curTime)
                continue;

            if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var puddleSolution) ||
                !HasEvaporatingReagents(puddleSolution))
            {
                RemComp<EvaporationComponent>(uid);
                continue;
            }

            evaporation.NextTick += EvaporationCooldown;
            Dirty(uid, evaporation);

            if (evaporation.EvaporationAmount <= FixedPoint2.Zero)
                continue;

            // If we have multiple evaporating reagents in one puddle, just take the average evaporation speed and apply
            // that to all of them.
            var evaporationSpeeds = GetEvaporationSpeeds(puddleSolution);
            var evaporationSpeed = evaporationSpeeds.Values.Sum(speed => speed.Double()) / evaporationSpeeds.Count;
            var evaporationRate = evaporation.EvaporationAmount.Double() * EvaporationCooldown.TotalSeconds * evaporationSpeed;
            var volume = puddleSolution.Volume.Double();

            for (var i = puddleSolution.Contents.Count - 1; i >= 0; i--)
            {
                var (reagent, quantity) = puddleSolution.Contents[i];
                if (!evaporationSpeeds.ContainsKey(reagent.Prototype))
                    continue;

                // Trace amounts must still evaporate when their share rounds below FixedPoint2 precision.
                var reagentTick = FixedPoint2.Max(FixedPoint2.Epsilon,
                    FixedPoint2.New(evaporationRate * quantity.Double() / volume));
                puddleSolution.RemoveReagent(reagent, FixedPoint2.Min(quantity, reagentTick));
            }

            // Despawn if we're done
            if (puddleSolution.Volume == FixedPoint2.Zero)
            {
                // Spawn a *sparkle*
                if (_net.IsServer) // TODO: Change this once we have entity spawn prediction V2
                    SpawnAttachedTo(evaporation.EvaporationEffect, Transform(uid).Coordinates);

                PredictedQueueDel(uid);
            }

            _solutionContainerSystem.UpdateChemicals(puddle.Solution.Value);
        }
    }


    private bool HasEvaporatingReagents(Solution solution)
    {
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (quantity > FixedPoint2.Zero && _prototypeManager.Index<ReagentPrototype>(reagent.Prototype).EvaporationSpeed > FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    public ProtoId<ReagentPrototype>[] GetAbsorbentReagents(Solution solution)
    {
        var absorbentReagents = new List<ProtoId<ReagentPrototype>>();
        foreach (ReagentPrototype solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.Absorbent)
                absorbentReagents.Add(solProto.ID);
        }
        return absorbentReagents.ToArray();
    }

    public bool CanFullyEvaporate(Solution solution)
    {
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (quantity > FixedPoint2.Zero && _prototypeManager.Index<ReagentPrototype>(reagent.Prototype).EvaporationSpeed <= FixedPoint2.Zero)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a mapping of evaporating speed of the reagents within a solution.
    /// The speed at which a solution evaporates is the average of the speed of all evaporating reagents in it.
    /// </summary>
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> GetEvaporationSpeeds(Solution solution)
    {
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> evaporatingSpeeds = [];
        foreach (var (reagent, quantity) in solution.Contents)
        {
            var prototype = _prototypeManager.Index<ReagentPrototype>(reagent.Prototype);
            if (quantity > FixedPoint2.Zero && prototype.EvaporationSpeed > FixedPoint2.Zero)
                evaporatingSpeeds.TryAdd(reagent.Prototype, prototype.EvaporationSpeed);
        }
        return evaporatingSpeeds;
    }
}

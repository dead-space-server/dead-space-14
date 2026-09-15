using Content.Server.Fluids.Components;
using Content.Server.Spreader;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Fluids.EntitySystems;

/// <summary>
/// Handles solutions on floors. Also handles the spreader logic for where the solution overflows a specified volume.
/// </summary>
public sealed partial class PuddleSystem : SharedPuddleSystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SpreaderSystem _spreader = default!;

    private EntityQuery<PuddleComponent> _puddleQuery;
    private readonly HashSet<EntityUid> _changedPuddles = [];

    /*
     * TODO: Need some sort of way to do blood slash / vomit solution spill on its own
     * This would then evaporate into the puddle tile below
     */

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _puddleQuery = GetEntityQuery<PuddleComponent>();

        SubscribeLocalEvent<PuddleComponent, SpreadNeighborsEvent>(OnPuddleSpread);
        SubscribeLocalEvent<PuddleComponent, SlipEvent>(OnPuddleSlip);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _changedPuddles)
        {
            if (TerminatingOrDeleted(uid) ||
                !_puddleQuery.TryGetComponent(uid, out var puddle) ||
                !TryComp<EdgeSpreaderComponent>(uid, out var spreader))
            {
                continue;
            }

            if (IsOverflowing(uid, puddle))
                EnsureComp<ActiveEdgeSpreaderComponent>(uid);

            _spreader.GetNeighbors(uid, Transform(uid), spreader.Id, out _, out var neighbors);
            foreach (var neighbor in neighbors)
            {
                if (_puddleQuery.TryGetComponent(neighbor, out var neighborPuddle) &&
                    IsOverflowing(neighbor, neighborPuddle))
                {
                    EnsureComp<ActiveEdgeSpreaderComponent>(neighbor);
                }
            }
        }

        _changedPuddles.Clear();
    }

    protected override void OnSolutionUpdate(Entity<PuddleComponent> entity, ref SolutionContainerChangedEvent args)
    {
        base.OnSolutionUpdate(entity, ref args);

        if (args.SolutionId == entity.Comp.SolutionName && args.Solution.Volume > FixedPoint2.Zero)
            _changedPuddles.Add(entity);
    }

    private void OnPuddleSpread(Entity<PuddleComponent> entity, ref SpreadNeighborsEvent args)
    {
        if (!_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.SolutionName,
                ref entity.Comp.Solution, out var solution) ||
            GetOverflowVolume(entity.Comp, solution) <= FixedPoint2.Zero)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(entity);
            return;
        }

        var source = entity.Comp.Solution.Value;
        var initialVolume = solution.Volume;

        if (args.NeighborFreeTiles.Count > 0)
        {
            _random.Shuffle(args.NeighborFreeTiles);
            for (var i = 0; i < args.NeighborFreeTiles.Count && args.Updates > 0; i++)
            {
                var amount = GetOverflowVolume(entity.Comp, solution) / (args.NeighborFreeTiles.Count - i);
                if (amount <= FixedPoint2.Zero)
                    continue;

                var split = solution.SplitSolution(amount);
                if (!TrySpillAt(args.NeighborFreeTiles[i].Tile, split, out _, sound: false))
                    solution.AddSolution(split, _prototypeManager);

                args.Updates--;
            }
        }
        else
        {
            var neighbors = new ValueList<(PuddleComponent Puddle, Entity<SolutionComponent> Solution)>();
            foreach (var uid in args.Neighbors)
            {
                if (!_puddleQuery.TryGetComponent(uid, out var puddle) ||
                    !_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution,
                        out var neighborSolution) || CanFullyEvaporate(neighborSolution))
                {
                    continue;
                }

                neighbors.Add((puddle, puddle.Solution.Value));
            }

            neighbors.Sort((a, b) => a.Solution.Comp.Solution.Volume.CompareTo(b.Solution.Comp.Solution.Volume));

            foreach (var (puddle, neighbor) in neighbors)
            {
                if (args.Updates <= 0)
                    break;

                var neighborSolution = neighbor.Comp.Solution;
                var capacity = GetEffectiveOverflowVolume(puddle, neighborSolution);
                if (capacity >= solution.Volume)
                    continue;

                var amount = FixedPoint2.Min(capacity - neighborSolution.Volume, GetOverflowVolume(entity.Comp, solution));
                if (TryTransferOverflow(source, neighbor, amount))
                    args.Updates--;
            }

            var totalVolume = solution.Volume;
            var neighborCount = 0;
            foreach (var (_, neighbor) in neighbors)
            {
                if (TerminatingOrDeleted(neighbor))
                    continue;

                totalVolume += neighbor.Comp.Solution.Volume;
                neighborCount++;
            }

            var averageVolume = totalVolume / (neighborCount + 1);
            foreach (var (_, neighbor) in neighbors)
            {
                if (args.Updates <= 0 || averageVolume >= solution.Volume)
                    break;

                var amount = FixedPoint2.Min(averageVolume - neighbor.Comp.Solution.Volume,
                    GetOverflowVolume(entity.Comp, solution));
                if (TryTransferOverflow(source, neighbor, amount))
                    args.Updates--;
            }
        }

        if (TerminatingOrDeleted(source) || TerminatingOrDeleted(entity))
            return;

        if (solution.Volume != initialVolume)
            _solutionContainerSystem.UpdateChemicals(source);

        if (solution.Volume == initialVolume || GetOverflowVolume(entity.Comp, solution) <= FixedPoint2.Zero)
            RemCompDeferred<ActiveEdgeSpreaderComponent>(entity);
    }

    private bool TryTransferOverflow(Entity<SolutionComponent> source, Entity<SolutionComponent> target, FixedPoint2 amount)
    {
        if (TerminatingOrDeleted(source) || TerminatingOrDeleted(target))
            return false;

        amount = FixedPoint2.Min(amount, target.Comp.Solution.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
            return false;

        var split = source.Comp.Solution.SplitSolution(amount);
        if (_solutionContainerSystem.TryAddSolution(target, split))
            return true;

        source.Comp.Solution.AddSolution(split, _prototypeManager);
        return false;
    }

    private FixedPoint2 GetOverflowVolume(PuddleComponent puddle, Solution solution)
    {
        return FixedPoint2.Max(FixedPoint2.Zero, solution.Volume - GetEffectiveOverflowVolume(puddle, solution));
    }

    private void OnPuddleSlip(Entity<PuddleComponent> entity, ref SlipEvent args)
    {
        // Reactive entities have a chance to get a touch reaction from slipping on a puddle
        // (i.e. it is implied they fell face first onto it or something)
        if (!HasComp<ReactiveComponent>(args.Slipped) || HasComp<SlidingComponent>(args.Slipped))
            return;

        // Eventually probably have some system of 'body coverage' to tweak the probability but for now just 0.5
        // (implying that spacemen have a 50% chance to either land on their ass or their face)
        if (!_random.Prob(0.5f))
            return;

        if (!_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.SolutionName, ref entity.Comp.Solution,
                out var solution))
            return;

        Popups.PopupEntity(Loc.GetString("puddle-component-slipped-touch-reaction", ("puddle", entity.Owner)),
            args.Slipped, args.Slipped, PopupType.SmallCaution);

        // Take 15% of the puddle solution
        var splitSol = _solutionContainerSystem.SplitSolution(entity.Comp.Solution.Value, solution.Volume * 0.15f);
        Reactive.DoEntityReaction(args.Slipped, splitSol, ReactionMethod.Touch);
    }

    /// <summary>
    ///     Gets the current volume of the given puddle, which may not necessarily be PuddleVolume.
    /// </summary>
    public FixedPoint2 CurrentVolume(EntityUid uid, PuddleComponent? puddleComponent = null)
    {
        if (!Resolve(uid, ref puddleComponent))
            return FixedPoint2.Zero;

        return _solutionContainerSystem.ResolveSolution(uid, puddleComponent.SolutionName, ref puddleComponent.Solution,
            out var solution)
            ? solution.Volume
            : FixedPoint2.Zero;
    }

    /// <summary>
    /// Try to add solution to <paramref name="puddleUid"/>.
    /// </summary>
    /// <param name="puddleUid">Puddle to which we add</param>
    /// <param name="addedSolution">Solution that is added to puddleComponent</param>
    /// <param name="sound">Play sound on overflow</param>
    /// <param name="checkForOverflow">Overflow on encountered values</param>
    /// <param name="puddleComponent">Optional resolved PuddleComponent</param>
    /// <returns></returns>
    public bool TryAddSolution(EntityUid puddleUid,
        Solution addedSolution,
        bool sound = true,
        bool checkForOverflow = true,
        PuddleComponent? puddleComponent = null,
        SolutionContainerManagerComponent? sol = null)
    {
        if (!Resolve(puddleUid, ref puddleComponent, ref sol))
            return false;

        _solutionContainerSystem.EnsureAllSolutions((puddleUid, sol));

        if (addedSolution.Volume == 0 ||
            !_solutionContainerSystem.ResolveSolution(puddleUid, puddleComponent.SolutionName,
                ref puddleComponent.Solution))
        {
            return false;
        }

        var currentSolution = puddleComponent.Solution.Value.Comp.Solution;
        var solutionToAdd = TrimDecorativePuddleSolution(puddleComponent, currentSolution, addedSolution);

        if (solutionToAdd.Volume <= FixedPoint2.Zero)
            return true;

        _solutionContainerSystem.AddSolution(puddleComponent.Solution.Value, solutionToAdd);

        if (checkForOverflow)
        {
            if (IsOverflowing(puddleUid, puddleComponent))
                EnsureComp<ActiveEdgeSpreaderComponent>(puddleUid);
            else
                RemCompDeferred<ActiveEdgeSpreaderComponent>(puddleUid);
        }

        if (!sound)
        {
            return true;
        }

        Audio.PlayPvs(puddleComponent.SpillSound, puddleUid);
        return true;
    }

    // DS14-start: reduce physics load from decorative puddles.
    private Solution TrimDecorativePuddleSolution(PuddleComponent puddle, Solution currentSolution, Solution addedSolution)
    {
        if (!IsDecorativePuddleSolution(addedSolution) ||
            currentSolution.Volume > FixedPoint2.Zero && !IsDecorativePuddleSolution(currentSolution))
        {
            return addedSolution;
        }

        var remaining = puddle.DecorativePuddleMaxVolume - currentSolution.Volume;
        if (remaining <= FixedPoint2.Zero)
            return new Solution();

        return addedSolution.Volume > remaining
            ? addedSolution.Clone().SplitSolution(remaining)
            : addedSolution;
    }

    private FixedPoint2 GetEffectiveOverflowVolume(PuddleComponent puddle, Solution solution)
    {
        var overflowVolume = IsDecorativePuddleSolution(solution)
            ? puddle.DecorativePuddleOverflowVolume
            : puddle.OverflowVolume;

        return overflowVolume > FixedPoint2.Zero
            ? overflowVolume
            : FixedPoint2.New(1);
    }

    private FixedPoint2 GetEffectiveOverflowVolume(PuddleComponent puddle, Solution currentSolution, Solution addedSolution)
    {
        var decorative = IsDecorativePuddleSolution(addedSolution) &&
                         (currentSolution.Volume <= FixedPoint2.Zero || IsDecorativePuddleSolution(currentSolution));
        var overflowVolume = decorative
            ? puddle.DecorativePuddleOverflowVolume
            : puddle.OverflowVolume;

        return overflowVolume > FixedPoint2.Zero
            ? overflowVolume
            : FixedPoint2.New(1);
    }
    // DS14-end

    /// <summary>
    ///     Whether adding this solution to this puddle would overflow.
    /// </summary>
    public bool WouldOverflow(EntityUid uid, Solution solution, PuddleComponent? puddle = null)
    {
        if (!Resolve(uid, ref puddle))
            return false;

        if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var currentSolution))
            return false;

        return currentSolution.Volume + solution.Volume > GetEffectiveOverflowVolume(puddle, currentSolution, solution);
    }

    /// <summary>
    ///     Whether adding this solution to this puddle would overflow.
    /// </summary>
    private bool IsOverflowing(EntityUid uid, PuddleComponent? puddle = null)
    {
        if (!Resolve(uid, ref puddle))
            return false;

        if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
            return false;

        return solution.Volume > GetEffectiveOverflowVolume(puddle, solution);
    }

    #region Spill

    // TODO: This can be predicted once https://github.com/space-wizards/RobustToolbox/pull/5849 is merged
    /// <inheritdoc/>
    public override bool TrySplashSpillAt(Entity<SpillableComponent?> entity,
        EntityCoordinates coordinates,
        out EntityUid puddleUid,
        out Solution spilled,
        bool sound = true,
        EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;
        spilled = new Solution();

        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (!_solutionContainerSystem.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out var solution))
            return false;

        spilled = solution.Value.Comp.Solution;

        return TrySplashSpillAt(entity, coordinates, solution.Value, out puddleUid, sound, user);
    }

    private bool TrySplashSpillAt(EntityUid entity,
        EntityCoordinates coordinates,
        Entity<SolutionComponent> solution,
        out EntityUid puddleUid,
        bool sound = true,
        EntityUid? user = null)
    {
        var result = TrySplashSpillAt(entity, coordinates, solution.Comp.Solution, out puddleUid, sound, user);
        _solutionContainerSystem.UpdateChemicals(solution);
        return result;
    }

    public override bool TrySplashSpillAt(EntityUid entity,
        EntityCoordinates coordinates,
        Solution solution,
        out EntityUid puddleUid,
        bool sound = true,
        EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;

        if (solution.Volume == 0)
            return false;

        var spilled = solution.SplitSolution(solution.Volume);
        var targets = new List<EntityUid>();
        var reactive = new HashSet<Entity<ReactiveComponent>>();
        _lookup.GetEntitiesInRange(coordinates, 1.0f, reactive);

        // Get reactive entities nearby--if there are some, it'll spill a bit on them instead.
        foreach (var ent in reactive)
        {
            // sorry! no overload for returning uid, so .owner must be used
            var owner = ent.Owner;

            // between 5 and 30%
            var splitAmount = spilled.Volume * _random.NextFloat(0.05f, 0.30f);
            var splitSolution = spilled.SplitSolution(splitAmount);

            if (user != null)
            {
                AdminLogger.Add(LogType.Landed,
                    $"{ToPrettyString(user.Value):user} threw {ToPrettyString(entity):entity} which splashed a solution {SharedSolutionContainerSystem.ToPrettyString(spilled):solution} onto {ToPrettyString(owner):target}");
            }

            targets.Add(owner);
            Reactive.DoEntityReaction(owner, splitSolution, ReactionMethod.Touch);
            Popups.PopupEntity(Loc.GetString("spill-land-spilled-on-other",
                    ("spillable", entity),
                    ("target", Identity.Entity(owner, EntityManager))),
                owner,
                PopupType.SmallCaution);
        }

        _color.RaiseEffect(spilled.GetColor(_prototypeManager), targets,
            Filter.Pvs(entity, entityManager: EntityManager));

        return TrySpillAt(coordinates, spilled, out puddleUid, sound);
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityCoordinates coordinates, Solution solution, out EntityUid puddleUid, bool sound = true)
    {
        if (solution.Volume == 0)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        var gridUid = _transform.GetGrid(coordinates);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        return TrySpillAt(_map.GetTileRef(gridUid.Value, mapGrid, coordinates), solution, out puddleUid, sound);
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityUid uid, Solution solution, out EntityUid puddleUid, bool sound = true,
        TransformComponent? transformComponent = null)
    {
        if (!Resolve(uid, ref transformComponent, false))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        return TrySpillAt(transformComponent.Coordinates, solution, out puddleUid, sound: sound);
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(TileRef tileRef, Solution solution, out EntityUid puddleUid, bool sound = true,
        bool tileReact = true)
    {
        if (solution.Volume <= 0)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // If space return early, let that spill go out into the void
        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // Let's not spill to invalid grids.
        var gridId = tileRef.GridUid;
        if (!TryComp<MapGridComponent>(gridId, out var mapGrid))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        if (tileReact)
        {
            // First, do all tile reactions
            DoTileReactions(tileRef, solution);
        }

        // Tile reactions used up everything.
        if (solution.Volume == FixedPoint2.Zero)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // Get normalized co-ordinate for spill location and spill it in the centre
        // TODO: Does SnapGrid or something else already do this?
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridId, mapGrid, tileRef.GridIndices);
        var puddleQuery = GetEntityQuery<PuddleComponent>();
        var sparklesQuery = GetEntityQuery<EvaporationSparkleComponent>();

        while (anchored.MoveNext(out var ent))
        {
            // If there's existing sparkles then delete it
            if (sparklesQuery.TryGetComponent(ent, out var sparkles))
            {
                QueueDel(ent.Value);
                continue;
            }

            if (!puddleQuery.TryGetComponent(ent, out var puddle))
                continue;

            if (TryAddSolution(ent.Value, solution, sound, puddleComponent: puddle))
            {
                puddleUid = ent.Value;
                return true;
            }

            puddleUid = EntityUid.Invalid;
            return false;
        }

        var coords = _map.GridTileToLocal(gridId, mapGrid, tileRef.GridIndices);
        puddleUid = Spawn("Puddle", coords);
        EnsureComp<PuddleComponent>(puddleUid);
        TryAddSolution(puddleUid, solution, sound);

        return true;
    }

    #endregion

    /// <summary>
    /// Tries to get the relevant puddle entity for a tile.
    /// </summary>
    public bool TryGetPuddle(TileRef tile, out EntityUid puddleUid)
    {
        puddleUid = EntityUid.Invalid;

        if (!TryComp<MapGridComponent>(tile.GridUid, out var grid))
            return false;

        var anc = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        var puddleQuery = GetEntityQuery<PuddleComponent>();

        while (anc.MoveNext(out var ent))
        {
            if (!puddleQuery.HasComponent(ent.Value))
                continue;

            puddleUid = ent.Value;
            return true;
        }

        return false;
    }
}

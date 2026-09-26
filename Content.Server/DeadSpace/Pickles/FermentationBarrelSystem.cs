// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.Components;
using Content.Shared.DeadSpace.Pickles;
using Content.Shared.DeadSpace.Pickles.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server.DeadSpace.Pickles;

public sealed class FermentationBarrelSystem : SharedFermentationSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPickleJarSystem _pickleJars = default!;

    private static readonly ProtoId<ReagentPrototype> Vinegar = "Vinegar";
    private static readonly ProtoId<ReagentPrototype> TableSalt = "TableSalt";
    private static readonly ProtoId<ReagentPrototype> Sugar = "Sugar";
    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly ProtoId<ReagentPrototype> Bacteria = "PickleBacteria";
    private static readonly ProtoId<ReagentPrototype> VinegarBrine = "PickleVinegarBrine";
    private static readonly ProtoId<ReagentPrototype> SaltBrine = "PickleSaltBrine";
    private static readonly ProtoId<ReagentPrototype> Nutriment = "Nutriment";
    private static readonly ProtoId<ReagentPrototype> PickleWine = "PickleWine";
    private static readonly ProtoId<ReagentPrototype> PickleCider = "PickleCider";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FermentationBarrelComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FermentationBarrelComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnMapInit(Entity<FermentationBarrelComponent> ent, ref MapInitEvent args)
    {
        RemComp<AnchorableComponent>(ent.Owner);
    }

    private void OnInteractHand(Entity<FermentationBarrelComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !IsEnabled() || ent.Comp.State != FermentationState.Ready)
            return;

        if (ent.Comp.ReadyMethod == PickleMethod.Alcohol)
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-hand-alcohol"), ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = TryTakeReadyProduce(ent, args.User);
    }

    protected override void HandleInteractUsing(Entity<FermentationBarrelComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.State == FermentationState.Fermenting)
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-busy"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.State == FermentationState.Ready)
        {
            if (TryComp<PickleJarComponent>(args.Used, out _))
            {
                TryFillJarFromBarrel(ent, args.Used, args.User);
                args.Handled = true;
                return;
            }

            if (!HasPickledProduce(ent))
            {
                ClearReady(ent);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("pickle-barrel-empty-first"), ent, args.User);
                args.Handled = true;
                return;
            }
        }

        if (HasComp<PickleJarComponent>(args.Used))
        {
            args.Handled = true;
            return;
        }

        if (MetaData(args.Used).EntityPrototype is not { } proto || !HasAnyRecipe(proto.ID))
            return;

        if (HasComp<PickledProduceComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-already-pickled"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
            return;

        if (container.ContainedEntities.Count >= ent.Comp.MaxProduce)
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-full"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!Container.Insert(args.Used, container))
            return;

        args.Handled = true;
    }

    protected override bool TryStartFermentation(Entity<FermentationBarrelComponent> ent, EntityUid user)
    {
        if (!IsEnabled())
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-disabled"), ent, user);
            return false;
        }

        if (ent.Comp.State != FermentationState.Idle)
            return false;

        if (!TryResolveBatch(ent, out _, out var duration, out var error, out var errorArgs))
        {
            _popup.PopupEntity(errorArgs is { Length: > 0 }
                ? Loc.GetString(error, errorArgs)
                : Loc.GetString(error), ent, user);
            return false;
        }

        if (!IsTemperatureOk(ent, out var tempError))
        {
            _popup.PopupEntity(Loc.GetString(tempError), ent, user);
            return false;
        }

        var speed = GetSpeedMultiplier(ent);
        ent.Comp.State = FermentationState.Fermenting;
        ent.Comp.Elapsed = TimeSpan.Zero;
        ent.Comp.InvalidTempTime = TimeSpan.Zero;
        ent.Comp.ReadyMethod = null;
        ent.Comp.ReadyDrinkReagent = null;
        ent.Comp.TargetDuration = TimeSpan.FromSeconds(duration / speed);
        ent.Comp.NextFart = _timing.CurTime + TimeSpan.FromSeconds(5);
        Dirty(ent);
        UpdateVisuals(ent);
        _popup.PopupEntity(Loc.GetString("pickle-barrel-started"), ent, user);
        return true;
    }

    protected override void EjectProduce(Entity<FermentationBarrelComponent> ent, EntityUid user)
    {
        if (ent.Comp.State != FermentationState.Idle)
            return;

        if (Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
        {
            foreach (var contained in container.ContainedEntities.ToArray())
            {
                Container.Remove(contained, container);
                _hands.PickupOrDrop(user, contained, dropNear: true, animate: false);
            }
        }

        if (!_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out var soln, out var tank))
            return;

        // Sugar always clears on empty; dry salt/sugar alone are deleted; vinegar stays for tipping.
        tank.RemoveReagent(new ReagentId(Sugar, null), tank.GetTotalPrototypeQuantity(Sugar));

        var vinegar = tank.GetTotalPrototypeQuantity(Vinegar);
        var salt = tank.GetTotalPrototypeQuantity(TableSalt);
        var other = tank.Volume - vinegar - salt - tank.GetTotalPrototypeQuantity(Bacteria);

        if (vinegar <= 0 && other <= 0)
        {
            tank.RemoveReagent(new ReagentId(TableSalt, null), salt);
            tank.RemoveReagent(new ReagentId(Bacteria, null), tank.GetTotalPrototypeQuantity(Bacteria));
        }

        _solutions.UpdateChemicals(soln.Value);
    }

    protected override void TipBarrel(Entity<FermentationBarrelComponent> ent, EntityUid user)
    {
        if (ent.Comp.State is FermentationState.Fermenting)
            return;

        var tipped = false;

        if (Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
        {
            foreach (var contained in container.ContainedEntities.ToArray())
            {
                Container.Remove(contained, container);
                _hands.PickupOrDrop(user, contained, dropNear: true, animate: false);
                tipped = true;
            }
        }

        if (_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out var soln, out var tank) &&
            tank.Volume > FixedPoint2.Zero)
        {
            _puddle.TrySpillAt(ent.Owner, tank.Clone(), out _);
            tank.RemoveAllSolution();
            _solutions.UpdateChemicals(soln.Value);
            tipped = true;
        }

        if (tipped)
            _popup.PopupEntity(Loc.GetString("pickle-barrel-tipped"), ent, user);

        if (ent.Comp.State == FermentationState.Ready)
            ClearReady(ent);
    }

    private bool HasPickledProduce(Entity<FermentationBarrelComponent> ent)
    {
        if (!Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
            return false;

        return container.ContainedEntities.Any(HasComp<PickledProduceComponent>);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!IsEnabled())
            return;

        var query = EntityQueryEnumerator<FermentationBarrelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State != FermentationState.Fermenting)
                continue;

            var ent = new Entity<FermentationBarrelComponent>(uid, comp);
            if (!IsTemperatureOk(ent, out var tempError))
            {
                comp.InvalidTempTime += TimeSpan.FromSeconds(frameTime);
                var hot = tempError == "pickle-barrel-too-hot";
                if ((hot && comp.InvalidTempTime > TimeSpan.FromSeconds(5)) ||
                    (!hot && comp.InvalidTempTime > TimeSpan.FromSeconds(25)))
                {
                    Fail(ent, hot);
                }

                continue;
            }

            var oldPct = ProgressBucket(comp);
            comp.InvalidTempTime = TimeSpan.Zero;
            comp.Elapsed += TimeSpan.FromSeconds(frameTime);

            if (GetSpeedMultiplier(ent) > 1.1f && _timing.CurTime >= comp.NextFart)
            {
                Fart(ent);
                comp.NextFart = _timing.CurTime + TimeSpan.FromSeconds(5);
            }

            if (GetBacteriaAmount(ent) >= 40)
            {
                Fail(ent, burst: true);
                continue;
            }

            if (ProgressBucket(comp) != oldPct)
                Dirty(ent);

            if (comp.Elapsed >= comp.TargetDuration)
                Complete(ent);
        }
    }

    private void Complete(Entity<FermentationBarrelComponent> ent)
    {
        if (!TryResolveBatch(ent, out var batches, out _, out _, out _) ||
            !Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container) ||
            !_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out var soln, out var tank))
        {
            Fail(ent, burst: false);
            return;
        }

        var method = batches[0].Recipe.Method;
        var extras = SplitExtras(tank);
        foreach (var reagent in extras.Contents.ToArray())
            tank.RemoveReagent(reagent.Reagent, reagent.Quantity);

        if (method == PickleMethod.Alcohol)
        {
            var drink = batches[0].Recipe.OutputDrinkReagent ?? PickleWine;
            var jars = 0;
            foreach (var (recipe, count) in batches)
            {
                jars += count / Math.Max(recipe.ProducePerJar, 1);
                tank.RemoveReagent(new ReagentId(recipe.RequiredReagent, null), recipe.MinReagent * (count / Math.Max(recipe.ProducePerJar, 1)));
            }

            foreach (var contained in container.ContainedEntities.ToArray())
                QueueDel(contained);

            var mix = new Solution();
            mix.AddReagent(drink, FixedPoint2.New(80 * Math.Max(jars, 1)));
            // Keep extras out of the bottling tank so ClearReady can key off drink volume alone.
            tank.RemoveAllSolution();
            tank.AddSolution(mix, _proto);
            ent.Comp.ReadyDrinkReagent = drink;
        }
        else
        {
            var pickled = new List<EntityUid>();
            foreach (var (recipe, count) in batches)
            {
                var perJar = Math.Max(recipe.ProducePerJar, 1);
                var usable = count - count % perJar;
                var jars = usable / perJar;
                tank.RemoveReagent(new ReagentId(recipe.RequiredReagent, null), recipe.MinReagent * jars);
                if (recipe.MinSugar > 0)
                    tank.RemoveReagent(new ReagentId(Sugar, null), recipe.MinSugar * jars);

                var applied = 0;
                foreach (var contained in container.ContainedEntities.ToArray())
                {
                    if (applied >= usable)
                        break;
                    if (MetaData(contained).EntityPrototype?.ID != recipe.Produce.Id)
                        continue;

                    ApplyPickle(contained, recipe);
                    pickled.Add(contained);
                    applied++;
                }
            }

            if (pickled.Count > 0 && extras.Volume > FixedPoint2.Zero)
            {
                var share = extras.Volume / pickled.Count;
                foreach (var piece in pickled)
                {
                    var slice = extras.SplitSolution(share);
                    InjectIntoFood(piece, slice);
                }
            }

            var brineId = method == PickleMethod.Salt ? SaltBrine : VinegarBrine;
            tank.AddReagent(brineId, FixedPoint2.New(Math.Max(pickled.Count * 6, 8)));
            ent.Comp.ReadyDrinkReagent = null;
        }

        _solutions.UpdateChemicals(soln.Value);
        ent.Comp.State = FermentationState.Ready;
        ent.Comp.ReadyMethod = method;
        ent.Comp.Elapsed = TimeSpan.Zero;
        Dirty(ent);
        UpdateVisuals(ent);
        _audio.PlayPvs(ent.Comp.CompleteSound, ent);
    }

    private void ApplyPickle(EntityUid produce, PickleRecipePrototype recipe)
    {
        var pickled = EnsureComp<PickledProduceComponent>(produce);
        pickled.Method = recipe.Method;
        pickled.Tint = recipe.BrineColor == default ? TintFor(recipe.Method) : recipe.BrineColor;
        pickled.PieceName = recipe.PieceName;
        Dirty(produce, pickled);

        var name = Identity.Name(produce, EntityManager);
        _meta.SetEntityName(produce, Loc.GetString(recipe.PieceName, ("name", name)));
        var descId = recipe.PieceName.Id.Replace("food-name-", "food-desc-", StringComparison.Ordinal);
        _meta.SetEntityDescription(produce, Loc.TryGetString(descId, out var desc)
            ? desc
            : Loc.GetString("pickle-produce-desc", ("name", Loc.GetString(recipe.PieceName, ("name", name)))));

        _pickleJars.ApplyPickledFlavor(produce, recipe.Method);

        if (_solutions.TryGetSolution(produce, "food", out var foodSoln, out var food))
        {
            food.RemoveReagent(new ReagentId(Water, null), food.GetTotalPrototypeQuantity(Water));
            _solutions.UpdateChemicals(foodSoln.Value);
        }

        var brine = recipe.LowBrine ? 2f : 6f;
        var brineId = recipe.Method == PickleMethod.Salt ? SaltBrine : VinegarBrine;
        var mix = new Solution();
        mix.AddReagent(brineId, brine);
        mix.AddReagent(Nutriment, 2);
        InjectIntoFood(produce, mix);
    }

    private void InjectIntoFood(EntityUid produce, Solution mix)
    {
        if (!_solutions.TryGetSolution(produce, "food", out var foodSoln, out var food))
            return;

        if (food.MaxVolume < FixedPoint2.New(40))
            food.MaxVolume = FixedPoint2.New(40);

        _solutions.TryAddSolution(foodSoln.Value, mix);
        _solutions.UpdateChemicals(foodSoln.Value);
    }

    private bool TryTakeReadyProduce(Entity<FermentationBarrelComponent> ent, EntityUid user)
    {
        if (!Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            ClearReady(ent);
            return false;
        }

        EntityUid? pick = null;
        foreach (var contained in container.ContainedEntities)
        {
            if (HasComp<PickledProduceComponent>(contained))
            {
                pick = contained;
                break;
            }
        }

        if (pick == null)
        {
            ClearReady(ent);
            return false;
        }

        Container.Remove(pick.Value, container);
        _hands.PickupOrDrop(user, pick.Value, dropNear: true, animate: false);

        if (container.ContainedEntities.Count == 0 || !container.ContainedEntities.Any(HasComp<PickledProduceComponent>))
            ClearReady(ent);

        return true;
    }

    private bool TryFillJarFromBarrel(Entity<FermentationBarrelComponent> ent, EntityUid jar, EntityUid user)
    {
        if (!TryComp<PickleJarComponent>(jar, out var jarComp))
            return false;

        if (!TryComp<OpenableComponent>(jar, out var openable) || !openable.Opened)
        {
            _popup.PopupEntity(Loc.GetString("pickle-jar-closed"), jar, user);
            return false;
        }

        if (!_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out var tankSoln, out var tank) ||
            !_solutions.TryGetSolution(jar, PickleJarComponent.SolutionName, out var jarSoln, out var jarSolution))
            return false;

        if (jarComp.RemainingPieces <= 0)
        {
            jarComp.PiecePrototype = null;
            jarComp.PieceTint = null;
            jarComp.IsWine = false;
            jarComp.ContentsStyle = "cucumber";
        }

        if (ent.Comp.ReadyMethod == PickleMethod.Alcohol)
        {
            if (jarComp.RemainingPieces > 0)
            {
                _popup.PopupEntity(Loc.GetString("pickle-jar-full"), jar, user);
                return false;
            }

            var drink = ent.Comp.ReadyDrinkReagent ?? PickleWine;
            var space = jarSolution.AvailableVolume;
            if (space <= FixedPoint2.Zero || tank.Volume <= FixedPoint2.Zero)
                return false;

            var take = FixedPoint2.Min(space, tank.Volume);
            var split = _solutions.SplitSolution(tankSoln.Value, take);
            _solutions.TryAddSolution(jarSoln.Value, split);
            jarComp.RemainingPieces = 0;
            jarComp.PiecePrototype = null;
            jarComp.IsWine = true;
            Dirty(jar, jarComp);
            _pickleJars.UpdateJarVisuals((jar, jarComp));
            var drinkLabel = drink == PickleCider
                ? Loc.GetString("food-name-station-cider")
                : Loc.GetString("pickle-piece-wine");
            _meta.SetEntityName(jar, Loc.GetString("pickle-alcohol-jar-name", ("name", drinkLabel)));

            if (tank.GetTotalPrototypeQuantity(drink) <= FixedPoint2.Zero)
                ClearReady(ent);

            _popup.PopupEntity(Loc.GetString("pickle-barrel-jar-filled"), ent, user);
            return true;
        }

        if (!Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
            return false;

        var room = jarComp.MaxPieces - jarComp.RemainingPieces;
        if (room <= 0)
        {
            _popup.PopupEntity(Loc.GetString("pickle-jar-full"), jar, user);
            return false;
        }

        var moved = 0;
        PickleRecipePrototype? recipe = null;
        foreach (var contained in container.ContainedEntities.ToArray())
        {
            if (moved >= room)
                break;
            if (!TryComp<PickledProduceComponent>(contained, out var pickled))
                continue;

            if (MetaData(contained).EntityPrototype?.ID is not { } protoId)
                continue;

            if (jarComp.PiecePrototype != null &&
                protoId != jarComp.PiecePrototype.Value.Id)
                continue;

            jarComp.PiecePrototype ??= protoId;
            jarComp.Method = pickled.Method;
            jarComp.PieceTint = pickled.Tint;
            jarComp.PieceName = pickled.PieceName;
            recipe ??= FindRecipe(jarComp.PiecePrototype.Value, pickled.Method);
            jarComp.ContentsStyle = SharedPickleJarSystem.ContentsStyleFor(recipe, protoId);
            jarComp.IsWine = false;

            Container.Remove(contained, container, reparent: false);
            QueueDel(contained);
            moved++;
        }

        if (moved <= 0)
            return false;

        jarComp.RemainingPieces += moved;
        Dirty(jar, jarComp);

        // Pack brine out of the barrel with the produce. When the batch is gone, wipe the tank.
        var brineId = jarComp.Method == PickleMethod.Salt ? SaltBrine : VinegarBrine;
        var pickledLeft = container.ContainedEntities.Count(HasComp<PickledProduceComponent>);
        var brineLeft = tank.GetTotalPrototypeQuantity(brineId);
        var brineAmt = pickledLeft <= 0
            ? brineLeft
            : brineLeft * moved / (moved + pickledLeft);
        brineAmt = FixedPoint2.Min(brineAmt, jarSolution.AvailableVolume);
        if (brineAmt > FixedPoint2.Zero)
        {
            var taken = tank.RemoveReagent(new ReagentId(brineId, null), brineAmt);
            if (taken > FixedPoint2.Zero)
            {
                var mix = new Solution();
                mix.AddReagent(brineId, taken);
                _solutions.TryAddSolution(jarSoln.Value, mix);
            }
        }

        if (pickledLeft <= 0)
        {
            tank.RemoveAllSolution();
            ClearReady(ent);
        }

        _solutions.UpdateChemicals(tankSoln.Value);
        _pickleJars.UpdateJarVisuals((jar, jarComp));

        var pieceLabel = Loc.GetString(jarComp.PieceName, ("name", Loc.GetString($"ent-{jarComp.PiecePrototype}")));
        _meta.SetEntityName(jar, Loc.GetString("pickle-jar-name", ("name", pieceLabel)));

        _popup.PopupEntity(Loc.GetString("pickle-barrel-jar-filled"), ent, user);
        return true;
    }

    private void ClearReady(Entity<FermentationBarrelComponent> ent)
    {
        ent.Comp.State = FermentationState.Idle;
        ent.Comp.ReadyMethod = null;
        ent.Comp.ReadyDrinkReagent = null;
        Dirty(ent);
        UpdateVisuals(ent);
    }

    private Solution SplitExtras(Solution tank)
    {
        var extras = tank.Clone();
        extras.RemoveReagent(new ReagentId(Bacteria, null), extras.GetTotalPrototypeQuantity(Bacteria));
        extras.RemoveReagent(new ReagentId(Vinegar, null), extras.GetTotalPrototypeQuantity(Vinegar));
        extras.RemoveReagent(new ReagentId(TableSalt, null), extras.GetTotalPrototypeQuantity(TableSalt));
        extras.RemoveReagent(new ReagentId(Sugar, null), extras.GetTotalPrototypeQuantity(Sugar));
        extras.RemoveReagent(new ReagentId(Water, null), extras.GetTotalPrototypeQuantity(Water));
        extras.RemoveReagent(new ReagentId(VinegarBrine, null), extras.GetTotalPrototypeQuantity(VinegarBrine));
        extras.RemoveReagent(new ReagentId(SaltBrine, null), extras.GetTotalPrototypeQuantity(SaltBrine));
        return extras;
    }

    private void Fail(Entity<FermentationBarrelComponent> ent, bool burst)
    {
        if (Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container))
        {
            foreach (var contained in container.ContainedEntities.ToArray())
                QueueDel(contained);
        }

        if (_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out var soln, out var tank))
        {
            var coords = Transform(ent).Coordinates.Offset(new Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.3f, 0.3f)));
            _puddle.TrySpillAt(coords, tank.Clone(), out _);
            tank.RemoveAllSolution();
            _solutions.UpdateChemicals(soln.Value);
        }

        if (burst)
        {
            _audio.PlayPvs(ent.Comp.BurstSound, ent);
            var shardCoords = Transform(ent).Coordinates.Offset(new Vector2(0.4f, 0f));
            Spawn(ent.Comp.ShardPrototype, shardCoords);
            if (_random.Prob(0.5f))
                Spawn(ent.Comp.ShardPrototype, shardCoords.Offset(new Vector2(-0.2f, 0.2f)));
            _popup.PopupEntity(Loc.GetString("pickle-barrel-burst"), ent);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("pickle-barrel-spoiled"), ent);
        }

        ClearReady(ent);
        ent.Comp.Elapsed = TimeSpan.Zero;
        ent.Comp.InvalidTempTime = TimeSpan.Zero;
        Dirty(ent);
    }

    private void Fart(Entity<FermentationBarrelComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.FartSound, ent);
        var mix = _atmos.GetTileMixture(ent.Owner, excite: true);
        mix?.AdjustMoles(Gas.Ammonia, ent.Comp.BacteriaFartMoles);
    }

    private float GetSpeedMultiplier(Entity<FermentationBarrelComponent> ent)
    {
        return GetBacteriaAmount(ent) >= 5
            ? ent.Comp.BacteriaSpeedMultiplier
            : 1f;
    }

    private FixedPoint2 GetBacteriaAmount(Entity<FermentationBarrelComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out _, out var tank))
            return FixedPoint2.Zero;

        return tank.GetTotalPrototypeQuantity(Bacteria);
    }

    private static int ProgressBucket(FermentationBarrelComponent comp)
    {
        if (comp.TargetDuration <= TimeSpan.Zero)
            return 0;

        return (int) Math.Clamp(comp.Elapsed / comp.TargetDuration * 10, 0, 10);
    }

    private bool IsTemperatureOk(Entity<FermentationBarrelComponent> ent, out string error)
    {
        error = "pickle-barrel-too-cold";
        var mix = _atmos.GetTileMixture(ent.Owner, excite: true);
        var temp = mix?.Temperature ?? 0f;
        if (temp < ent.Comp.MinTemperature)
        {
            error = "pickle-barrel-too-cold";
            return false;
        }

        if (temp > ent.Comp.MaxTemperature)
        {
            error = "pickle-barrel-too-hot";
            return false;
        }

        return true;
    }

    private bool TryResolveBatch(
        Entity<FermentationBarrelComponent> ent,
        out List<(PickleRecipePrototype Recipe, int Count)> batches,
        out float duration,
        out string error,
        out (string, object)[]? errorArgs)
    {
        batches = new();
        duration = 45f;
        error = "pickle-barrel-empty";
        errorArgs = null;

        if (!Container.TryGetContainer(ent, FermentationBarrelComponent.ProduceContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
            return false;

        if (!_solutions.TryGetSolution(ent.Owner, FermentationBarrelComponent.SolutionName, out _, out var tank))
        {
            error = "pickle-barrel-no-liquid";
            return false;
        }

        var method = ResolveMethod(tank, out error);
        if (method is not { } resolved)
            return false;

        var counts = new Dictionary<string, int>();
        foreach (var contained in container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype is not { } proto)
                continue;
            counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
        }

        float maxDuration = 0;
        var reagentNeeded = FixedPoint2.Zero;
        var sugarNeeded = FixedPoint2.Zero;
        string? shortfallName = null;
        var shortfallHave = 0;
        var shortfallNeeded = 0;
        string? badProduceName = null;

        foreach (var (protoId, count) in counts)
        {
            var recipe = FindRecipe(protoId, resolved);
            if (recipe == null)
            {
                badProduceName ??= Identity.Name(
                    container.ContainedEntities.First(uid => MetaData(uid).EntityPrototype?.ID == protoId),
                    EntityManager);
                error = "pickle-barrel-bad-recipe";
                errorArgs = [("name", badProduceName)];
                return false;
            }

            if (count < recipe.ProducePerJar)
            {
                if (shortfallNeeded == 0 || recipe.ProducePerJar - count < shortfallNeeded - shortfallHave)
                {
                    shortfallName = Identity.Name(
                        container.ContainedEntities.First(uid => MetaData(uid).EntityPrototype?.ID == protoId),
                        EntityManager);
                    shortfallHave = count;
                    shortfallNeeded = recipe.ProducePerJar;
                }

                continue;
            }

            var jars = count / recipe.ProducePerJar;
            reagentNeeded += recipe.MinReagent * jars;
            if (recipe.MinSugar > 0)
                sugarNeeded += recipe.MinSugar * jars;
            batches.Add((recipe, count));
            maxDuration = Math.Max(maxDuration, recipe.DurationSeconds);
        }

        if (batches.Count == 0)
        {
            if (shortfallNeeded > 0 && shortfallName != null)
            {
                error = "pickle-barrel-not-enough";
                errorArgs =
                [
                    ("name", shortfallName),
                    ("have", shortfallHave),
                    ("needed", shortfallNeeded),
                ];
            }
            else
            {
                error = "pickle-barrel-not-enough-generic";
            }

            return false;
        }

        var haveReagent = tank.GetTotalPrototypeQuantity(batches[0].Recipe.RequiredReagent);
        if (haveReagent < reagentNeeded)
        {
            var need = batches[0].Recipe.RequiredReagent;
            error = need == Vinegar
                ? "pickle-barrel-need-vinegar"
                : need == TableSalt
                    ? "pickle-barrel-need-salt"
                    : need == Sugar
                        ? "pickle-barrel-need-sugar-ferment"
                        : "pickle-barrel-no-brine";
            errorArgs =
            [
                ("needed", (int) Math.Ceiling((float) reagentNeeded)),
                ("have", (int) Math.Floor((float) haveReagent)),
            ];
            return false;
        }

        var haveSugar = tank.GetTotalPrototypeQuantity(Sugar);
        if (sugarNeeded > FixedPoint2.Zero && haveSugar < sugarNeeded)
        {
            error = "pickle-barrel-no-sugar";
            errorArgs =
            [
                ("needed", (int) Math.Ceiling((float) sugarNeeded)),
                ("have", (int) Math.Floor((float) haveSugar)),
            ];
            return false;
        }

        duration = maxDuration;
        return true;
    }

    private PickleMethod? ResolveMethod(Solution tank, out string error)
    {
        error = "pickle-barrel-no-liquid";
        var vinegar = tank.GetTotalPrototypeQuantity(Vinegar);
        var salt = tank.GetTotalPrototypeQuantity(TableSalt);
        var sugar = tank.GetTotalPrototypeQuantity(Sugar);
        var water = tank.GetTotalPrototypeQuantity(Water);

        if (vinegar <= 0 && salt <= 0 && sugar <= 0)
        {
            if (water > 0)
                error = "pickle-barrel-water-only";
            return null;
        }

        if (sugar > vinegar && sugar > salt)
            return PickleMethod.Alcohol;

        if (vinegar >= salt)
            return PickleMethod.Vinegar;

        return PickleMethod.Salt;
    }
}

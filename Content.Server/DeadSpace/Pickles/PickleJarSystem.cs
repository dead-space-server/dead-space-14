// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Pickles;
using Content.Shared.DeadSpace.Pickles.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Pickles;

public sealed class PickleJarSystem : SharedPickleJarSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly FlavorProfileSystem _flavor = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ReactiveSystem _reaction = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private static readonly SoundSpecifier EatSound = new SoundCollectionSpecifier("eating");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickleJarComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<PickleJarComponent, IngestedEvent>(OnWineIngested);
    }

    private void OnSolutionChanged(Entity<PickleJarComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != PickleJarComponent.SolutionName)
            return;

        // Drinking wine dry must clear IsWine so later sips of brine cannot blind.
        if (ent.Comp.RemainingPieces <= 0 &&
            ent.Comp.IsWine &&
            (!_solutions.TryGetSolution(ent.Owner, PickleJarComponent.SolutionName, out _, out var drink) ||
             drink.Volume <= FixedPoint2.Zero))
        {
            ClearJarContentsMeta(ent);
            return;
        }

        UpdateJarVisuals(ent);
    }

    private void OnWineIngested(Entity<PickleJarComponent> ent, ref IngestedEvent args)
    {
        if (!ent.Comp.IsWine || !_random.Prob(ent.Comp.WineBlindChance))
            return;

        var drinker = args.Target;
        if (!TryComp<BlindableComponent>(drinker, out var blindable) || blindable.IsBlind)
            return;

        _blindable.AdjustEyeDamage((drinker, blindable), blindable.MaxDamage);
        _popup.PopupEntity(Loc.GetString("pickle-wine-eye-damage"), drinker, drinker);
    }

    protected override void OnJarSlip(Entity<PickleJarComponent> jar, EntityUid user, EntityUid piece)
    {
        // Piece is freshly spawned and not in a hand yet — place it on the floor, do not pick it up.
        _transform.PlaceNextTo(piece, user);

        if (!_solutions.TryGetSolution(jar.Owner, PickleJarComponent.SolutionName, out var jarSoln, out var drink) ||
            drink.Volume <= FixedPoint2.Zero)
            return;

        var spillAmt = FixedPoint2.Min(drink.Volume, FixedPoint2.New(8));
        var spilled = _solutions.SplitSolution(jarSoln.Value, spillAmt);
        _puddle.TrySpillAt(user, spilled, out _);
    }

    protected override bool TryEatOnePiece(Entity<PickleJarComponent> ent, EntityUid user)
    {
        // Same gates as normal food: mouth free, has a stomach, and can digest this produce.
        if (!_ingestion.HasMouthAvailable(user, user))
            return false;

        if (!HasComp<BodyComponent>(user) ||
            !_body.TryGetBodyOrganEntityComps<StomachComponent>(user, out var stomachs))
        {
            _popup.PopupEntity(Loc.GetString("ingestion-cant-digest", ("entity", ent.Owner)), user, user);
            return false;
        }

        if (!TrySpawnPiece(ent, user, out var piece))
            return false;

        if (!_ingestion.IsDigestibleBy(piece, stomachs, out var showPopup))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("ingestion-cant-digest", ("entity", piece)), user, user);

            ReturnPieceToJar(ent, piece);
            QueueDel(piece);
            return true;
        }

        if (!_solutions.TryGetSolution(piece, "food", out var foodSoln, out var food) ||
            food.Volume <= FixedPoint2.Zero)
        {
            QueueDel(piece);
            return true;
        }

        var bite = _solutions.SplitSolution(foodSoln.Value, food.Volume);

        Entity<StomachComponent, OrganComponent>? best = null;
        var bestVol = FixedPoint2.Zero;
        foreach (var organ in stomachs)
        {
            if (!_solutions.ResolveSolution(organ.Owner, StomachSystem.DefaultSolutionName, ref organ.Comp1.Solution, out var stomachSol))
                continue;

            if (stomachSol.AvailableVolume <= bestVol)
                continue;

            best = organ;
            bestVol = stomachSol.AvailableVolume;
        }

        if (best != null)
            _stomach.TryTransferSolution(best.Value.Owner, bite, best.Value.Comp1);

        _reaction.DoEntityReaction(user, bite, ReactionMethod.Ingestion);

        var flavors = _flavor.GetLocalizedFlavorsMessage(piece, user, bite);
        _popup.PopupEntity(Loc.GetString("edible-nom", ("food", piece), ("flavors", flavors)), user, user);
        _audio.PlayPvs(EatSound, user);

        QueueDel(piece);
        return true;
    }

    private void ReturnPieceToJar(Entity<PickleJarComponent> ent, EntityUid piece)
    {
        if (!TryComp<PickledProduceComponent>(piece, out var pickled))
            return;

        if (MetaData(piece).EntityPrototype?.ID is not { } protoId)
            return;

        ent.Comp.RemainingPieces++;
        ent.Comp.PiecePrototype = protoId;
        ent.Comp.Method = pickled.Method;
        ent.Comp.PieceTint = pickled.Tint;
        ent.Comp.PieceName = pickled.PieceName;
        ent.Comp.IsWine = false;
        Dirty(ent);
        UpdateJarVisuals(ent);

        var pieceLabel = Loc.GetString(ent.Comp.PieceName, ("name", Loc.GetString($"ent-{protoId}")));
        _meta.SetEntityName(ent.Owner, Loc.GetString("pickle-jar-name", ("name", pieceLabel)));
    }

    protected override void AfterSpawnPiece(Entity<PickleJarComponent> jar, EntityUid piece)
    {
        if (!_solutions.TryGetSolution(jar.Owner, PickleJarComponent.SolutionName, out var jarSoln, out var jarSolution))
            return;

        if (jarSolution.Volume <= FixedPoint2.Zero)
            return;

        var share = FixedPoint2.Min(
            jarSolution.Volume / Math.Max(jar.Comp.RemainingPieces + 1, 1),
            FixedPoint2.New(4));
        if (share <= FixedPoint2.Zero)
            return;

        var split = _solutions.SplitSolution(jarSoln.Value, share);

        if (!_solutions.TryGetSolution(piece, "food", out var foodSoln, out var food))
        {
            _solutions.TryAddSolution(jarSoln.Value, split);
            return;
        }

        if (food.MaxVolume < FixedPoint2.New(40))
            food.MaxVolume = FixedPoint2.New(40);

        _solutions.TryAddSolution(foodSoln.Value, split);
    }
}

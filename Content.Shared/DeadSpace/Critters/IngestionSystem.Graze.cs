using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.Critters;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Random;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class IngestionSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private void TryCritterGraze(Entity<BodyComponent> entity, ref AttemptIngestEvent args)
    {
        if (!HasComp<CritterGrazeComponent>(entity))
            return;

        var food = args.Ingested;
        var digest = new IsDigestibleEvent();
        RaiseLocalEvent(food, ref digest);
        if (!digest.Digestible || (digest.SpecialDigestion && !digest.Universal))
            return;

        if (!args.Ingest)
        {
            args.Handled = true;
            return;
        }

        if (!CanConsume(args.User, entity, food, out _, out var time))
            return;

        if (!_doAfter.TryStartDoAfter(GetEdibleDoAfterArgs(args.User, entity, food, time ?? TimeSpan.Zero, args.Utensil)))
            return;

        args.Handled = true;
    }

    private void FinishCritterGraze(Entity<BodyComponent> entity, ref EatingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        if (!TryComp<CritterGrazeComponent>(entity, out var graze))
            return;

        var food = args.Target.Value;
        if (!CanConsume(args.User, entity, food, out var solution, out _))
            return;

        var sol = solution.Value.Comp.Solution;
        var before = new BeforeIngestedEvent(FixedPoint2.Zero, sol.Volume, sol);
        RaiseLocalEvent(food, ref before);
        if (before.Cancelled || before.Min > before.Max)
            return;

        var transfer = FixedPoint2.Clamp(before.Transfer, before.Min, before.Max);
        if (transfer <= 0)
            return;

        var split = _solutionContainer.SplitSolution(solution.Value, transfer);
        if (before.Refresh)
            _solutionContainer.TryAddSolution(solution.Value, split);

        if (TryComp<EdibleComponent>(food, out var edible))
        {
            var proto = _proto.Index(edible.Edible);
            _audio.PlayPredicted(edible.UseSound ?? proto.UseSound, entity.Owner, args.User);
            var flavors = _flavorProfile.GetLocalizedFlavorsMessage(food, entity.Owner, split);
            _popup.PopupPredicted(
                Loc.GetString(proto.Message, ("food", food), ("flavors", flavors)),
                Loc.GetString(proto.OtherMessage),
                args.User,
                args.User);

            if (edible.DestroyOnEmpty && IsEmpty((food, edible)))
            {
                SpawnTrash((food, edible), args.User);
                var attempt = new DestructionAttemptEvent();
                RaiseLocalEvent(food, attempt);
                if (!attempt.Cancelled)
                {
                    var finished = new FullyEatenEvent(args.User);
                    RaiseLocalEvent(food, ref finished);
                    var destroyed = new DestructionEventArgs();
                    RaiseLocalEvent(food, destroyed);
                    PredictedDel(food);
                }
            }
        }

        graze.NextGraze = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextDouble(graze.MinCooldown.TotalSeconds, graze.MaxCooldown.TotalSeconds));
        args.Handled = true;
        args.Repeat = false;
    }
}

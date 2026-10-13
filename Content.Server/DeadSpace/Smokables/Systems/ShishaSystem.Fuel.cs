using Content.Shared.DeadSpace.Smokables;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Temperature;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Smokables.Systems;

public sealed partial class ShishaSystem
{
    private static readonly ProtoId<TagPrototype> FillerTag = "Smokable";

    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private void InitializeFuel()
    {
        SubscribeLocalEvent<ShishaComponent, AfterInteractEvent>(OnBaseAfterInteract);
        SubscribeLocalEvent<ShishaComponent, ExaminedEvent>(OnExamined);
    }

    private void LoadOrLight(Entity<ShishaComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<StackComponent>(args.Used, out var stack) && stack.StackTypeId == ent.Comp.Fuel)
        {
            args.Handled = true;
            if (ent.Comp.FuelRemaining > 0)
            {
                _popup.PopupEntity(Loc.GetString("shisha-coal-loaded"), ent, args.User);
                return;
            }

            if (ent.Comp.FuelPerItem <= 0 || !_stacks.TryUse((args.Used, stack), 1))
                return;

            ent.Comp.FuelRemaining = ent.Comp.FuelPerItem;
            UpdateAppearance(ent, IsDocked(ent));
            _popup.PopupEntity(Loc.GetString("shisha-add-coal"), ent, args.User);
            return;
        }

        // Reuse the same prepared-filler tag as smoking pipes and rolling papers.
        if (_tags.HasTag(args.Used, FillerTag))
        {
            args.Handled = true;
            if (Terminating(args.Used) || stack is { Count: <= 0 }
                || !_solutions.TryGetSolution(args.Used, "food", out _, out var filler)
                || filler.Volume <= 0)
            {
                _popup.PopupEntity(Loc.GetString("shisha-invalid-filler"), ent, args.User);
                return;
            }

            if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var reservoir, out _)
                || !_solutions.TryAddSolution(reservoir.Value, filler))
            {
                _popup.PopupEntity(Loc.GetString("shisha-filler-full"), ent, args.User);
                return;
            }

            // Stack solutions describe one item (as in the reagent grinder). Copy that dose,
            // then consume one item, preserving the chemistry and count of the remainder.
            if (stack != null)
                _stacks.ReduceCount((args.Used, stack), 1);
            else
                QueueDel(args.Used);

            _popup.PopupEntity(Loc.GetString("shisha-add-filler"), ent, args.User);
            return;
        }

        args.Handled = TryLight(ent, args.Used, args.User);
    }

    private void OnBaseAfterInteract(Entity<ShishaComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryLight(ent, target, args.User);
    }

    private bool TryLight(Entity<ShishaComponent> ent, EntityUid source, EntityUid user)
    {
        if (ent.Comp.Lit)
            return false;

        // The standard lighter/match/welder check used by cigarettes and smoking pipes.
        var hot = new IsHotEvent();
        RaiseLocalEvent(source, hot);
        if (!hot.IsHot)
            return false;

        if (ent.Comp.FuelRemaining <= 0)
            _popup.PopupEntity(Loc.GetString("shisha-needs-coal"), ent, user);
        else if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var filler)
                 || filler.Volume <= 0)
            _popup.PopupEntity(Loc.GetString("shisha-needs-filler"), ent, user);
        else
        {
            ent.Comp.Lit = true;
            UpdateAppearance(ent, IsDocked(ent));
            _audio.PlayPvs(ent.Comp.LightSound, ent);
        }

        return true;
    }

    private bool CanSmoke(Entity<ShishaComponent> ent, EntityUid user)
    {
        if (ent.Comp.Lit && ent.Comp.FuelRemaining > 0)
            return true;

        _popup.PopupEntity(Loc.GetString(ent.Comp.FuelRemaining > 0 ? "shisha-needs-light" : "shisha-needs-coal"), ent, user);
        return false;
    }

    private void UpdateFuel(Entity<ShishaComponent> ent, float elapsed)
    {
        if (!ent.Comp.Lit)
            return;

        // Reuse the existing quarter-second pass; idle filler never loses reagents.
        ent.Comp.FuelRemaining = MathF.Max(0, ent.Comp.FuelRemaining - elapsed);
        if (ent.Comp.FuelRemaining <= 0)
            BurnOut(ent);
    }

    private void BurnOut(Entity<ShishaComponent> ent)
    {
        if (!ent.Comp.Lit)
            return;

        ent.Comp.Lit = false;
        if (TryComp<ShishaHoseComponent>(ent.Comp.Hose, out var hose))
            CancelPuff(hose);
        UpdateAppearance(ent, IsDocked(ent));
    }

    private void OnExamined(Entity<ShishaComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Lit ? "shisha-lit" : "shisha-unlit"));
        args.PushMarkup(Loc.GetString("shisha-fuel-remaining", ("seconds", (int) MathF.Ceiling(ent.Comp.FuelRemaining))));
    }

    private void UpdateAppearance(Entity<ShishaComponent> ent, bool docked)
    {
        var prefix = ent.Comp.Lit ? "icon-lit" : ent.Comp.FuelRemaining > 0 ? "icon-coal" : "icon";
        _appearance.SetData(ent, ShishaVisuals.HoseDocked, docked);
        _appearance.SetData(ent, ShishaVisuals.State, docked ? prefix : prefix + "-no-hose");
    }
}

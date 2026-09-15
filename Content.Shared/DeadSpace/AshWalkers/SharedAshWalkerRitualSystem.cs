using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Chasm;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tiles;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Configuration;
using DeadSpaceCVars = Content.Shared.DeadSpace.CCCCVars.CCCCVars;

namespace Content.Shared.DeadSpace.AshWalkers;

public abstract class SharedAshWalkerRitualSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshWalkerRitualDaggerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<AshWalkerRitualDaggerComponent, AshWalkerDrawRuneActionEvent>(OnDrawAction);
        SubscribeLocalEvent<AshWalkerRitualDaggerComponent, AshWalkerDrawRuneDoAfterEvent>(OnDrawFinished);
        SubscribeLocalEvent<AshWalkerRuneComponent, CanDropTargetEvent>(OnCanDrop);
        SubscribeLocalEvent<AshWalkerRuneComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AshWalkerRuneComponent, GetVerbsEvent<AlternativeVerb>>(OnEraseVerb);
    }

    private void OnGetActions(Entity<AshWalkerRitualDaggerComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands || !HasComp<AshWalkerComponent>(args.User))
            return;

        args.AddAction(ref ent.Comp.HuntAction, "ActionAshWalkerDrawHuntRune");
        args.AddAction(ref ent.Comp.MendingAction, "ActionAshWalkerDrawMendingRune");
        args.AddAction(ref ent.Comp.ReturnAction, "ActionAshWalkerDrawReturnRune");
        Dirty(ent);
    }

    private void OnDrawAction(Entity<AshWalkerRitualDaggerComponent> ent, ref AshWalkerDrawRuneActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanDraw(ent, args.Performer, args.Target, out var center))
        {
            _popup.PopupClient(Loc.GetString("ash-walker-rune-cannot-draw"), ent, args.Performer);
            return;
        }

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, ent.Comp.DrawTime,
            new AshWalkerDrawRuneDoAfterEvent { Coordinates = GetNetCoordinates(center), Rite = args.Rite }, ent, used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnDrawFinished(Entity<AshWalkerRitualDaggerComponent> ent, ref AshWalkerDrawRuneDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled ||
            !CanDraw(ent, args.User, GetCoordinates(args.Coordinates), out var center))
            return;

        args.Handled = true;
        var prototype = args.Rite switch
        {
            AshWalkerRite.Hunt => "AshWalkerHuntRune",
            AshWalkerRite.Mending => "AshWalkerMendingRune",
            AshWalkerRite.Return => "AshWalkerReturnRune",
            _ => null,
        };
        if (prototype == null)
            return;

        var rune = PredictedSpawnAtPosition(prototype, center);
        var comp = Comp<AshWalkerRuneComponent>(rune);
        comp.HomeMap = Transform(args.User).MapUid;
        Dirty(rune, comp);
    }

    public bool CanDraw(EntityUid dagger, EntityUid user, EntityCoordinates coordinates, out EntityCoordinates center)
    {
        center = default;
        if (!_configuration.GetCVar(DeadSpaceCVars.AshWalkersEnabled) || !HasComp<AshWalkerComponent>(user) ||
            !TryComp<AshWalkerTribeMemberComponent>(user, out var tribe) ||
            tribe.HomeMap == null || Transform(user).MapUid != tribe.HomeMap ||
            !_hands.IsHolding(user, dagger) || !_blocker.CanInteract(user, dagger) ||
            _turf.GetTileRef(coordinates) is not { } tile || !TryComp<MapGridComponent>(tile.GridUid, out var grid) ||
            Transform(tile.GridUid).MapUid != tribe.HomeMap || tile.Tile.IsEmpty || _turf.IsSpace(tile) ||
            _turf.IsTileBlocked(tile, Content.Shared.Physics.CollisionGroup.MobMask))
            return false;

        center = _map.ToCenterCoordinates(tile, grid);
        if (!_interaction.InRangeUnobstructed(user, center))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<LavaComponent>(uid) || HasComp<ChasmComponent>(uid) || HasComp<AshWalkerRuneComponent>(uid))
                return false;
        }

        return true;
    }

    protected bool CanUseRune(Entity<AshWalkerRuneComponent> rune, EntityUid user)
    {
        return _configuration.GetCVar(DeadSpaceCVars.AshWalkersEnabled) && !TerminatingOrDeleted(rune) && Transform(rune).Anchored &&
               HasComp<AshWalkerComponent>(user) && TryComp<AshWalkerTribeMemberComponent>(user, out var tribe) &&
               tribe.HomeMap != null && tribe.HomeMap == rune.Comp.HomeMap &&
               Transform(rune).MapUid == tribe.HomeMap && _blocker.CanInteract(user, rune) &&
               _interaction.InRangeUnobstructed(user, rune.Owner);
    }

    private void OnCanDrop(Entity<AshWalkerRuneComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.CanDrop = CanUseRune(ent, args.User) && (_mobState.IsDead(args.Dragged) || IsValidOffering(args.Dragged));
    }

    protected bool IsValidOffering(EntityUid uid)
    {
        if (!TryComp<AshWalkerOfferingComponent>(uid, out var offering))
            return false;
        if (offering.RequiredReagents.Count == 0)
            return true;
        return _solutions.TryGetSolution(uid, offering.Solution, out _, out var solution) &&
               offering.RequiredReagents.All(reagent => solution.GetTotalPrototypeQuantity(reagent.Key) >= reagent.Value);
    }

    private void OnExamined(Entity<AshWalkerRuneComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString($"ash-walker-rune-{ent.Comp.Rite.ToString().ToLowerInvariant()}-requirements"));
    }

    private void OnEraseVerb(Entity<AshWalkerRuneComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanUseRune(ent, args.User) ||
            !_hands.EnumerateHeld(args.User).Any(HasComp<AshWalkerRitualDaggerComponent>))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("ash-walker-rune-erase"),
            Act = () =>
            {
                if (CanUseRune(ent, user) && _hands.EnumerateHeld(user).Any(HasComp<AshWalkerRitualDaggerComponent>))
                    PredictedQueueDel(ent.Owner);
            },
        });
    }

}

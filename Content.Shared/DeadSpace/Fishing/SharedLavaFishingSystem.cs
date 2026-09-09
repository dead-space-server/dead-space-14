using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tiles;
using Content.Shared.Verbs;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Fishing;

public abstract class SharedLavaFishingSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavaFishingRodComponent, AfterInteractEvent>(OnCast);
        SubscribeLocalEvent<LavaFishingRodComponent, InteractUsingEvent>(OnBait);
        SubscribeLocalEvent<LavaFishingRodComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<LavaFishingRodComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LavaFishingRodComponent, GetVerbsEvent<AlternativeVerb>>(OnRetrieveVerb);
        SubscribeLocalEvent<LavaFishingRodComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LavaFishingRodComponent, GotUnequippedHandEvent>(OnUnequipped);
        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.UseItemInHand, new PointerInputCmdHandler(OnReelInput, ignoreUp: false), typeof(SharedHandsSystem))
            .Register<SharedLavaFishingSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<SharedLavaFishingSystem>();
        base.Shutdown();
    }

    private void OnCast(Entity<LavaFishingRodComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !_hands.IsHolding(args.User, ent.Owner) || !IsLava(args.ClickLocation))
            return;

        args.Handled = true;
        if (ent.Comp.Phase != LavaFishingPhase.Idle)
            return;
        if (ent.Comp.Bait <= 0)
        {
            _popup.PopupClient(Loc.GetString("lava-fishing-no-bait"), ent, args.User);
            return;
        }
        if (!_interaction.InRangeUnobstructed(args.User, args.ClickLocation, range: ent.Comp.CastRange) ||
            !_blocker.CanInteract(args.User, ent.Owner) || !_mobState.IsAlive(args.User))
            return;

        BeginFishing(ent, args.User, args.ClickLocation);
    }

    private void OnBait(Entity<LavaFishingRodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<EdibleComponent>(args.Used, out var food) || food.Edible != IngestionSystem.Food ||
            food.RequiresSpecialDigestion || HasComp<MobStateComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Phase != LavaFishingPhase.Idle || ent.Comp.Bait >= ent.Comp.BaitCapacity)
        {
            _popup.PopupClient(Loc.GetString("lava-fishing-cannot-bait"), ent, args.User);
            return;
        }

        if (!_ingestion.CanAccessSolution(args.Used, args.User, out var solution, out _, requireUtensils: false) ||
            solution.Value.Comp.Solution.Volume <= FixedPoint2.Zero)
            return;

        _solutions.SplitSolution(solution.Value, FixedPoint2.Min(ent.Comp.BaitPortion, solution.Value.Comp.Solution.Volume));
        ent.Comp.Bait++;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("lava-fishing-bait-added", ("count", ent.Comp.Bait), ("max", ent.Comp.BaitCapacity)), ent, args.User);
        if (solution.Value.Comp.Solution.Volume > FixedPoint2.Zero || !food.DestroyOnEmpty)
            return;

        var attempt = new DestructionAttemptEvent();
        RaiseLocalEvent(args.Used, attempt);
        if (attempt.Cancelled)
            return;
        _ingestion.SpawnTrash((args.Used, food), args.User);
        var destruction = new DestructionEventArgs();
        RaiseLocalEvent(args.Used, destruction);
        PredictedDel(args.Used);
    }

    private void OnUseInHand(Entity<LavaFishingRodComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.Fisher == args.User)
            args.Handled = true;
    }

    private void OnActivate(Entity<LavaFishingRodComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !_hands.IsHolding(args.User, ent.Owner))
            return;
        args.Handled = true;
        if (ent.Comp.Phase == LavaFishingPhase.Caught)
            RetrieveCatch(ent, args.User);
        else if (ent.Comp.Fisher == args.User)
            FinishFishing(ent, false);
    }

    private void OnRetrieveVerb(Entity<LavaFishingRodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Phase == LavaFishingPhase.Idle ||
            !_hands.IsHolding(args.User, ent.Owner))
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Phase == LavaFishingPhase.Caught ? "lava-fishing-collect" : "lava-fishing-cancel"),
            Act = () =>
            {
                if (ent.Comp.Phase == LavaFishingPhase.Caught)
                    RetrieveCatch(ent, user);
                else if (ent.Comp.Fisher == user)
                    FinishFishing(ent, false);
            },
        });
    }

    private bool OnReelInput(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.Session?.AttachedEntity is not { } user)
            return false;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<LavaFishingRodComponent>(held, out var rod) || rod.Fisher != user || !CanContinue((held, rod)))
                continue;
            var pulling = args.State == BoundKeyState.Down;
            if (rod.Phase == LavaFishingPhase.Bite && pulling)
            {
                rod.Phase = LavaFishingPhase.Reeling;
                rod.Elapsed = 0;
                rod.ZonePosition = 0.5f;
                rod.FishPosition = 0.5f;
                rod.Progress = 0.25f;
            }
            if (rod.Phase == LavaFishingPhase.Reeling)
            {
                rod.Reeling = pulling;
                Dirty(held, rod);
            }
            // The command must reach the server; UseInHandEvent suppresses the default item action.
            return false;
        }
        return false;
    }

    private void OnUnequipped(Entity<LavaFishingRodComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Fisher == args.User && ent.Comp.Phase != LavaFishingPhase.Caught)
            FinishFishing(ent, false);
    }

    private void OnExamined(Entity<LavaFishingRodComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        args.PushMarkup(Loc.GetString("lava-fishing-bait-count", ("count", ent.Comp.Bait), ("max", ent.Comp.BaitCapacity)));
        if (ent.Comp.Phase == LavaFishingPhase.Caught)
            args.PushMarkup(Loc.GetString("lava-fishing-catch-held"));
    }

    public bool IsLava(EntityCoordinates coordinates)
    {
        if (_turf.GetTileRef(coordinates) is not { } tile || !TryComp<MapGridComponent>(tile.GridUid, out var grid))
            return false;
        var anchored = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<LavaComponent>(uid))
                return true;
        }
        return false;
    }

    protected bool CanContinue(Entity<LavaFishingRodComponent> ent)
    {
        if (ent.Comp.Fisher is not { } user || TerminatingOrDeleted(user) || _hands.GetActiveItem(user) != ent.Owner ||
            !_blocker.CanInteract(user, ent.Owner) || !_mobState.IsAlive(user) ||
            !ent.Comp.Origin.IsValid(EntityManager) || !ent.Comp.Spot.IsValid(EntityManager))
            return false;

        var original = _transform.ToMapCoordinates(ent.Comp.Origin);
        var position = _transform.GetMapCoordinates(user);
        return position.MapId == original.MapId && Vector2.DistanceSquared(position.Position, original.Position) <= 0.36f &&
               IsLava(ent.Comp.Spot) && _interaction.InRangeUnobstructed(user, ent.Comp.Spot, range: ent.Comp.CastRange);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<LavaFishingRodComponent>();
        while (query.MoveNext(out var uid, out var rod))
        {
            if (rod.Phase is LavaFishingPhase.Idle or LavaFishingPhase.Caught)
                continue;
            if (!CanContinue((uid, rod)))
            {
                FinishFishing((uid, rod), false);
                continue;
            }

            if (rod.Phase == LavaFishingPhase.Waiting && _timing.CurTime >= rod.NextPhase)
            {
                rod.Phase = LavaFishingPhase.Bite;
                rod.NextPhase = _timing.CurTime + TimeSpan.FromSeconds(3);
                rod.Reeling = false;
                Dirty(uid, rod);
            }
            else if (rod.Phase == LavaFishingPhase.Bite && _timing.CurTime >= rod.NextPhase)
                FinishFishing((uid, rod), false);
            else if (rod.Phase == LavaFishingPhase.Reeling)
            {
                var state = AdvanceReeling(rod, frameTime, rod.Reeling);
                rod.ZonePosition = state.ZonePosition;
                rod.FishPosition = state.FishPosition;
                rod.Progress = state.Progress;
                rod.Elapsed = state.Elapsed;
                Dirty(uid, rod);
                if (rod.Progress <= 0 || rod.Elapsed >= 45)
                    FinishFishing((uid, rod), false);
                else if (rod.Progress >= 1)
                    FinishFishing((uid, rod), true);
            }
        }
    }

    public static float GetZoneWidth(LavaFishingRodComponent rod)
    {
        return Math.Clamp(0.28f - (rod.Difficulty - 0.8f) * 0.06f, 0.20f, 0.32f);
    }

    public static LavaFishingReelState AdvanceReeling(LavaFishingRodComponent rod, float elapsed, bool pulling)
    {
        var totalElapsed = rod.Elapsed + elapsed;
        var velocity = pulling ? 0.65f : -0.65f;
        var halfWidth = GetZoneWidth(rod) / 2;
        var zone = Math.Clamp(rod.ZonePosition + velocity * elapsed, halfWidth, 1 - halfWidth);

        var time = totalElapsed * rod.Difficulty * (rod.IsFish ? 1f : 0.55f);
        var target = Math.Clamp(0.5f + 0.34f * MathF.Sin(time * 0.7f + rod.Rhythm) +
                               0.10f * MathF.Sin(time * 2.3f + rod.Rhythm * 2), 0.05f, 0.95f);
        var speed = (rod.IsFish ? 0.35f : 0.20f) * rod.Difficulty * elapsed;
        var fish = rod.FishPosition + Math.Clamp(target - rod.FishPosition, -speed, speed);
        var progress = rod.Progress;
        if (MathF.Abs(fish - zone) <= halfWidth)
            progress = Math.Min(1, progress + 0.065f * elapsed);
        else if (totalElapsed > 0.75f)
            progress = Math.Max(0, progress - 0.08f * elapsed);
        return new LavaFishingReelState(zone, fish, progress, totalElapsed);
    }

    protected virtual void BeginFishing(Entity<LavaFishingRodComponent> ent, EntityUid fisher, EntityCoordinates spot) { }
    protected virtual void FinishFishing(Entity<LavaFishingRodComponent> ent, bool success) { }
    protected virtual void RetrieveCatch(Entity<LavaFishingRodComponent> ent, EntityUid user) { }
}

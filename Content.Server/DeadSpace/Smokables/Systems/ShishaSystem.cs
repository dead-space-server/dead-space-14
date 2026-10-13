using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Events;

namespace Content.Server.DeadSpace.Smokables.Systems;

/// <summary>
/// Owns hose docking and consumption. The hose is an existing item, not a newly spawned
/// copy on each retrieval. All completion checks use the current base reservoir.
/// </summary>
public sealed partial class ShishaSystem : SharedShishaSystem
{
    private const float ConnectionCheckInterval = 0.25f;
    private float _connectionCheckAccumulator;

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeFuel();
        SubscribeLocalEvent<BeforeSerializationEvent>(OnBeforeSave);
        SubscribeLocalEvent<ShishaComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShishaComponent, ComponentShutdown>(OnBaseShutdown);
        SubscribeLocalEvent<ShishaComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ShishaComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<ShishaComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ShishaHoseComponent, ComponentShutdown>(OnHoseShutdown);
        SubscribeLocalEvent<ShishaHoseComponent, EntGotRemovedFromContainerMessage>(OnHoseRemoved);
        SubscribeLocalEvent<ShishaHoseComponent, ThrowItemAttemptEvent>(OnThrow);
        SubscribeLocalEvent<ShishaHoseComponent, AfterInteractEvent>(OnSmoke);
        SubscribeLocalEvent<ShishaHoseComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ShishaHoseComponent, ShishaPuffDoAfterEvent>(OnPuff);
    }

    private void OnBeforeSave(BeforeSerializationEvent args)
    {
        var query = AllEntityQuery<ShishaComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shisha, out _))
        {
            if (shisha.Hose == null || IsDocked((uid, shisha)))
                continue;

            // A holder may be excluded from map saves. Dock before traversal so the owned hose
            // is serialized with its base, rather than leaving a reference to an omitted child.
            var parent = uid;
            while (!args.Entities.Contains(parent))
            {
                if (!TryComp(parent, out TransformComponent? transform))
                    break;
                parent = transform.ParentUid;
            }

            if (args.Entities.Contains(parent))
                ReturnHose((uid, shisha));
        }
    }

    private void OnMapInit(Entity<ShishaComponent> ent, ref MapInitEvent args)
    {
        var slot = _containers.EnsureContainer<ContainerSlot>(ent, ShishaComponent.HoseContainer);
        // Adopt a saved docked hose, or retain the saved deployed hose. Only fresh bases spawn one.
        var hose = ent.Comp.Hose ?? slot.ContainedEntity;
        if (hose == null && !ent.Comp.HoseInitialized)
            hose = Spawn(ent.Comp.HosePrototype, Transform(ent).Coordinates);

        ent.Comp.HoseInitialized = true;
        ent.Comp.Lit &= ent.Comp.FuelRemaining > 0;
        UpdateAppearance(ent, false);

        if (!TryComp<ShishaHoseComponent>(hose, out var component))
            return;

        ent.Comp.Hose = hose;
        component.Base = ent;
        Dirty(ent);
        Dirty(hose.Value, component);
        ReturnHose(ent);
    }

    private void OnInsertAttempt(Entity<ShishaComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ShishaComponent.HoseContainer && args.EntityUid != ent.Comp.Hose)
            args.Cancel();
    }

    private void OnVerbs(Entity<ShishaComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.Hose == null || !IsDocked(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("shisha-take-hose"),
            Act = () => TryTakeHose(ent, user),
        });
    }

    public bool IsDocked(Entity<ShishaComponent> ent)
    {
        return _containers.TryGetContainer(ent, ShishaComponent.HoseContainer, out var slot)
            && ent.Comp.Hose is { } hose && slot.Contains(hose);
    }

    protected override void HandleHoseClick(Entity<ShishaComponent> ent, EntityUid user)
    {
        if (ent.Comp.Hose is { } hose && _hands.IsHolding(user, hose))
        {
            ReturnHose(ent);
            return;
        }

        TryTakeHose(ent, user);
    }

    public bool TryTakeHose(Entity<ShishaComponent> ent, EntityUid user)
    {
        if (Terminating(ent) || !IsDocked(ent) || !_blocker.CanInteract(user, ent)
            || _containers.IsEntityInContainer(user) || !TryGetEndpoint(ent, out _)
            || !_interaction.InRangeAndAccessible(user, ent.Owner))
            return false;

        if (!_hands.TryPickupAnyHand(user, ent.Comp.Hose!.Value))
        {
            _popup.PopupEntity(Loc.GetString("shisha-hands-full"), ent, user);
            return false;
        }

        UpdateVisual(ent);
        return true;
    }

    private void OnInteractUsing(Entity<ShishaComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<ShishaHoseComponent>(args.Used))
        {
            LoadOrLight(ent, ref args);
            return;
        }

        args.Handled = true;
        if (args.Used != ent.Comp.Hose)
        {
            _popup.PopupEntity(Loc.GetString("shisha-wrong-hose"), ent, args.User);
            return;
        }

        ReturnHose(ent);
    }

    public void ReturnHose(Entity<ShishaComponent> ent)
    {
        if (Terminating(ent) || !TryComp<ShishaHoseComponent>(ent.Comp.Hose, out var hose)
            || Terminating(ent.Comp.Hose.Value))
            return;

        CancelPuff(hose);
        RemComp<ShishaHoseVisualsComponent>(ent.Comp.Hose.Value);
        UpdateAppearance(ent, true);
        if (IsDocked(ent))
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(ent, ShishaComponent.HoseContainer);
        // Internal retraction must also work from a hand with removal restrictions.
        _containers.Insert(ent.Comp.Hose.Value, slot, force: true);
    }

    private void OnHoseRemoved(Entity<ShishaHoseComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        // Never move entities from inside a container removal callback. Update handles retraction
        // after the hand/storage transaction finishes, but pending use is invalidated immediately.
        CancelPuff(ent.Comp);
    }

    private void OnThrow(Entity<ShishaHoseComponent> ent, ref ThrowItemAttemptEvent args)
    {
        args.Cancelled = true;
        if (TryComp<ShishaComponent>(ent.Comp.Base, out var shisha))
            ReturnHose((ent.Comp.Base.Value, shisha));
    }

    private void CancelPuff(ShishaHoseComponent hose)
    {
        var puff = hose.Puff;
        hose.Puff = null;
        if (_doAfter.IsRunning(puff))
            _doAfter.Cancel(puff);
    }

    /// <summary>Only world items and items held by an uncontained player are valid endpoints.</summary>
    private bool TryGetEndpoint(EntityUid item, out EntityUid endpoint)
    {
        endpoint = item;
        if (!_containers.TryGetContainingContainer((item, null, null), out var container))
            return true;

        endpoint = container.Owner;
        return _hands.IsHolding(endpoint, item) && !_containers.IsEntityInContainer(endpoint);
    }

    private bool ValidConnection(Entity<ShishaComponent> ent, EntityUid user)
    {
        return !Terminating(ent) && ent.Comp.Hose is { } hose && !IsDocked(ent)
            && _hands.IsHolding(user, hose) && !_containers.IsEntityInContainer(user)
            && TryGetEndpoint(ent, out var endpoint)
            // The generic interaction helper can accept overlapping fixtures before checking maps.
            && Transform(user).MapID == Transform(endpoint).MapID
            && _interaction.InRangeUnobstructed(user, endpoint, ent.Comp.HoseLength);
    }

    public override void Update(float frameTime)
    {
        _connectionCheckAccumulator += frameTime;
        if (_connectionCheckAccumulator < ConnectionCheckInterval)
            return;

        // Check once after a slow frame rather than repeating expensive checks to catch up.
        var elapsed = _connectionCheckAccumulator;
        _connectionCheckAccumulator %= ConnectionCheckInterval;
        elapsed -= _connectionCheckAccumulator;

        var query = EntityQueryEnumerator<ShishaComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var shisha, out var metadata))
        {
            if (metadata.EntityPaused)
                continue;

            UpdateFuel((uid, shisha), elapsed);
            if (shisha.Hose is not { } hose || IsDocked((uid, shisha)) || Terminating(hose))
                continue;

            if (!TryGetEndpoint(hose, out var holder) || !ValidConnection((uid, shisha), holder))
                ReturnHose((uid, shisha));
        }
    }

    private void UpdateVisual(Entity<ShishaComponent> ent)
    {
        UpdateAppearance(ent, false);
        var visual = EnsureComp<ShishaHoseVisualsComponent>(ent.Comp.Hose!.Value);
        visual.Target = ent;
        visual.Sprite = ent.Comp.RopeSprite;
        visual.Sag = ent.Comp.HoseSag;
        visual.OffsetA = new System.Numerics.Vector2(0.125f, -0.125f);
        // Match the hose socket on the 32px SS220 base sprite: 6px right, 3px up from center.
        visual.OffsetB = new System.Numerics.Vector2(6f / 32f, 3f / 32f);
        Dirty(ent.Comp.Hose.Value, visual);
    }

    private void OnSmoke(Entity<ShishaHoseComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != args.User)
            return;

        args.Handled = true;
        TryStartPuff(ent, args.User);
    }

    private void OnUseInHand(Entity<ShishaHoseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStartPuff(ent, args.User);
    }

    private void TryStartPuff(Entity<ShishaHoseComponent> ent, EntityUid user)
    {
        if (!TryComp<ShishaComponent>(ent.Comp.Base, out var shisha)
            || shisha.Hose != ent.Owner || !ValidConnection((ent.Comp.Base.Value, shisha), user)
            || !_blocker.CanInteract(user, ent) || !HasComp<BloodstreamComponent>(user)
            || !_ingestion.HasMouthAvailable(user, user) || _doAfter.IsRunning(ent.Comp.Puff))
            return;

        if (shisha.Dose <= 0 || !_solutions.TryGetSolution(ent.Comp.Base.Value, shisha.Solution, out _, out var solution))
            return;

        // Empty puffs still bubble, but filler cannot be inhaled without burning coal.
        if (solution.Volume > 0 && !CanSmoke((ent.Comp.Base.Value, shisha), user))
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, shisha.PuffDuration,
            new ShishaPuffDoAfterEvent(), ent, target: user, used: ent)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            CancelDuplicate = false,
        };
        _doAfter.TryStartDoAfter(doAfter, out ent.Comp.Puff);
    }

    private void OnPuff(Entity<ShishaHoseComponent> ent, ref ShishaPuffDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Puff != args.DoAfter.Id)
            return;

        ent.Comp.Puff = null;
        args.Handled = true;
        if (args.Cancelled || !TryComp<ShishaComponent>(ent.Comp.Base, out var shisha)
            || shisha.Hose != ent.Owner || !ValidConnection((ent.Comp.Base.Value, shisha), args.User)
            || !_blocker.CanInteract(args.User, ent) || !HasComp<BloodstreamComponent>(args.User)
            || !_ingestion.HasMouthAvailable(args.User, args.User) || shisha.Dose <= 0
            || !_solutions.TryGetSolution(ent.Comp.Base.Value, shisha.Solution, out var sol, out var solution))
            return;

        if (solution.Volume == 0)
        {
            _audio.PlayPvs(shisha.EmptyPuffSound, args.User);
            _popup.PopupEntity(Loc.GetString("shisha-empty"), ent, args.User);
            return;
        }

        if (!CanSmoke((ent.Comp.Base.Value, shisha), args.User))
            return;

        var inhaled = _solutions.SplitSolution(sol.Value, Content.Shared.FixedPoint.FixedPoint2.Min(shisha.Dose, solution.Volume));
        if (!_bloodstream.TryAddToBloodstream(args.User, inhaled))
        {
            // A missing/full bloodstream must not silently destroy the dose.
            _solutions.TryAddSolution(sol.Value, inhaled);
            return;
        }

        _reactive.DoEntityReaction(args.User, inhaled, ReactionMethod.Ingestion);
        Spawn(shisha.PuffPrototype, Transform(args.User).Coordinates);
        _audio.PlayPvs(shisha.PuffSound, args.User);
        _popup.PopupEntity(Loc.GetString("shisha-puff"), args.User, args.User);
    }

    private void OnBaseShutdown(Entity<ShishaComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Hose is not { } hose)
            return;

        ent.Comp.Hose = null;
        if (TryComp<ShishaHoseComponent>(hose, out var component))
            CancelPuff(component);
        if (!Terminating(hose))
            QueueDel(hose);
    }

    private void OnHoseShutdown(Entity<ShishaHoseComponent> ent, ref ComponentShutdown args)
    {
        CancelPuff(ent.Comp);
        if (TryComp<ShishaComponent>(ent.Comp.Base, out var shisha) && !Terminating(ent.Comp.Base.Value))
        {
            shisha.Hose = null;
            UpdateAppearance((ent.Comp.Base.Value, shisha), false);
            Dirty(ent.Comp.Base.Value, shisha);
        }
    }
}

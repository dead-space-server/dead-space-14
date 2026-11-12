using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Systems;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Tube;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared.Disposal.Unit;

/// <summary>
/// This system handles all operations relating to disposal units.
/// </summary>
public abstract partial class SharedDisposalUnitSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedDisposalHolderSystem _disposalHolder = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private PowerStateSystem _powerState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DisposalUnitComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DisposalUnitComponent, BeforeExplodeEvent>(OnExploded);
        SubscribeLocalEvent<DisposalUnitComponent, PowerChangedEvent>(OnPowerChange);
        SubscribeLocalEvent<DisposalUnitComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DisposalUnitComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<DisposalUnitComponent, GotEmaggedEvent>(OnEmagged);

        // See SharedDisposalUnitSystem.Interactions
        SubscribeLocalEvent<DisposalUnitComponent, GetVerbsEvent<InteractionVerb>>(AddInteractionVerb);
        SubscribeLocalEvent<DisposalUnitComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<DisposalUnitComponent, GetVerbsEvent<Verb>>(AddEnterOrExitVerb);
        SubscribeLocalEvent<DisposalUnitComponent, DisposalDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DisposalUnitComponent, BeforeThrowInsertEvent>(OnThrowInsert);
<<<<<<< ours

        SubscribeLocalEvent<DisposalUnitComponent, DisposalUnitComponent.UiButtonPressedMessage>(OnUiButtonPressed);

        SubscribeLocalEvent<DisposalUnitComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<DisposalUnitComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DisposalUnitComponent, PowerChangedEvent>(OnPowerChange);
        SubscribeLocalEvent<DisposalUnitComponent, ComponentInit>(OnDisposalInit);

        SubscribeLocalEvent<DisposalUnitComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(ActivatableUISystem) }); // DS14
||||||| base

        SubscribeLocalEvent<DisposalUnitComponent, DisposalUnitComponent.UiButtonPressedMessage>(OnUiButtonPressed);

        SubscribeLocalEvent<DisposalUnitComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<DisposalUnitComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DisposalUnitComponent, PowerChangedEvent>(OnPowerChange);
        SubscribeLocalEvent<DisposalUnitComponent, ComponentInit>(OnDisposalInit);

        SubscribeLocalEvent<DisposalUnitComponent, ActivateInWorldEvent>(OnActivate);
=======
        SubscribeLocalEvent<DisposalUnitComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<DisposalUnitComponent, ActivateInWorldEvent>(OnActivate);
>>>>>>> theirs
        SubscribeLocalEvent<DisposalUnitComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<DisposalUnitComponent, ContainerRelayMovementEntityEvent>(OnMovement);
        SubscribeLocalEvent<DisposalUnitComponent, CanDropTargetEvent>(OnCanDragDropOn);
        SubscribeLocalEvent<DisposalUnitComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<DisposalUnitComponent, GetDumpableVerbEvent>(OnGetDumpableVerb);
        SubscribeLocalEvent<DisposalUnitComponent, DumpEvent>(OnDump);

<<<<<<< ours
    private void AddDisposalAltVerbs(Entity<DisposalUnitComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
            return;
        // DS14-end

        var uid = ent.Owner;
        var component = ent.Comp;

        // Behavior for if the disposals bin has items in it
        if (component.Container.ContainedEntities.Count > 0)
        {
            // Verbs to flush the unit
            AlternativeVerb flushVerb = new()
            {
                Act = () => ManualEngage(uid, component),
                Text = Loc.GetString("disposal-flush-verb-get-data-text"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                Priority = 1,
            };
            args.Verbs.Add(flushVerb);

            // Verb to eject the contents
            AlternativeVerb ejectVerb = new()
            {
                Act = () => TryEjectContents(uid, component),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("disposal-eject-verb-get-data-text")
            };
            args.Verbs.Add(ejectVerb);
        }
||||||| base
    private void AddDisposalAltVerbs(Entity<DisposalUnitComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var uid = ent.Owner;
        var component = ent.Comp;

        // Behavior for if the disposals bin has items in it
        if (component.Container.ContainedEntities.Count > 0)
        {
            // Verbs to flush the unit
            AlternativeVerb flushVerb = new()
            {
                Act = () => ManualEngage(uid, component),
                Text = Loc.GetString("disposal-flush-verb-get-data-text"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                Priority = 1,
            };
            args.Verbs.Add(flushVerb);

            // Verb to eject the contents
            AlternativeVerb ejectVerb = new()
            {
                Act = () => TryEjectContents(uid, component),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("disposal-eject-verb-get-data-text")
            };
            args.Verbs.Add(ejectVerb);
        }
=======
        // See SharedDisposalUnitSystem.Visuals
        SubscribeLocalEvent<DisposalUnitComponent, DisposalUnitUiButtonPressedMessage>(OnUiButtonPressed);
>>>>>>> theirs
    }

<<<<<<< ours
    private void AddInsertVerb(EntityUid uid, DisposalUnitComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
            return;
        // DS14-end

        if (!ActionBlockerSystem.CanDrop(args.User))
            return;

        if (!CanInsert(uid, component, args.Using.Value))
            return;

        InteractionVerb insertVerb = new()
        {
            Text = Name(args.Using.Value),
            Category = VerbCategory.Insert,
            Act = () =>
            {
                _handsSystem.TryDropIntoContainer((args.User, args.Hands), args.Using.Value, component.Container, checkActionBlocker: false);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Using.Value)} into {ToPrettyString(uid)}");
                AfterInsert(uid, component, args.Using.Value, args.User);
            }
        };

        args.Verbs.Add(insertVerb);
    }
||||||| base
    private void AddInsertVerb(EntityUid uid, DisposalUnitComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (!ActionBlockerSystem.CanDrop(args.User))
            return;

        if (!CanInsert(uid, component, args.Using.Value))
            return;

        InteractionVerb insertVerb = new()
        {
            Text = Name(args.Using.Value),
            Category = VerbCategory.Insert,
            Act = () =>
            {
                _handsSystem.TryDropIntoContainer((args.User, args.Hands), args.Using.Value, component.Container, checkActionBlocker: false);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Using.Value)} into {ToPrettyString(uid)}");
                AfterInsert(uid, component, args.Using.Value, args.User);
            }
        };

        args.Verbs.Add(insertVerb);
    }
=======
    #region: Event handling
>>>>>>> theirs

    protected virtual void OnComponentInit(Entity<DisposalUnitComponent> ent, ref ComponentInit args)
    {
<<<<<<< ours
        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.Args.User) ||
            HasComp<DemonShadowComponent>(args.Args.Target.Value))
        {
            args.Handled = true;
            return;
        }
        // DS14-end

        AfterInsert(uid, component, args.Args.Target.Value, args.Args.User, doInsert: true);

        args.Handled = true;
    }

    private void OnThrowInsert(Entity<DisposalUnitComponent> ent, ref BeforeThrowInsertEvent args)
    {
        if (!CanInsert(ent, ent, args.ThrownEntity))
            args.Cancelled = true;
||||||| base
        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        AfterInsert(uid, component, args.Args.Target.Value, args.Args.User, doInsert: true);

        args.Handled = true;
    }

    private void OnThrowInsert(Entity<DisposalUnitComponent> ent, ref BeforeThrowInsertEvent args)
    {
        if (!CanInsert(ent, ent, args.ThrownEntity))
            args.Cancelled = true;
=======
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, nameof(DisposalUnitComponent));
>>>>>>> theirs
    }

    private void OnExploded(Entity<DisposalUnitComponent> ent, ref BeforeExplodeEvent args)
    {
        args.Contents.AddRange(GetContainedEntities(ent));
    }

    private void OnPowerChange(Entity<DisposalUnitComponent> ent, ref PowerChangedEvent args)
    {
        RecalculateFlushTime(ent, true);
        UpdateVisualState(ent);
    }

    private void OnAnchorChanged(Entity<DisposalUnitComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

<<<<<<< ours
        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
        {
            args.Handled = true;
            return;
        }
        // DS14-end

        args.Handled = true;
        _ui.TryToggleUi(uid, DisposalUnitComponent.DisposalUnitUiKey.Key, args.User);
||||||| base
        args.Handled = true;
        _ui.TryToggleUi(uid, DisposalUnitComponent.DisposalUnitUiKey.Key, args.User);
=======
        EjectContents(ent);
>>>>>>> theirs
    }

    protected void OnPreventCollide(Entity<DisposalUnitComponent> ent, ref PreventCollideEvent args)
    {
<<<<<<< ours
        if (args.Handled || !args.CanReach)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
        {
            args.Handled = true;
            return;
        }
        // DS14-end

        if (!HasComp<HandsComponent>(args.User))
        {
            return;
        }
||||||| base
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<HandsComponent>(args.User))
        {
            return;
        }
=======
        var otherBody = args.OtherEntity;
>>>>>>> theirs

        // Items dropped shouldn't collide but items thrown should
        if (HasComp<ItemComponent>(otherBody) && !HasComp<ThrownItemComponent>(otherBody))
        {
            args.Cancelled = true;
        }
    }

    protected void OnEmagged(Entity<DisposalUnitComponent> ent, ref GotEmaggedEvent args)
    {
        ent.Comp.DisablePressure = true;
        args.Handled = true;
    }

    private void OnGetDumpableVerb(Entity<DisposalUnitComponent> ent, ref GetDumpableVerbEvent args)
    {
        args.Verb = Loc.GetString("dump-disposal-verb-name", ("unit", ent));
    }

    private void OnDump(Entity<DisposalUnitComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.PlaySound = true;

        foreach (var entity in args.DumpQueue)
        {
            Insert(ent, entity, args.User);
        }
    }

<<<<<<< ours
    private void OnAnchorChanged(EntityUid uid, DisposalUnitComponent component, ref AnchorStateChangedEvent args)
    {
        if (Terminating(uid))
            return;

        UpdateVisualState(uid, component);
        if (!args.Anchored)
            TryEjectContents(uid, component);
    }

    private void OnDragDropOn(EntityUid uid, DisposalUnitComponent component, ref DragDropTargetEvent args)
    {
        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
        {
            args.Handled = true;
            return;
        }
        // DS14-end

        args.Handled = TryInsert(uid, args.Dragged, args.User);
    }
||||||| base
    private void OnAnchorChanged(EntityUid uid, DisposalUnitComponent component, ref AnchorStateChangedEvent args)
    {
        if (Terminating(uid))
            return;

        UpdateVisualState(uid, component);
        if (!args.Anchored)
            TryEjectContents(uid, component);
    }

    private void OnDragDropOn(EntityUid uid, DisposalUnitComponent component, ref DragDropTargetEvent args)
    {
        args.Handled = TryInsert(uid, args.Dragged, args.User);
    }
=======
    #endregion
>>>>>>> theirs

    /// <summary>
    /// Checks whether a disposal unit can flush.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>True if the disposal unit can flush.</returns>
    public bool CanFlush(Entity<DisposalUnitComponent> ent)
    {
        return GetState(ent) == DisposalsPressureState.Ready
               && _power.IsPowered(ent.Owner)
               && Transform(ent).Anchored;
    }

    /// <summary>
    /// Gets the current pressure state of a disposals unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>The disposal unit's pressure state.</returns>
    public DisposalsPressureState GetState(Entity<DisposalUnitComponent> ent)
    {
        var nextPressure = ent.Comp.NextPressurized - _timing.CurTime;
        var pressurizeTime = 1f / ent.Comp.PressurePerSecond;
        var pressurizeDuration = pressurizeTime - ent.Comp.FlushDelay.TotalSeconds;

        if (nextPressure.TotalSeconds > pressurizeDuration)
            return DisposalsPressureState.Flushed;

        if (nextPressure > TimeSpan.Zero)
            return DisposalsPressureState.Pressurizing;

        return DisposalsPressureState.Ready;
    }

    /// <summary>
    /// Handles the actual insertion of an entity into a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="inserted">The entity inserted.</param>
    /// <param name="user">The one who inserted the entity.</param>
    /// <param name="doInsert">Do the insertion now.</param>
    protected void Insert(Entity<DisposalUnitComponent> ent, EntityUid inserted, EntityUid? user = null)
    {
        if (ent.Comp.Container != null)
        {
            // Note: the entity may have been inserted via SharedHandsSystem already
            _container.Insert(inserted, ent.Comp.Container);
        }

        if (!GetContainedEntities(ent).Contains(inserted))
            return;

        if (_timing.CurTime >= ent.Comp.NextAllowedInsertSound)
        {
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, user: user);
            ent.Comp.NextAllowedInsertSound = _timing.CurTime + ent.Comp.InsertSoundDelay;
        }

        if (user != inserted && user != null)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(ent)}");
        }

        AutomaticEngage(ent);

        _joints.RecursiveClearJoints(inserted);

        _ui.CloseUi(ent.Owner, DisposalUnitUiKey.Key, inserted);

        UpdateVisualState(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DisposalUnitComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var unit, out var metadata))
        {
            UpdateDisposalUnit((uid, unit), metadata);
        }
    }

    private void UpdateDisposalUnit(Entity<DisposalUnitComponent> ent, MetaDataComponent metadata)
    {
        var state = GetState(ent);

        // Check if we need a state update
        if (ent.Comp.NextPressurized > _timing.CurTime)
        {
            UpdateState(ent, state);
            return;
        }

        // Check if we need to flush
        if (ent.Comp.NextFlush != null &&
            ent.Comp.NextFlush.Value < _timing.CurTime)
        {
            TryFlush(ent);
        }

        UpdateState(ent, state);
    }

    private void UpdateState(Entity<DisposalUnitComponent> ent, DisposalsPressureState state)
    {
        if (ent.Comp.State == state)
            return;

        ent.Comp.State = state;

        switch (state)
        {
            case DisposalsPressureState.Ready:
                ent.Comp.NextPressurized = TimeSpan.Zero;
                _powerState.SetWorkingState(ent.Owner, false);
                break;
            case DisposalsPressureState.Pressurizing:
                _powerState.SetWorkingState(ent.Owner, true);
                break;
        }

<<<<<<< ours
    protected void OnCanDragDropOn(EntityUid uid, DisposalUnitComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
        {
            args.CanDrop = false;
            args.Handled = true;
            return;
        }
        // DS14-end

        args.CanDrop = CanInsert(uid, component, args.Dragged);
        args.Handled = true;
||||||| base
    protected void OnCanDragDropOn(EntityUid uid, DisposalUnitComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = CanInsert(uid, component, args.Dragged);
        args.Handled = true;
=======
        RecalculateFlushTime(ent, true);
        UpdateVisualState(ent);
>>>>>>> theirs
    }

    /// <summary>
    /// Try to flush a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>True if the flush was successful.</returns>
    public bool TryFlush(Entity<DisposalUnitComponent> ent)
    {
<<<<<<< ours
        // DS14-start
        if (HasComp<DemonShadowComponent>(entity))
            return false;
        // DS14-end

        // TODO: All of the below should be using the EXISTING EVENT
        if (!Containers.CanInsert(entity, component.Container))
||||||| base
        // TODO: All of the below should be using the EXISTING EVENT
        if (!Containers.CanInsert(entity, component.Container))
=======
        if (!CanFlush(ent))
>>>>>>> theirs
            return false;

        var beforeFlushArgs = new BeforeDisposalFlushEvent();
        RaiseLocalEvent(ent, beforeFlushArgs);

        if (beforeFlushArgs.Cancelled)
        {
            SetEngage(ent, false);
            return false;
        }

        var xform = Transform(ent);

        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
<<<<<<< ours
    }

    public void DoInsertDisposalUnit(EntityUid uid,
        EntityUid toInsert,
        EntityUid user,
        DisposalUnitComponent? disposal = null)
    {
        // DS14-start
        if (IsDemonShadowDisposalUser(user) ||
            HasComp<DemonShadowComponent>(toInsert))
        {
            return;
        }
        // DS14-end

        if (!Resolve(uid, ref disposal))
            return;

        if (!Containers.Insert(toInsert, disposal.Container))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} inserted {ToPrettyString(toInsert)} into {ToPrettyString(uid)}");
        AfterInsert(uid, disposal, toInsert, user);
    }

    public virtual void AfterInsert(EntityUid uid,
        DisposalUnitComponent component,
        EntityUid inserted,
        EntityUid? user = null,
        bool doInsert = false)
    {
        Audio.PlayPredicted(component.InsertSound, uid, user: user);
        if (doInsert && !Containers.Insert(inserted, component.Container))
            return;

        if (user != inserted && user != null)
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        QueueAutomaticEngage(uid, component);

        _ui.CloseUi(uid, DisposalUnitComponent.DisposalUnitUiKey.Key, inserted);
||||||| base
    }

    public void DoInsertDisposalUnit(EntityUid uid,
        EntityUid toInsert,
        EntityUid user,
        DisposalUnitComponent? disposal = null)
    {
        if (!Resolve(uid, ref disposal))
            return;

        if (!Containers.Insert(toInsert, disposal.Container))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} inserted {ToPrettyString(toInsert)} into {ToPrettyString(uid)}");
        AfterInsert(uid, disposal, toInsert, user);
    }

    public virtual void AfterInsert(EntityUid uid,
        DisposalUnitComponent component,
        EntityUid inserted,
        EntityUid? user = null,
        bool doInsert = false)
    {
        Audio.PlayPredicted(component.InsertSound, uid, user: user);
        if (doInsert && !Containers.Insert(inserted, component.Container))
            return;

        if (user != inserted && user != null)
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        QueueAutomaticEngage(uid, component);

        _ui.CloseUi(uid, DisposalUnitComponent.DisposalUnitUiKey.Key, inserted);
=======
>>>>>>> theirs

        // Try to find an entry into disposals
        Entity<DisposalTubeComponent>? tube = null;

<<<<<<< ours
    public bool TryInsert(EntityUid unitId, EntityUid toInsertId, EntityUid? userId, DisposalUnitComponent? unit = null)
    {
        if (!Resolve(unitId, ref unit))
            return false;

        // DS14-start
        if (userId is { } user && IsDemonShadowDisposalUser(user))
            return false;
        // DS14-end

        if (userId.HasValue && !HasComp<HandsComponent>(userId) && toInsertId != userId) // Mobs like mouse can Jump inside even with no hands
||||||| base
    public bool TryInsert(EntityUid unitId, EntityUid toInsertId, EntityUid? userId, DisposalUnitComponent? unit = null)
    {
        if (!Resolve(unitId, ref unit))
            return false;

        if (userId.HasValue && !HasComp<HandsComponent>(userId) && toInsertId != userId) // Mobs like mouse can Jump inside even with no hands
=======
        foreach (var tubeUid in _map.GetLocal(xform.GridUid.Value, grid, xform.Coordinates))
>>>>>>> theirs
        {
            if (HasComp<DisposalEntryComponent>(tubeUid) &&
                TryComp<DisposalTubeComponent>(tubeUid, out var tubeComp))
            {
                tube = new Entity<DisposalTubeComponent>(tubeUid, tubeComp);
                break;
            }
        }

        if (tube == null)
        {
            SetEngage(ent, false);
            return false;
        }

        // Try to transfer entities from the unit into disposals.
        TryTransfer(ent, tube.Value, beforeFlushArgs.Tags);

        ent.Comp.NextPressurized = _timing.CurTime;

        if (!ent.Comp.DisablePressure)
        {
            ent.Comp.NextPressurized += TimeSpan.FromSeconds(1f / ent.Comp.PressurePerSecond);
        }

        SetEngage(ent, false);
        return true;
    }

    /// <summary>
    /// Tries to transfer all entities in a disposal unit to the disposals system.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="tube">The tube leading into disposals.</param>
    /// <returns>True if the transfer was successful.</returns>
    public bool TryTransfer(Entity<DisposalUnitComponent> ent, Entity<DisposalTubeComponent> tube, IEnumerable<string>? tags = null)
    {
        if (_net.IsClient && !_timing.IsFirstTimePredicted)
            return false;

        if (GetContainedEntityCount(ent) == 0)
            return false;

        var xform = Transform(ent);
        var holderUid = Spawn(ent.Comp.HolderPrototypeId, _xform.GetMapCoordinates(ent, xform: xform));
        var holder = new Entity<DisposalHolderComponent>(holderUid, Comp<DisposalHolderComponent>(holderUid));

        if (holder.Comp.Container == null)
            return false;

        foreach (var entity in GetContainedEntities(ent))
        {
            _container.Insert(entity, holder.Comp.Container);
            _disposalHolder.AttachEntity((holderUid, holder), entity);
        }

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                _disposalHolder.AddTag(holder, tag);
            }

            Dirty(holder);
        }

        // Take in the local atmos and pass it to the spawned disposals holder.
        IntakeAir(ent, xform);
        _disposalHolder.TransferAtmos(holder, ent);

        return _disposalHolder.TryEnterTube(holder, (tube, tube.Comp));
    }

    /// <summary>
    /// Remove all entities currently in a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void EjectContents(Entity<DisposalUnitComponent> ent)
    {
        foreach (var toRemove in GetContainedEntities(ent))
        {
            Remove(ent, toRemove);
        }

        UpdateVisualState(ent);
    }

    /// <summary>
    /// Remove an entity from a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="toRemove">The entity to remove.</param>
    public void Remove(Entity<DisposalUnitComponent> ent, EntityUid toRemove)
    {
        if (_timing.ApplyingState)
            return;

        if (!Terminating(toRemove) &&
            ent.Comp.Container != null &&
            _container.Remove(toRemove, ent.Comp.Container))
        {
            _climb.Climb(toRemove, toRemove, ent, silent: true);
        }

        RecalculateFlushTime(ent);
        UpdateVisualState(ent);
    }

    /// <summary>
    /// Recalculates when a disposal will flush next
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="reset">Reset any in-progress flushes.</param>
    public void RecalculateFlushTime(Entity<DisposalUnitComponent> ent, bool reset = false)
    {
        TimeSpan? nextFlush = null;

        // Reset next flush?
        if (reset)
        {
            ent.Comp.NextFlush = null;
        }

        // If engaged/automatically engaged, figure out the next flush time
        if (ent.Comp.Engaged || GetContainedEntityCount(ent) > 0)
        {
            var newFlush = ent.Comp.Engaged
                ? _timing.CurTime + ent.Comp.ManualFlushTime
                : _timing.CurTime + ent.Comp.AutomaticEngageTime;

            if (GetState(ent) != DisposalsPressureState.Ready)
            {
                newFlush += ent.Comp.NextPressurized;
            }

            nextFlush = (ent.Comp.NextFlush ?? TimeSpan.MaxValue);

            // Check if there is a flush in progress that will occur
            // sooner than what was calculated
            nextFlush = TimeSpan.FromSeconds(Math.Min(nextFlush.Value.TotalSeconds, newFlush.TotalSeconds));
        }

        ent.Comp.NextFlush = nextFlush;
        Dirty(ent);

        UpdateUI(ent);
    }

    // DS14-start
    private bool IsDemonShadowDisposalUser(EntityUid user)
    {
        return HasComp<DemonShadowComponent>(user);
    }
    // DS14-end

    /// <summary>
    /// Sets a disposal unit to move towards flushing itself.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="metadata">The disposal unit's metadata.</param>
    public void SetEngage(Entity<DisposalUnitComponent> ent, bool engaged)
    {
        ent.Comp.Engaged = engaged;

        RecalculateFlushTime(ent);
        UpdateVisualState(ent);
    }

    /// <summary>
    /// Primes a disposal unit to automatically flush sometime in the future.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="metadata">The disposal unit's metadata.</param>
    public void AutomaticEngage(Entity<DisposalUnitComponent> ent)
    {
        if (!ent.Comp.AutomaticEngage)
            return;

<<<<<<< ours
        // DS14-start
        if (IsDemonShadowDisposalUser(player))
            return;
        // DS14-end

        switch (args.Button)
        {
            case DisposalUnitComponent.UiButton.Eject:
                TryEjectContents(uid, component);
                _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):player} hit eject button on {ToPrettyString(uid)}");
                break;
            case DisposalUnitComponent.UiButton.Engage:
                ToggleEngage(uid, component);
                _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):player} hit flush button on {ToPrettyString(uid)}, it's now {(component.Engaged ? "on" : "off")}");
                break;
            case DisposalUnitComponent.UiButton.Power:
                _power.TogglePower(uid, user: args.Actor);
                break;
            default:
                throw new ArgumentOutOfRangeException($"{ToPrettyString(player):player} attempted to hit a nonexistant button on {ToPrettyString(uid)}");
        }
||||||| base
        switch (args.Button)
        {
            case DisposalUnitComponent.UiButton.Eject:
                TryEjectContents(uid, component);
                _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):player} hit eject button on {ToPrettyString(uid)}");
                break;
            case DisposalUnitComponent.UiButton.Engage:
                ToggleEngage(uid, component);
                _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):player} hit flush button on {ToPrettyString(uid)}, it's now {(component.Engaged ? "on" : "off")}");
                break;
            case DisposalUnitComponent.UiButton.Power:
                _power.TogglePower(uid, user: args.Actor);
                break;
            default:
                throw new ArgumentOutOfRangeException($"{ToPrettyString(player):player} attempted to hit a nonexistant button on {ToPrettyString(uid)}");
        }
=======
        RecalculateFlushTime(ent);
>>>>>>> theirs
    }

    /// <summary>
    /// Toggles a disposal unit between 'engaged' and 'disengaged'.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void ToggleEngage(Entity<DisposalUnitComponent> ent)
    {
        SetEngage(ent, !ent.Comp.Engaged);
    }

    /// <summary>
    /// All entities contained in a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>A copy of the disposal unit's ContainedEntities list.</returns>
    public IReadOnlyList<EntityUid> GetContainedEntities(Entity<DisposalUnitComponent> ent)
    {
<<<<<<< ours
        // This is not an interaction, activation, or alternative verb type because unfortunately most users are
        // unwilling to accept that this is where they belong and don't want to accidentally climb inside.
        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
            return;
        // DS14-end

        if (!args.CanAccess ||
            !args.CanInteract ||
            component.Container.ContainedEntities.Contains(args.User) ||
            !ActionBlockerSystem.CanMove(args.User))
        {
            return;
        }

        if (!CanInsert(uid, component, args.User))
            return;
||||||| base
        // This is not an interaction, activation, or alternative verb type because unfortunately most users are
        // unwilling to accept that this is where they belong and don't want to accidentally climb inside.
        if (!args.CanAccess ||
            !args.CanInteract ||
            component.Container.ContainedEntities.Contains(args.User) ||
            !ActionBlockerSystem.CanMove(args.User))
        {
            return;
        }

        if (!CanInsert(uid, component, args.User))
            return;
=======
        if (ent.Comp.Container == null)
            return new List<EntityUid>();
>>>>>>> theirs

        return ent.Comp.Container.ContainedEntities.ToList();
    }

    /// <summary>
    /// The number of entities contained in a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>The entity count.</returns>
    public int GetContainedEntityCount(Entity<DisposalUnitComponent> ent)
    {
<<<<<<< ours
        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
            return;
        // DS14-end

        args.Verb = Loc.GetString("dump-disposal-verb-name", ("unit", ent));
||||||| base
        args.Verb = Loc.GetString("dump-disposal-verb-name", ("unit", ent));
=======
        return GetContainedEntities(ent).Count;
>>>>>>> theirs
    }

    /// <summary>
    /// Takes the atmos surrounding the disposal unit into itself.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="xform">The disposal unit's transform.</param>
    protected virtual void IntakeAir(Entity<DisposalUnitComponent> ent, TransformComponent xform)
    {
<<<<<<< ours
        if (args.Handled)
            return;

        // DS14-start
        if (IsDemonShadowDisposalUser(args.User))
        {
            args.Handled = true;
            args.PlaySound = false;
            return;
        }
        // DS14-end

        args.Handled = true;
        args.PlaySound = true;

        foreach (var entity in args.DumpQueue)
        {
            DoInsertDisposalUnit(ent, entity, args.User);
        }
||||||| base
        if (args.Handled)
            return;

        args.Handled = true;
        args.PlaySound = true;

        foreach (var entity in args.DumpQueue)
        {
            DoInsertDisposalUnit(ent, entity, args.User);
        }
=======
        // Handled by the server
>>>>>>> theirs
    }
}

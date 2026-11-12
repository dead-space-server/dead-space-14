using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Blocking;

public sealed partial class BlockingSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private FixtureSystem _fixtureSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private TurfSystem _turf = default!;
    // DS14-start
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    // DS14-end

    [Dependency] private EntityQuery<BlockingComponent> _blockQuery = default!;
    //[Dependency] private EntityQuery<BlockingUserComponent> _userQuery = default!;
    //[Dependency] private EntityQuery<HandsComponent> _handQuery = default!;
    //[Dependency] private EntityQuery<MobStateComponent> _mobQuery = default!;
    // ps я не уверен что так делать безопасно, но на это жалуются тесты не дай бог что то упадёт

    public override void Initialize()
||||||| base
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
=======
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FixtureSystem _fixtureSystem = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<BlockingComponent> _blockQuery;
    [Dependency] private EntityQuery<BlockingUserComponent> _userQuery;
    [Dependency] private EntityQuery<HandsComponent> _handQuery;
    [Dependency] private EntityQuery<MobStateComponent> _mobQuery;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<BlockingComponent> entity, ref MapInitEvent args)
>>>>>>> theirs
    {
        if (!CanBlock(entity.AsNullable()))
            return;

<<<<<<< ours
        SubscribeLocalEvent<BlockingComponent, GetVerbsEvent<ExamineVerb>>(OnVerbExamine);
        SubscribeLocalEvent<BlockingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmpDisableItemToggleComponent, EmpItemToggleDisabledEvent>(OnEmpItemToggleDisabled); // DS14
||||||| base
        SubscribeLocalEvent<BlockingComponent, GetVerbsEvent<ExamineVerb>>(OnVerbExamine);
        SubscribeLocalEvent<BlockingComponent, MapInitEvent>(OnMapInit);
=======
        _actionContainer.EnsureAction(entity, ref entity.Comp.BlockingToggleActionEntity, entity.Comp.BlockingToggleAction);
        DirtyField(entity, entity.Comp, nameof(BlockingComponent.BlockingToggleActionEntity));
>>>>>>> theirs
    }

    [SubscribeLocalEvent]
    private void OnItemToggled(Entity<BlockingComponent> entity, ref ItemToggledEvent args)
    {
        if (!_handsSystem.IsHeld(entity.Owner, out var holder))
            return;

        if (args.Activated)
            StartBlocking(entity, holder.Value);
        else
            StopBlocking(entity, holder.Value);
    }

    [SubscribeLocalEvent]
    private void OnEquip(Entity<BlockingComponent> entity, ref GotEquippedHandEvent args)
    {
        if (!CanBlock(entity.AsNullable()))
            return;

        StartBlocking(entity, args.User);
    }

    [SubscribeLocalEvent]
    private void OnUnequip(Entity<BlockingComponent> entity, ref GotUnequippedHandEvent args)
    {
        StopBlocking(entity, args.User);
    }

    [SubscribeLocalEvent]
    private void OnDrop(Entity<BlockingComponent> entity, ref DroppedEvent args)
    {
        StopBlocking(entity, args.User);
    }

    [SubscribeLocalEvent]
    private void OnGetActions(Entity<BlockingComponent> entity, ref GetItemActionsEvent args)
    {
        args.AddAction(ref entity.Comp.BlockingToggleActionEntity, entity.Comp.BlockingToggleAction);
    }

    [SubscribeLocalEvent]
    private void OnToggleAction(Entity<BlockingComponent> entity, ref ToggleActionEvent args)
    {
        if (args.Handled || !CanBlock(entity.AsNullable()))
            return;

<<<<<<< ours
        // DS14-start
        if (HasComp<EmpDisableItemToggleComponent>(uid) && HasComp<EmpDisabledComponent>(uid))
        {
            CantBlockError(args.Performer);
            args.Handled = true;
            return;
        }
        // DS14-end

        var blockQuery = GetEntityQuery<BlockingComponent>();
        var handQuery = GetEntityQuery<HandsComponent>();

        if (!handQuery.TryGetComponent(args.Performer, out var hands))
||||||| base
        var blockQuery = GetEntityQuery<BlockingComponent>();
        var handQuery = GetEntityQuery<HandsComponent>();

        if (!handQuery.TryGetComponent(args.Performer, out var hands))
=======
        if (!_handQuery.TryGetComponent(args.Performer, out var hands))
>>>>>>> theirs
            return;

        var shields = _handsSystem.EnumerateHeld((args.Performer, hands)).ToArray();

        foreach (var shield in shields)
        {
            if (shield == entity.Owner)
                continue;

            if (!_blockQuery.TryGetComponent(shield, out var otherBlockComp) || !otherBlockComp.IsRaised)
                continue;

            CantBlockError(args.Performer);
            return;
        }

        if (entity.Comp.IsRaised)
            LowerShield(entity, args.Performer);
        else
            RaiseShield(entity, args.Performer);

        args.Handled = true;
    }

<<<<<<< ours
    // DS14-start
    private void OnEmpItemToggleDisabled(Entity<EmpDisableItemToggleComponent> ent, ref EmpItemToggleDisabledEvent args)
    {
        if (!TryComp<BlockingComponent>(ent.Owner, out var blocking))
            return;

        if (blocking.User is { } user)
            StopBlocking(ent.Owner, blocking, user);
    }
    // DS14-end

    private void OnShutdown(EntityUid uid, BlockingComponent component, ComponentShutdown args)
||||||| base
    private void OnShutdown(EntityUid uid, BlockingComponent component, ComponentShutdown args)
=======
    [SubscribeLocalEvent]
    private void OnShutdown(Entity<BlockingComponent> entity, ref ComponentShutdown args)
>>>>>>> theirs
    {
        //In theory the user should not be null when this fires off
        if (entity.Comp.User is not { } user)
            return;

        _actionsSystem.RemoveProvidedActions(user, entity);
        StopBlocking(entity, user);
    }

    /// <summary>
    /// Called where you want the user to start blocking
    /// Creates a new hard fixture to bodyblock
    /// Also makes the user static to prevent prediction issues
    /// </summary>
    /// <param name="entity"> The entity with the blocking component</param>
    /// <param name="user"> The entity who's using the item to block</param>
    /// <returns></returns>
    public bool RaiseShield(Entity<BlockingComponent> entity, EntityUid user)
    {
        if (entity.Comp.IsRaised)
            return false;

        var xform = Transform(user);

        var shieldName = Name(entity);

        var blockerName = Identity.Entity(user, EntityManager);
        var msgUser = Loc.GetString("action-popup-blocking-user", ("shield", shieldName));
        var msgOther = Loc.GetString("action-popup-blocking-other", ("blockerName", blockerName), ("shield", shieldName));

        //Don't allow someone to block if they're not parented to a grid
        if (xform.GridUid != xform.ParentUid)
        {
            CantBlockError(user);
            return false;
        }

        // Don't allow someone to block if they're not holding the shield
        if (!_handsSystem.IsHolding(user, entity, out _))
        {
            CantBlockError(user);
            return false;
        }

        //Don't allow someone to block if someone else is on the same tile
        var playerTileRef = _turf.GetTileRef(xform.Coordinates);
        if (playerTileRef != null)
        {
            var intersecting = _lookup.GetLocalEntitiesIntersecting(playerTileRef.Value, 0f);
            foreach (var uid in intersecting)
            {
<<<<<<< ours
                // DS14-start
                if (uid == user ||
                    !mobQuery.HasComponent(uid) ||
                    _container.IsEntityOrParentInContainer(uid))
                    continue;

                TooCloseError(user);
                return false;
                // DS14-end
||||||| base
                if (uid != user && mobQuery.HasComponent(uid))
                {
                    TooCloseError(user);
                    return false;
                }
=======
                if (uid != user && _mobQuery.HasComponent(uid))
                {
                    TooCloseError(user);
                    return false;
                }
>>>>>>> theirs
            }
        }

        //Don't allow someone to block if they're somehow not anchored.
        _transformSystem.AnchorEntity(user, xform);
        if (!xform.Anchored)
        {
            CantBlockError(user);
            return false;
        }
        _actionsSystem.SetToggled(entity.Comp.BlockingToggleActionEntity, true);
        _popupSystem.PopupEntity(msgUser, msgOther, user, user);

        if (TryComp<PhysicsComponent>(user, out var physicsComponent))
        {
            _fixtureSystem.TryCreateFixture(user,
                entity.Comp.Shape,
                BlockingComponent.BlockFixtureId,
                hard: true,
                collisionLayer: (int)CollisionGroup.WallLayer,
                body: physicsComponent);
        }

        entity.Comp.IsRaised = true;
        DirtyField(entity, entity.Comp, nameof(BlockingComponent.IsRaised));

        return true;
    }

    private void CantBlockError(EntityUid user)
    {
        var msgError = Loc.GetString("action-popup-blocking-user-cant-block");
        _popupSystem.PopupEntity(msgError, user, user);
    }

    private void TooCloseError(EntityUid user)
    {
        var msgError = Loc.GetString("action-popup-blocking-user-too-close");
        _popupSystem.PopupEntity(msgError, user, user);
    }

    /// <summary>
    /// Called where you want the user to stop blocking.
    /// </summary>
    /// <param name="entity"> The entity with the blocking component</param>
    /// <param name="user"> The entity who's using the item to block</param>
    /// <returns></returns>
    public bool LowerShield(Entity<BlockingComponent> entity, EntityUid user)
    {
        if (!entity.Comp.IsRaised)
            return false;

        var xform = Transform(user);

        var shieldName = Name(entity);

        var blockerName = Identity.Entity(user, EntityManager);
        var msgUser = Loc.GetString("action-popup-blocking-disabling-user", ("shield", shieldName));
        var msgOther = Loc.GetString("action-popup-blocking-disabling-other", ("blockerName", blockerName), ("shield", shieldName));

        //If the component blocking toggle isn't null, grab the users SharedBlockingUserComponent and PhysicsComponent
        //then toggle the action to false, unanchor the user, remove the hard fixture
        //and set the users bodytype back to their original type
        if (TryComp<BlockingUserComponent>(user, out var blockingUserComponent) && TryComp<PhysicsComponent>(user, out var physicsComponent))
        {
            if (xform.Anchored)
                _transformSystem.Unanchor(user, xform, false);

            _actionsSystem.SetToggled(entity.Comp.BlockingToggleActionEntity, false);
            _fixtureSystem.DestroyFixture(user, BlockingComponent.BlockFixtureId, body: physicsComponent);
            _physics.SetBodyType(user, blockingUserComponent.OriginalBodyType, body: physicsComponent);
            _popupSystem.PopupEntity(msgUser, msgOther, user, user);
        }

        entity.Comp.IsRaised = false;
        DirtyField(entity, entity.Comp, nameof(BlockingComponent.IsRaised));
        return true;
    }

    /// <summary>
    /// Checks if this blocking entity can block.
    /// </summary>
    /// <param name="entity">Entity we are checking.</param>
    /// <returns></returns>
    public bool CanBlock(Entity<BlockingComponent?> entity)
    {
        if (!_blockQuery.Resolve(entity, ref entity.Comp))
            return false;

        if (!_toggle.IsActivated(entity.Owner))
            return false;

        return true;
    }

    /// <summary>
    /// Tries to enable a blocking entity, allowing it to block incoming damage.
    /// </summary>
    /// <param name="entity">Blocking entity we wish to enable</param>
    /// <param name="user">User that is trying to use the blocking entity</param>
    private void StartBlocking(Entity<BlockingComponent> entity, EntityUid user)
    {
        entity.Comp.User = user;
        DirtyField(entity, entity.Comp, nameof(BlockingComponent.User));

        //To make sure that this bodytype doesn't get set as anything but the original
        if (_timing.ApplyingState || EnsureComp<BlockingUserComponent>(user, out var userComp))
            return;

        userComp.BlockingItem = entity;

        if (!TryComp<PhysicsComponent>(user, out var physicsComponent))
        {
            DirtyField(user, userComp, nameof(BlockingUserComponent.BlockingItem));
            return;
        }

        userComp.OriginalBodyType = physicsComponent.BodyType;
        DirtyFields(user, userComp, null, nameof(BlockingUserComponent.BlockingItem), nameof(BlockingUserComponent.OriginalBodyType));
    }

    /// <summary>
    /// Called where you want someone to stop blocking and to remove the <see cref="BlockingUserComponent"/> from them
    /// Won't remove the <see cref="BlockingUserComponent"/> if they're holding another blocking item
    /// </summary>
    /// <param name="entity"> The item the component is attached to</param>
    /// <param name="user"> The person holding the blocking item </param>
    private void StopBlocking(Entity<BlockingComponent> entity, EntityUid user)
    {
        if (entity.Comp.IsRaised)
            LowerShield(entity, user);

        if (!_handQuery.TryGetComponent(user, out var hands))
            return;

        var shields = _handsSystem.EnumerateHeld((user, hands)).ToArray();

        foreach (var shield in shields)
        {
            if (HasComp<BlockingComponent>(shield) && _userQuery.TryGetComponent(user, out var blockingUserComponent))
            {
                blockingUserComponent.BlockingItem = shield;
                return;
            }
        }

        RemComp<BlockingUserComponent>(user);
        entity.Comp.User = null;
        DirtyField(entity, entity.Comp, nameof(BlockingComponent.User));
    }

    private DamageModifierSet GetBlockingModifier(Entity<BlockingComponent> entity)
    {
        return entity.Comp.IsRaised ? entity.Comp.ActiveBlockModifier ?? entity.Comp.PassiveBlockModifier : entity.Comp.PassiveBlockModifier;
    }

    [SubscribeLocalEvent]
    private void OnVerbExamine(Entity<BlockingComponent> entity, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var fraction = entity.Comp.IsRaised ? entity.Comp.ActiveBlockFraction : entity.Comp.PassiveBlockFraction;
        var modifier = GetBlockingModifier(entity);

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("blocking-fraction", ("value", MathF.Round(fraction * 100, 1))));

        AppendCoefficients(modifier, msg);

        _examine.AddDetailedExamineVerb(args,
            entity.Comp,
            msg,
            Loc.GetString("blocking-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("blocking-examinable-verb-message")
        );
    }

    private void AppendCoefficients(DamageModifierSet modifiers, FormattedMessage msg)
    {
        foreach (var coefficient in modifiers.Coefficients)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Robust.Shared.Localization.Loc.GetString("blocking-coefficient-value",
                ("type", GetLocalizedDamageType(coefficient.Key)), // DS14
                ("value", MathF.Round(coefficient.Value * 100, 1))
            ));
        }

        foreach (var flat in modifiers.FlatReductions)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Robust.Shared.Localization.Loc.GetString("blocking-reduction-value",
                ("type", GetLocalizedDamageType(flat.Key)), // DS14
                ("value", flat.Value)
            ));
        }
    }

    // DS14-start
    private string GetLocalizedDamageType(string type)
    {
        return _prototype.TryIndex<DamageTypePrototype>(type, out var prototype)
            ? prototype.LocalizedName
            : type;
    }
    // DS14-end
}

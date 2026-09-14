using System.Numerics;
using Content.Shared._CM14.Scoping;
using Content.Shared.Actions;
using Robust.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
namespace Content.Shared._CM14.Scoping;
public abstract partial class SharedScopeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEyeSystem _contentEye = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private const float SmallestViewpointSize = 15;
    public override void Initialize()
    {
        InitializeUser();
        SubscribeLocalEvent<ScopeComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<ScopeComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ScopeComponent, ScopeDoAfterEvent>(OnScopeDoAfter);
        SubscribeLocalEvent<GunScopingComponent, GotUnequippedHandEvent>(OnGunUnequip);
        SubscribeLocalEvent<GunScopingComponent, HandDeselectedEvent>(OnGunDeselectHand);
        SubscribeLocalEvent<GunScopingComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<GunScopingComponent, GunShotEvent>(OnGunGunShot);
    }
    protected virtual void InitializeUser() { }
    private void OnToggleAction(Entity<ScopeComponent> scope, ref ToggleActionEvent args)
    {
        ToggleScoping(scope, args.Performer);
    }
    private void OnGunShot(Entity<ScopeComponent> scope, ref GunShotEvent args)
    {
        if (scope.Comp.User is { } user)
            Unscope(scope);
    }
    private void OnScopeDoAfter(Entity<ScopeComponent> scope, ref ScopeDoAfterEvent args)
    {
    }
    private void OnGunUnequip(Entity<GunScopingComponent> ent, ref GotUnequippedHandEvent args)
    {
        UnscopeGun(ent);
    }
    private void OnGunDeselectHand(Entity<GunScopingComponent> ent, ref HandDeselectedEvent args)
    {
        UnscopeGun(ent);
    }
    private void OnGunUnwielded(Entity<GunScopingComponent> ent, ref ItemUnwieldedEvent args)
    {
        UnscopeGun(ent);
    }
    private void OnGunGunShot(Entity<GunScopingComponent> ent, ref GunShotEvent args)
    {
        UnscopeGun(ent);
    }
    private void UnscopeGun(Entity<GunScopingComponent> gun)
    {
        if (TryComp(gun.Comp.Scope, out ScopeComponent? scope))
            Unscope((gun.Comp.Scope.Value, scope));
    }
    private void ToggleScoping(Entity<ScopeComponent> scope, EntityUid user)
    {
        if (HasComp<ScopingComponent>(user))
            Unscope(scope);
        else
            StartScoping(scope, user);
    }
    protected virtual void StartScoping(Entity<ScopeComponent> scope, EntityUid user)
    {
    }
    protected virtual void Unscope(Entity<ScopeComponent> scope)
    {
    }
    protected Vector2 GetScopeOffset(Entity<ScopeComponent> scope, Direction direction)
    {
        return direction.ToVec() * ((scope.Comp.Offset * scope.Comp.Zoom - 1) / 2);
    }
    protected virtual void DeleteRelay(Entity<ScopeComponent> scope, EntityUid? user)
    {
    }
    private bool TryGetActiveEntity(Entity<ScopeComponent> scope, out EntityUid active)
    {
        if (!scope.Comp.Attachment)
        {
            active = scope;
            return true;
        }
        if (!_container.TryGetContainingContainer((scope, null), out var container) ||
            !HasComp<GunComponent>(container.Owner))
        {
            active = default;
            return false;
        }
        active = container.Owner;
        return true;
    }
}
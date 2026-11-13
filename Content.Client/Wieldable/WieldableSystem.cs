using System.Numerics;
using Content.Client.CombatMode;
using Content.Client.Hands.Systems;
using Content.Client.Movement.Components;
using Content.Client.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.Wieldable;

public sealed partial class WieldableSystem : SharedWieldableSystem
{
    [Dependency] private CombatModeSystem _combat = default!;
    [Dependency] private EyeCursorOffsetSystem _eyeOffset = default!;
    [Dependency] private IClientGameTiming _gameTiming = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private InputSystem _input = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly HashSet<EntityUid> _aimingItems = new();
    private readonly HashSet<EntityUid> _seenAimItems = new();
    private readonly List<EntityUid> _aimingItemCopy = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, ItemUnwieldedEvent>(OnEyeOffsetUnwielded);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, HeldRelayedEvent<GetEyeOffsetRelayedEvent>>(OnGetEyeOffset);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        UpdateAimStates();
    }

    public void OnEyeOffsetUnwielded(Entity<CursorOffsetRequiresWieldComponent> entity, ref ItemUnwieldedEvent args)
    {
        if (!TryComp(entity.Owner, out EyeCursorOffsetComponent? cursorOffsetComp))
            return;

        if (_gameTiming.IsFirstTimePredicted)
        {
            SetLocalAimState(entity.Owner, false);
            cursorOffsetComp.CurrentPosition = Vector2.Zero;
            cursorOffsetComp.TargetPosition = Vector2.Zero;
        }
    }

    public void OnGetEyeOffset(Entity<CursorOffsetRequiresWieldComponent> entity, ref HeldRelayedEvent<GetEyeOffsetRelayedEvent> args)
    {
        if (!TryComp(entity.Owner, out WieldableComponent? wieldableComp))
            return;

        if (!wieldableComp.Wielded)
            return;

        if (!IsAimInputDown())
            return;

        var offset = _eyeOffset.OffsetAfterMouse(entity.Owner, null);
        if (offset == null)
            return;

        args.Args.Offset += offset.Value;
    }

    private void UpdateAimStates()
    {
        var aim = IsAimInputDown();
        _seenAimItems.Clear();

        if (_player.LocalEntity is not { } player || !TryComp<HandsComponent>(player, out var hands))
        {
            ClearLocalAimStates();
            return;
        }

        foreach (var held in _hands.EnumerateHeld((player, hands)))
        {
            if (!HasComp<CursorOffsetRequiresWieldComponent>(held))
                continue;

            _seenAimItems.Add(held);

            var aiming = aim &&
                         TryComp<WieldableComponent>(held, out var wieldable) &&
                         wieldable.Wielded;

            SetLocalAimState(held, aiming);
        }

        _aimingItemCopy.Clear();
        _aimingItemCopy.AddRange(_aimingItems);

        foreach (var uid in _aimingItemCopy)
        {
            if (_seenAimItems.Contains(uid))
                continue;

            SetLocalAimState(uid, false);
        }
    }

    private bool IsAimInputDown()
    {
        return _combat.IsInCombatMode() &&
               _input.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;
    }

    private void ClearLocalAimStates()
    {
        _aimingItemCopy.Clear();
        _aimingItemCopy.AddRange(_aimingItems);

        foreach (var uid in _aimingItemCopy)
        {
            SetLocalAimState(uid, false);
        }
    }

    private void SetLocalAimState(EntityUid uid, bool aiming)
    {
        if (aiming)
        {
            if (!_aimingItems.Add(uid))
                return;
        }
        else
        {
            if (!_aimingItems.Remove(uid))
                return;

            ResetCursorOffset(uid);
        }
    }

    private void ResetCursorOffset(EntityUid uid)
    {
        if (!TryComp(uid, out EyeCursorOffsetComponent? cursorOffset))
            return;

        cursorOffset.CurrentPosition = Vector2.Zero;
        cursorOffset.TargetPosition = Vector2.Zero;
    }
}

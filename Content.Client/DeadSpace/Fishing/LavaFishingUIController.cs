using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Client.UserInterface.Systems;
using Content.Shared.DeadSpace.Fishing;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Fishing;

public sealed class LavaFishingUIController : UIController, IOnSystemChanged<LavaFishingSystem>, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [UISystemDependency] private readonly LavaFishingSystem _fishing = default!;
    [UISystemDependency] private readonly HandsSystem _hands = default!;
    [UISystemDependency] private readonly ProgressColorSystem _colors = default!;

    private LavaFishingWindow? _window;
    private EntityUid? _rod;
    private EntityUid? _dismissedFloat;
    private bool _loaded;
    private bool _inGame;

    public void OnSystemLoaded(LavaFishingSystem system)
    {
        _loaded = true;
    }

    public void OnSystemUnloaded(LavaFishingSystem system)
    {
        _loaded = false;
        ResetWindow();
    }

    public void OnStateEntered(GameplayState state)
    {
        _inGame = true;
    }

    public void OnStateExited(GameplayState state)
    {
        _inGame = false;
        ResetWindow();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (!_loaded || !_inGame)
            return;

        if (_players.LocalEntity is not { } player || _hands.GetActiveItem(player) is not { } uid ||
            !EntityManager.TryGetComponent<LavaFishingRodComponent>(uid, out var rod) || rod.Fisher != player ||
            rod.Phase is LavaFishingPhase.Idle or LavaFishingPhase.Caught || rod.Float is not { } bobber)
        {
            _dismissedFloat = null;
            CloseWindow();
            return;
        }

        if (_dismissedFloat == bobber)
            return;

        var firstOpen = _window == null;
        if (_window == null)
        {
            _window = UIManager.CreateWindow<LavaFishingWindow>();
            _window.OnClose += OnWindowClosed;
        }

        _rod = uid;
        var state = _fishing.GetRenderState((uid, rod));
        var zoneWidth = SharedLavaFishingSystem.GetZoneWidth(rod);
        var inZone = MathF.Abs(state.FishPosition - state.ZonePosition) <= zoneWidth / 2;
        _window.UpdateState(rod.Phase, state, zoneWidth, _colors.GetProgressColor(inZone ? 1f : 0.5f),
            _input.GetKeyFunctionButtonString(ContentKeyFunctions.UseItemInHand));
        if (_window.IsOpen)
            return;

        if (firstOpen)
            _window.OpenCenteredRight();
        else
        {
            _window.Open();
            LayoutContainer.SetPosition(_window, Vector2.Clamp(_window.Position, Vector2.Zero,
                Vector2.Max(Vector2.Zero, UIManager.WindowRoot.Size - _window.Size)));
        }
    }

    private void CloseWindow()
    {
        _rod = null;
        _window?.Close();
    }

    private void ResetWindow()
    {
        _dismissedFloat = null;
        CloseWindow();
        if (_window != null)
            _window.OnClose -= OnWindowClosed;
        _window = null;
    }

    private void OnWindowClosed()
    {
        if (_rod is not { } uid || !EntityManager.TryGetComponent<LavaFishingRodComponent>(uid, out var rod))
            return;

        _rod = null;
        _dismissedFloat = rod.Float;
        EntityManager.RaisePredictiveEvent(new InteractInventorySlotEvent(EntityManager.GetNetEntity(uid)));
    }
}

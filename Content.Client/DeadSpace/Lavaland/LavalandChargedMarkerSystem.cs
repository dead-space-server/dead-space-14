// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// The kinetic machete's charged marker on the client: the right button in combat mode starts and releases the charge
/// (predicted, the server spawns the bolt), and a ring by the cursor fills up while it is held.
/// </summary>
public sealed class LavalandChargedMarkerSystem : SharedLavalandChargedMarkerSystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private bool _wasDown;
    private EntityUid? _charging;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new LavalandChargedMarkerOverlay(EntityManager, Timing, _input, _player));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<LavalandChargedMarkerOverlay>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var down = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;
        var pressed = down && !_wasDown;
        var released = !down && _wasDown;
        _wasDown = down;

        if (_player.LocalEntity is not { } user)
            return;

        var held = Hands.GetActiveItem(user);
        var usable = held is { } weapon && HasComp<LavalandChargedMarkerComponent>(weapon) && _combat.IsInCombatMode(user);

        if (pressed && usable)
        {
            _charging = held;
            RaisePredictiveEvent(new LavalandChargedMarkerStartEvent(GetNetEntity(held!.Value)));
            return;
        }

        if (released && _charging is { } charging)
        {
            _charging = null;
            var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
            if (mouse.MapId == MapId.Nullspace)
                return;

            var target = _transform.ToCoordinates(mouse);
            RaisePredictiveEvent(new LavalandChargedMarkerReleaseEvent(GetNetEntity(charging), GetNetCoordinates(target)));
        }
    }
}

/// <summary>A ring by the cursor: how far the machete's mark is charged, red when it is about to blow up.</summary>
public sealed class LavalandChargedMarkerOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IPlayerManager _player;
    private readonly SharedHandsSystem _hands;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private const float Radius = 16f;
    private const int Segments = 48;

    public LavalandChargedMarkerOverlay(IEntityManager entities, IGameTiming timing, IInputManager input, IPlayerManager player)
    {
        _entities = entities;
        _timing = timing;
        _input = input;
        _player = player;
        _hands = entities.System<SharedHandsSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } user ||
            _hands.GetActiveItem(user) is not { } weapon ||
            !_entities.TryGetComponent<LavalandChargedMarkerComponent>(weapon, out var comp) ||
            !comp.Charging)
        {
            return;
        }

        var held = _timing.CurTime - comp.ChargeStart;
        var progress = Math.Clamp((float) (held / comp.OverchargeTime), 0f, 1f);
        var full = (float) (comp.FullChargeTime / comp.OverchargeTime);

        // White while building up, orange from the full charge (the crit is doubled), red as it nears blowing up.
        var color = progress < full
            ? Color.White
            : Color.InterpolateBetween(Color.Orange, Color.Red, (progress - full) / Math.Max(0.001f, 1f - full));

        var center = _input.MouseScreenPosition.Position + new Vector2(22f, -22f);
        var handle = args.ScreenHandle;
        DrawArc(handle, center, 1f, Color.Black.WithAlpha(0.45f));
        DrawArc(handle, center, progress, color);

        // A tick where the full charge is.
        var tick = Angle.FromDegrees(-90 + 360 * full).ToVec();
        handle.DrawLine(center + tick * (Radius - 4), center + tick * (Radius + 4), Color.Orange);
    }

    private static void DrawArc(DrawingHandleScreen handle, Vector2 center, float fraction, Color color)
    {
        var count = Math.Max(1, (int) (Segments * fraction));
        var points = new Vector2[count + 1];
        for (var i = 0; i <= count; i++)
        {
            var angle = Angle.FromDegrees(-90 + 360 * fraction * i / count);
            points[i] = center + angle.ToVec() * Radius;
        }

        for (var width = -1; width <= 1; width++)
        {
            for (var i = 0; i < count; i++)
            {
                var a = points[i] + Vector2.Normalize(points[i] - center) * width;
                var b = points[i + 1] + Vector2.Normalize(points[i + 1] - center) * width;
                handle.DrawLine(a, b, color);
            }
        }
    }
}

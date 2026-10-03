using Content.Shared.Crayon;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client.DeadSpace.Crayon;

public sealed class CrayonGhostSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private SharedHandsSystem _hands = default!;
    private SharedTransformSystem _transform = default!;
    private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        _hands = EntityManager.System<SharedHandsSystem>();
        _transform = EntityManager.System<SharedTransformSystem>();
        _sprite = EntityManager.System<SpriteSystem>();

        _overlay.AddOverlay(new CrayonGhostOverlay(this, _transform, _sprite));
    }

    public void SetRotation(Angle rotation)
    {
        var player = _player.LocalEntity;
        if (player == null) return;
        if (!_hands.TryGetActiveItem(player.Value, out var held)) return;
        if (!TryComp<CrayonComponent>(held, out var crayon)) return;
        rotation = crayon.Rotation;
    }
    
    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CrayonGhostOverlay>();
    }

    public bool GetActiveDecalInfo(out string decalId, out Color color, out Angle rotation)
    {
        decalId = string.Empty;
        color = Color.White;
        rotation = Angle.Zero;

        var player = _player.LocalEntity;
        if (player == null)
            return false;

        if (!_hands.TryGetActiveItem(player.Value, out var held))
            return false;

        if (!TryComp<CrayonComponent>(held.Value, out var crayon))
            return false;

        if (string.IsNullOrEmpty(crayon.SelectedState))
            return false;

        decalId = crayon.SelectedState;
        var tint = crayon.Color;
        color = tint.WithAlpha(Math.Clamp(crayon.Opacity, crayon.MinOpacity, crayon.MaxOpacity));
        rotation = crayon.Rotation;
        return true;
    }
}
// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// What a fighter caught by the green scream sees: living bodies through walls as in thermal sight, marked outsiders
/// picked out in green, and an arrow at the edge of the screen towards each marked one that has run off it.
/// </summary>
public sealed class CardinalHuntOverlay : Overlay
{
    private static readonly Color BodyTint = new(1f, 0.45f, 0.4f, 0.85f);
    private static readonly Color MarkColor = Color.FromHex("#50ff70");

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly EntityLookupSystem _lookup;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly HashSet<Entity<ThermalVisibleComponent>> _bodies = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CardinalHuntOverlay(IEntityManager entities, IPlayerManager player, IGameTiming timing,
        EntityLookupSystem lookup, SpriteSystem sprite, SharedTransformSystem transform)
    {
        _entities = entities;
        _player = player;
        _timing = timing;
        _lookup = lookup;
        _sprite = sprite;
        _transform = transform;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entities.HasComponent<CardinalHuntComponent>(local))
            return false;

        return _entities.TryGetComponent<EyeComponent>(local, out var eye) && args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        DrawBodies(args);
        DrawArrows(args);
    }

    private void DrawBodies(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var local = _player.LocalEntity;
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var pulse = 0.75f + 0.25f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 4f);

        _bodies.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldBounds.Enlarged(1f), _bodies);
        foreach (var (uid, _) in _bodies)
        {
            if (uid == local
                || !_entities.TryGetComponent<SpriteComponent>(uid, out var sprite) || !sprite.Visible
                || !_entities.TryGetComponent<TransformComponent>(uid, out var xform)
                || Carried(xform))
                continue;

            var marked = _entities.HasComponent<CardinalMarkedComponent>(uid);
            var position = _transform.GetWorldPosition(xform);
            if (marked)
                handle.DrawCircle(position, 0.55f, MarkColor.WithAlpha(0.18f * pulse));

            var old = sprite.Color;
            var tint = marked ? MarkColor.WithAlpha(pulse) : BodyTint;
            _sprite.SetColor((uid, sprite), new Color(old.R * tint.R, old.G * tint.G, old.B * tint.B, old.A * tint.A));
            _sprite.RenderSprite((uid, sprite), handle, eyeRot, _transform.GetWorldRotation(xform), position);
            _sprite.SetColor((uid, sprite), old);
        }
    }

    /// <summary>
    /// Bodies inside someone's inventory are drawn with their wearer.
    /// </summary>
    private bool Carried(TransformComponent xform)
    {
        var parent = xform.ParentUid;
        while (parent.IsValid() && !_entities.HasComponent<MapGridComponent>(parent))
        {
            if (_entities.HasComponent<InventoryComponent>(parent))
                return true;

            parent = _entities.GetComponent<TransformComponent>(parent).ParentUid;
        }

        return false;
    }

    /// <summary>
    /// An arrow just inside the edge of the view towards every marked outsider beyond it. Drawn in the world, so it
    /// needs nothing but the view's own bounds; the positions come from the server, however far the marked have run.
    /// </summary>
    private void DrawArrows(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent<CardinalHuntComponent>(local, out var hunt) || hunt.Targets.Count == 0 ||
            !_entities.TryGetComponent<TransformComponent>(local, out var localXform) ||
            _entities.GetEntity(hunt.TargetsMap) != localXform.MapUid)
            return;

        var handle = args.WorldHandle;
        var view = args.WorldBounds;
        var centre = view.Box.Center;
        var half = view.Box.Size / 2f - new Vector2(ArrowMargin);
        if (half.X <= 0f || half.Y <= 0f)
            return;

        var rotation = view.Rotation;
        var pulse = 0.7f + 0.3f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 5f);
        foreach (var target in hunt.Targets)
        {
            var offset = (-rotation).RotateVec(target - centre);
            if (MathF.Abs(offset.X) <= half.X + ArrowMargin && MathF.Abs(offset.Y) <= half.Y + ArrowMargin)
                continue;

            var scale = MathF.Min(
                offset.X != 0f ? half.X / MathF.Abs(offset.X) : float.MaxValue,
                offset.Y != 0f ? half.Y / MathF.Abs(offset.Y) : float.MaxValue);
            var tip = centre + rotation.RotateVec(offset * scale);
            var dir = Vector2.Normalize(target - centre);
            DrawArrow(handle, tip, dir, MarkColor.WithAlpha(pulse));
        }
    }

    private const float ArrowMargin = 0.9f;

    // Reused buffers: stackalloc does not pass the client sandbox's IL verification.
    private readonly Vector2[] _head = new Vector2[3];
    private readonly Vector2[] _outline = new Vector2[3];

    private void DrawArrow(DrawingHandleWorld handle, Vector2 tip, Vector2 dir, Color color)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var back = tip - dir * 0.75f;
        _head[0] = tip;
        _head[1] = back + side * 0.42f;
        _head[2] = back - side * 0.42f;
        _outline[0] = tip + dir * 0.1f;
        _outline[1] = back + side * 0.52f - dir * 0.07f;
        _outline[2] = back - side * 0.52f - dir * 0.07f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _outline, Color.Black.WithAlpha(color.A * 0.6f));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _head, color);
    }
}

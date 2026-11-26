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

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

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
        if (args.Space == OverlaySpace.ScreenSpace)
            DrawArrows(args);
        else
            DrawBodies(args);
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

    private void DrawArrows(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        UIBox2 bounds = args.ViewportBounds;
        var margin = 36f;
        var inner = new UIBox2(bounds.Left + margin, bounds.Top + margin, bounds.Right - margin, bounds.Bottom - margin);
        if (inner.Width <= 0f || inner.Height <= 0f)
            return;

        var centre = inner.Center;
        var pulse = 0.7f + 0.3f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 5f);
        var query = _entities.EntityQueryEnumerator<CardinalMarkedComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var screen = args.ViewportControl!.WorldToScreen(_transform.GetWorldPosition(xform));
            if (bounds.Contains(screen))
                continue;

            var dir = screen - centre;
            if (dir.LengthSquared() < 1f)
                continue;

            dir = Vector2.Normalize(dir);
            // Where the line from the centre towards the target leaves the inner box.
            var sx = dir.X != 0f ? (dir.X > 0 ? inner.Right - centre.X : inner.Left - centre.X) / dir.X : float.MaxValue;
            var sy = dir.Y != 0f ? (dir.Y > 0 ? inner.Bottom - centre.Y : inner.Top - centre.Y) / dir.Y : float.MaxValue;
            var tip = centre + dir * MathF.Min(sx, sy);
            DrawArrow(handle, tip, dir, MarkColor.WithAlpha(pulse));
        }
    }

    private static void DrawArrow(DrawingHandleScreen handle, Vector2 tip, Vector2 dir, Color color)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var back = tip - dir * 22f;
        Span<Vector2> head = stackalloc Vector2[]
        {
            tip,
            back + side * 13f,
            back - side * 13f,
        };
        Span<Vector2> outline = stackalloc Vector2[]
        {
            tip + dir * 3f,
            back + side * 16f - dir * 2f,
            back - side * 16f - dir * 2f,
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, outline, Color.Black.WithAlpha(color.A * 0.6f));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, head, color);
    }
}

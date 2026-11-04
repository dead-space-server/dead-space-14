// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.DeviceLinkVis;

public sealed class DeviceLinkVisOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly DeviceLinkVisSystem _vis;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly HashSet<NetEntity> _drawnSources = new();

    public DeviceLinkVisOverlay(DeviceLinkVisSystem vis, IEntityManager entities)
    {
        _vis = vis;
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _sprite = entities.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var links = _vis.Links;
        if (links.Count == 0)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        _drawnSources.Clear();

        foreach (var link in links)
        {
            if (!TryPosition(link.Source, link.SourcePos, args.MapId, out var sourcePos) ||
                !TryPosition(link.Sink, link.SinkPos, args.MapId, out var sinkPos))
            {
                continue;
            }

            var span = new Box2(Vector2.Min(sourcePos, sinkPos), Vector2.Max(sourcePos, sinkPos)).Enlarged(2f);
            if (!args.WorldAABB.Intersects(span))
                continue;

            var color = ColorFor(link.Source);
            var sourceBox = BoxFor(link.Source, sourcePos, args);
            var sinkBox = BoxFor(link.Sink, sinkPos, args);
            handle.DrawLine(sourceBox.Center, sinkBox.Center, color);
            handle.DrawRect(sinkBox, color, false);
            if (_drawnSources.Add(link.Source))
                handle.DrawRect(sourceBox, color, false);
        }
    }

    private bool TryPosition(NetEntity net, NetCoordinates fallback, MapId map, out Vector2 world)
    {
        world = default;
        if (_entities.TryGetEntity(net, out var uid) &&
            _entities.TryGetComponent(uid.Value, out TransformComponent? xform) &&
            xform.MapID == map)
        {
            world = _transform.GetWorldPosition(uid.Value);
            return true;
        }

        var coords = _entities.GetCoordinates(fallback);
        if (!coords.IsValid(_entities))
            return false;

        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId != map)
            return false;

        world = mapCoords.Position;
        return true;
    }

    private Box2Rotated BoxFor(NetEntity net, Vector2 pos, in OverlayDrawArgs args)
    {
        if (!_entities.TryGetEntity(net, out var uid))
            return Fallback(pos);

        var entity = uid.Value;
        if (!_entities.TryGetComponent(entity, out SpriteComponent? sprite) || !sprite.Visible)
            return Fallback(pos);

        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(entity);
        var bounds = _sprite.CalculateBounds((entity, sprite), worldPos, worldRot, args.Viewport.Eye?.Rotation ?? default);
        if (bounds.Box.Width < 0.05f || bounds.Box.Height < 0.05f)
            return Fallback(pos);

        return new Box2Rotated(bounds.Box.Enlarged(1f / 32f), bounds.Rotation, bounds.Origin);
    }

    private static Box2Rotated Fallback(Vector2 pos)
    {
        return new Box2Rotated(Box2.CenteredAround(pos, new Vector2(0.75f, 0.75f)), Angle.Zero, pos);
    }

    private static Color ColorFor(NetEntity source)
    {
        var hue = source.Id * 0.61803398875f % 1f;
        if (hue < 0f)
            hue += 1f;

        return Color.FromHsv(new Vector4(hue, 0.9f, 1f, 1f));
    }
}

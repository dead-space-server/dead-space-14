// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.RCD;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.RCD;

public sealed class RcdBuildSiteOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly RcdBuildSiteSystem _sites;
    private readonly SharedHandsSystem _hands;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public RcdBuildSiteOverlay(IEntityManager entities)
    {
        _entities = entities;
        _sites = entities.System<RcdBuildSiteSystem>();
        _hands = entities.System<SharedHandsSystem>();
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<RcdBuildSiteComponent, TransformComponent>();
        while (query.MoveNext(out _, out var site, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var width = site.Path.Width;
            var height = site.Path.Height;
            if (width < 0.01f || height < 0.01f)
                continue;

            var (origin, rotation) = _transform.GetWorldPositionRotation(xform);
            if (!args.WorldBounds.Contains(origin))
                continue;

            Vector2? hand = null;
            if (site.Active && site.User.IsValid())
            {
                var user = _entities.GetEntity(site.User);
                if (_entities.EntityExists(user))
                    hand = HandOrigin(user);
            }

            DrawSite(args.WorldHandle, origin, rotation, site, _sites.ProgressOf(site), hand);
        }
    }

    private Vector2 HandOrigin(EntityUid user)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(user);
        var location = HandLocation.Right;
        if (_hands.TryGetHand(user, _hands.GetActiveHand(user), out var hand))
            location = hand.Value.Location;

        return pos + HandOffset(rot.GetCardinalDir(), location);
    }

    private static Vector2 HandOffset(Direction direction, HandLocation location)
    {
        var right = location != HandLocation.Left;
        return direction switch
        {
            Direction.North => right ? new Vector2(0.25f, -0.17f) : new Vector2(-0.31f, -0.17f),
            Direction.East => right ? new Vector2(0.05f, -0.18f) : new Vector2(0.21f, -0.15f),
            Direction.West => right ? new Vector2(-0.24f, -0.15f) : new Vector2(-0.08f, -0.18f),
            _ => right ? new Vector2(-0.31f, -0.17f) : new Vector2(0.25f, -0.17f),
        };
    }

    private static void DrawSite(
        DrawingHandleWorld handle,
        Vector2 origin,
        Angle rotation,
        RcdBuildSiteComponent site,
        float progress,
        Vector2? hand)
    {
        var scanY = site.Downward
            ? site.Path.Top + (site.Path.Bottom - site.Path.Top) * progress
            : site.Path.Bottom + (site.Path.Top - site.Path.Bottom) * progress;
        var edgeY = site.Downward ? site.Path.Top : site.Path.Bottom;
        var edgeLeft = World(origin, rotation, site.Path.Left, edgeY);
        var edgeRight = World(origin, rotation, site.Path.Right, edgeY);
        var scanLeft = World(origin, rotation, site.Path.Left, scanY);
        var scanRight = World(origin, rotation, site.Path.Right, scanY);

        var red = site.Downward;
        var wash = red ? new Color(1f, 0.25f, 0.18f, 0.24f) : new Color(0.35f, 0.85f, 1f, 0.22f);
        var line = red ? new Color(1f, 0.55f, 0.42f, 0.95f) : new Color(0.75f, 1f, 1f, 0.9f);
        var beam = red ? new Color(1f, 0.22f, 0.16f, 0.34f) : new Color(0.4f, 0.88f, 1f, 0.28f);
        var core = red ? new Color(1f, 0.78f, 0.7f, 0.78f) : new Color(0.85f, 1f, 1f, 0.72f);

        if (progress > 0.001f)
        {
            var covered = new Vector2[]
            {
                edgeLeft,
                edgeRight,
                scanRight,
                scanLeft,
            };
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, covered, wash);
        }

        var edge = new Vector2[] { scanLeft, scanRight };
        handle.DrawPrimitives(DrawPrimitiveTopology.LineList, edge, line);

        if (hand == null)
            return;

        var cone = new Vector2[]
        {
            hand.Value,
            scanLeft,
            scanRight,
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, cone, beam);

        var mid = (scanLeft + scanRight) * 0.5f;
        var coreHalf = (scanRight - scanLeft) * 0.12f;
        var coreVerts = new Vector2[]
        {
            hand.Value,
            mid - coreHalf,
            mid + coreHalf,
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, coreVerts, core);
    }

    private static Vector2 World(Vector2 origin, Angle rotation, float x, float y)
    {
        return origin + rotation.RotateVec(new Vector2(x, y));
    }
}

using System.Numerics;
using Content.Shared.DeadSpace.Smokables;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Smokables;

/// <summary>
/// Draws the cosmetic shisha hose without a physical joint.
/// </summary>
public sealed class ShishaHoseOverlay : Overlay
{
    private const int CurveSegments = 16;
    // Content assemblies must use verifiable IL. Reuse managed storage instead of stackalloc.
    private readonly DrawVertexUV2D[] _curveVertices = new DrawVertexUV2D[(CurveSegments + 1) * 2];

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private IEntityManager _entManager;

    public ShishaHoseOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        var spriteSystem = _entManager.System<SpriteSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var joints = _entManager.EntityQueryEnumerator<ShishaHoseVisualsComponent, TransformComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);

        while (joints.MoveNext(out var visuals, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var other = visuals.Target;

            if (!xformQuery.TryGetComponent(other, out var otherXform))
                continue;

            if (xform.MapID != otherXform.MapID)
                continue;

            var texture = spriteSystem.Frame0(visuals.Sprite);
            var width = texture.Width / (float)EyeManager.PixelsPerMeter;

            var coordsA = xform.Coordinates;
            var coordsB = otherXform.Coordinates;

            var rotA = xform.LocalRotation;
            var rotB = otherXform.LocalRotation;

            coordsA = coordsA.Offset(rotA.RotateVec(visuals.OffsetA));
            coordsB = coordsB.Offset(rotB.RotateVec(visuals.OffsetB));

            var posA = xformSystem.ToMapCoordinates(coordsA).Position;
            var posB = xformSystem.ToMapCoordinates(coordsB).Position;
            var diff = posB - posA;
            var length = diff.Length();

            if (visuals.Sag > 0f)
            {
                DrawCurved(worldHandle, texture, posA, posB, width, visuals.Sag);
                continue;
            }

            var midPoint = diff / 2f + posA;
            var angle = (posB - posA).ToWorldAngle();
            var box = new Box2(-width / 2f, -length / 2f, width / 2f, length / 2f);
            var rotate = new Box2Rotated(box.Translated(midPoint), angle, midPoint);

            worldHandle.DrawTextureRect(texture, rotate);
        }
    }

    private void DrawCurved(DrawingHandleWorld handle, Texture texture, Vector2 start, Vector2 end,
        float width, float sag)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length < 0.001f)
            return;

        // RSI frames are atlas regions; primitive drawing requires the underlying texture and its UVs.
        var textureSize = new Vector2(texture.Width, texture.Height);
        var textureOrigin = Vector2.Zero;
        while (texture is AtlasTexture atlas)
        {
            textureOrigin += atlas.SubRegion.TopLeft;
            texture = atlas.SourceTexture;
        }
        var sourceSize = new Vector2(texture.Width, texture.Height);

        // A bounded textured strip avoids per-segment draw calls and allocations.
        var vertices = _curveVertices;
        var drop = MathF.Min(sag, length * 0.75f);
        for (var i = 0; i <= CurveSegments; i++)
        {
            var t = i / (float) CurveSegments;
            var point = Vector2.Lerp(start, end, t) - new Vector2(0, 4f * drop * t * (1f - t));
            var tangent = delta - new Vector2(0, 4f * drop * (1f - 2f * t));
            if (tangent.LengthSquared() < 0.000001f)
                tangent = delta;
            var normal = Vector2.Normalize(new Vector2(-tangent.Y, tangent.X)) * (width / 2f);
            var leftUv = (textureOrigin + new Vector2(0, textureSize.Y * t)) / sourceSize;
            var rightUv = (textureOrigin + new Vector2(textureSize.X, textureSize.Y * t)) / sourceSize;
            leftUv.Y = 1f - leftUv.Y;
            rightUv.Y = 1f - rightUv.Y;
            vertices[i * 2] = new DrawVertexUV2D(point - normal, leftUv);
            vertices[i * 2 + 1] = new DrawVertexUV2D(point + normal, rightUv);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, texture, vertices);
    }
}

using System.Numerics;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Crayon;

public sealed class CrayonGhostOverlay : Overlay
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    private readonly CrayonGhostSystem _crayonSystem;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public CrayonGhostOverlay(CrayonGhostSystem crayonSystem, SharedTransformSystem transform, SpriteSystem sprite)
    {
        IoCManager.InjectDependencies(this);
        _crayonSystem = crayonSystem;
        _transform = transform;
        _sprite = sprite;
        ZIndex = 999;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _crayonSystem.GetActiveDecalInfo(out var decalId, out var color, out var rotation);

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var mousePos = _eyeManager.PixelToMap(mouseScreenPos);

        if (mousePos.MapId != args.MapId)
            return;

        if (!_mapManager.TryFindGridAt(mousePos, out var gridUid, out var grid))
            return;

        var worldMatrix = _transform.GetWorldMatrix(gridUid);
        var invMatrix = _transform.GetInvWorldMatrix(gridUid);

        var handle = args.WorldHandle;
        handle.SetTransform(worldMatrix);

        var localPos = Vector2.Transform(mousePos.Position, invMatrix);

        var aabb = Box2.UnitCentered.Translated(localPos);
        var box = new Box2Rotated(aabb, rotation, localPos);

        if (!string.IsNullOrEmpty(decalId) && _protoMan.TryIndex<DecalPrototype>(decalId, out var decalProto))
        {
            handle.DrawTextureRect(_sprite.Frame0(decalProto.Sprite), box, color);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
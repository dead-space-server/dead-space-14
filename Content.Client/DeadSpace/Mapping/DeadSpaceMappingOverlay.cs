using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using static Content.Client.DeadSpace.Mapping.DeadSpaceMappingState;

namespace Content.Client.DeadSpace.Mapping;

public sealed class DeadSpaceMappingOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly SpriteSystem _sprite;

    // 1 off in case something else uses these colors since we use them to compare
    private static readonly Color PickColor = new(1, 255, 0);
    private static readonly Color DeleteColor = new(255, 1, 0);

    private readonly Dictionary<EntityUid, Color> _oldColors = new();

    private readonly DeadSpaceMappingState _state;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public DeadSpaceMappingOverlay(DeadSpaceMappingState state)
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entities.System<SpriteSystem>();

        _state = state;
        _shader = _prototypes.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (id, color) in _oldColors)
        {
            if (!_entities.TryGetComponent(id, out SpriteComponent? sprite))
                continue;

            if (sprite.Color == DeleteColor || sprite.Color == PickColor)
                _sprite.SetColor((id, sprite), color);
        }

        _oldColors.Clear();

        var handle = args.WorldHandle;
        handle.UseShader(_shader);

        switch (_state.Meta.State)
        {
            case CursorState.Tile:
            {
                if (_state.GetHoveredTileBox2() is { } box)
                    args.WorldHandle.DrawRect(box, _state.Meta.Color);

                break;
            }
            case CursorState.Entity:
            {
                if (_state.GetHoveredEntity() is { } entity &&
                    _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                {
                    _oldColors[entity] = sprite.Color;
                    _sprite.SetColor((entity, sprite), _state.Meta.Color);
                }

                break;
            }
            case CursorState.EntityOrTile:
            {
                if (_state.GetHoveredEntity() is { } entity &&
                    _entities.TryGetComponent(entity, out SpriteComponent? sprite))
                {
                    _oldColors[entity] = sprite.Color;
                    _sprite.SetColor((entity, sprite), _state.Meta.Color);
                }
                else if (_state.GetHoveredTileBox2() is { } box)
                {
                    args.WorldHandle.DrawRect(box, _state.Meta.SecondColor ?? _state.Meta.Color);
                }

                break;
            }
        }

        handle.UseShader(null);
    }
}

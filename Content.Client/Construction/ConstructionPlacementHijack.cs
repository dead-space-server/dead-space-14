using System.Linq;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
// DS14-start
using Content.Client.DeadSpace.AdminToy;
using Content.Shared.DeadSpace.AdminToy;
using Robust.Client.Player;
// DS14-end

namespace Content.Client.Construction;

public sealed partial class ConstructionPlacementHijack : PlacementHijack
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    private readonly ConstructionSystem _constructionSystem;
    private readonly SpriteSystem _spriteSystem;

    private readonly ConstructionPrototype? _prototype;

    public ConstructionPrototype? CurrentPrototype => _prototype;

    public override bool CanRotate { get; }

    public ConstructionPlacementHijack(ConstructionPrototype? prototype)
    {
        IoCManager.InjectDependencies(this);

        _constructionSystem = _entMan.System<ConstructionSystem>();
        _spriteSystem = _entMan.System<SpriteSystem>();
        _prototype = prototype;
        CanRotate = prototype?.CanRotate ?? true;
    }

    /// <inheritdoc />
    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        if (_prototype != null)
        {
            var dir = Manager.Direction;
            // DS14-start
            if (TryGetAdminToySystem(out var adminToy))
                adminToy.PlaceConstructionGhost(_prototype, coordinates, dir.ToAngle());
            else
            // DS14-end
                _constructionSystem.SpawnGhost(_prototype, coordinates, dir);
        }
        return true;
    }

    /// <inheritdoc />
    public override bool HijackDeletion(EntityUid entity)
    {
        // DS14-start
        if (_entMan.TryGetComponent<ConstructionGhostComponent>(entity, out var ghost))
        {
            if (TryGetAdminToySystem(out var adminToy))
                adminToy.ClearConstructionGhost(ghost.GhostId);
            else
            // DS14-end
                _constructionSystem.ClearGhost(entity.GetHashCode());
        }
        return true;
    }

    /// <inheritdoc />
    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);

        if (_prototype is null || !_constructionSystem.TryGetRecipePrototype(_prototype.ID, out var targetProtoId))
            return;

        if (!_protoMan.TryIndex(targetProtoId, out var proto))
            return;

        manager.CurrentTextures = _spriteSystem.GetPrototypeTextures(proto).ToList();
    }

    // DS14-start
    private static bool TryGetAdminToySystem(out AdminToySystem adminToy)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        adminToy = entityManager.System<AdminToySystem>();

        return IoCManager.Resolve<IPlayerManager>().LocalEntity is { } localEntity &&
               entityManager.HasComponent<AdminToyComponent>(localEntity);
    }
    // DS14-end
}

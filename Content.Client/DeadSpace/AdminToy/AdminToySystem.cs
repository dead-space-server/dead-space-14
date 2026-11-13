// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Client.Construction;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.DeadSpace.AdminToy;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.AdminToy;

public sealed partial class AdminToySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private ConstructionSystem _construction = default!;

    private readonly Dictionary<int, EntityUid> _constructionGhosts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminToyComponent, ComponentStartup>(OnToyStartup);
        SubscribeLocalEvent<AdminToyComponent, AfterAutoHandleStateEvent>(OnToyState);
        SubscribeNetworkEvent<AdminToyConstructionGhostCreateEvent>(OnCreateConstructionGhost);
        SubscribeNetworkEvent<AdminToyClearConstructionGhostsEvent>(OnClearConstructionGhosts);
        SubscribeNetworkEvent<AckStructureConstructionMessage>(OnAckStructure);
    }

    public void PlaceConstructionGhost(ConstructionPrototype prototype, EntityCoordinates coordinates, Angle angle)
    {
        RaiseNetworkEvent(new AdminToyPlaceConstructionGhostRequest(
            GetNetCoordinates(coordinates),
            prototype.ID,
            angle));
    }

    public void ClearConstructionGhost(int ghostId)
    {
        RaiseNetworkEvent(new AdminToyClearConstructionGhostRequest(ghostId));
    }

    public void ClearAllConstructionGhosts()
    {
        RaiseNetworkEvent(new AdminToyClearAllConstructionGhostsRequest());
    }

    private void OnToyStartup(Entity<AdminToyComponent> ent, ref ComponentStartup args)
    {
        ApplyToyVisuals(ent);
    }

    private void OnToyState(Entity<AdminToyComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyToyVisuals(ent);
    }

    private void ApplyToyVisuals(Entity<AdminToyComponent> ent)
    {
        var toyPrototype = ent.Comp.ToyPrototype;
        if (string.IsNullOrEmpty(toyPrototype) ||
            ent.Comp.AppliedToyPrototype == toyPrototype ||
            !_prototype.TryIndex(toyPrototype, out EntityPrototype? prototype) ||
            !prototype.Components.ContainsKey("Sprite"))
        {
            return;
        }

        var dummy = EntityManager.SpawnEntity(toyPrototype, MapCoordinates.Nullspace);
        var dummySprite = EnsureComp<SpriteComponent>(dummy);
        EntityManager.System<AppearanceSystem>().OnChangeData(dummy, dummySprite);

        var sprite = EnsureComp<SpriteComponent>(ent.Owner);
        _sprite.CopySprite((dummy, dummySprite), (ent.Owner, sprite));

        Del(dummy);
        ent.Comp.AppliedToyPrototype = toyPrototype;
    }

    private void OnCreateConstructionGhost(AdminToyConstructionGhostCreateEvent ev)
    {
        if (!_prototype.TryIndex<ConstructionPrototype>(ev.Prototype, out var construction) ||
            !_construction.TryGetRecipePrototype(construction.ID, out var targetProtoId) ||
            !_prototype.TryIndex(targetProtoId, out EntityPrototype? targetProto))
        {
            return;
        }

        if (_constructionGhosts.Remove(ev.GhostId, out var oldGhost))
            QueueDel(oldGhost);

        var coordinates = GetCoordinates(ev.Coordinates);
        var ghost = Spawn("constructionghost", coordinates);
        var ghostComponent = Comp<ConstructionGhostComponent>(ghost);
        ghostComponent.Prototype = construction;
        ghostComponent.GhostId = ev.GhostId;
        Comp<TransformComponent>(ghost).LocalRotation = ev.Angle;
        _constructionGhosts[ev.GhostId] = ghost;

        var sprite = Comp<SpriteComponent>(ghost);

        if (targetProto.TryGetComponent(out IconComponent? icon, EntityManager.ComponentFactory))
        {
            _sprite.AddBlankLayer((ghost, sprite), 0);
            _sprite.LayerSetSprite((ghost, sprite), 0, icon.Icon);
            sprite.LayerSetShader(0, "unshaded");
            _sprite.LayerSetVisible((ghost, sprite), 0, true);
        }
        else if (targetProto.Components.ContainsKey("Sprite"))
        {
            var dummy = EntityManager.SpawnEntity(targetProtoId, MapCoordinates.Nullspace);
            var targetSprite = EnsureComp<SpriteComponent>(dummy);
            EntityManager.System<AppearanceSystem>().OnChangeData(dummy, targetSprite);

            _sprite.CopySprite((dummy, targetSprite), (ghost, sprite));

            for (var i = 0; i < sprite.AllLayers.Count(); i++)
            {
                sprite.LayerSetShader(i, "unshaded");
            }

            Del(dummy);
        }
        else
        {
            QueueDel(ghost);
            _constructionGhosts.Remove(ev.GhostId);
            return;
        }

        _sprite.SetColor((ghost, sprite), new Color(48, 255, 48, 128));

        if (construction.CanBuildInImpassable)
            EnsureComp<WallMountComponent>(ghost).Arc = new(Math.Tau);
    }


    private void OnAckStructure(AckStructureConstructionMessage msg)
    {
        // ConstructionSystem sends ConstructionGhostComponent.GhostId.
        // For admin toy ghosts this is the same id as the server-side AdminToy ghost id.
        if (!ClearLocalConstructionGhost(msg.GhostId))
            return;

        // Keep the server-side AdminToyComponent.ConstructionGhosts list in sync and
        // make the server broadcast removal to the other private viewer as well.
        RaiseNetworkEvent(new AdminToyClearConstructionGhostRequest(msg.GhostId));
    }

    private bool ClearLocalConstructionGhost(int ghostId)
    {
        if (!_constructionGhosts.Remove(ghostId, out var ghost))
            return false;

        QueueDel(ghost);
        return true;
    }

    private void OnClearConstructionGhosts(AdminToyClearConstructionGhostsEvent ev)
    {
        foreach (var ghostId in ev.GhostIds)
        {
            ClearLocalConstructionGhost(ghostId);
        }
    }
}

using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Movement.Systems;

/// <summary>
/// Controls the switching of motion and standing still animation
/// </summary>
public sealed class ClientSpriteMovementSystem : SharedSpriteMovementSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    // DS14-start
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    // DS14-end

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<SpriteMovementComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        // DS14-start
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<SpriteMovementComponent, MobStateChangedEvent>(OnMobStateChanged);
        // DS14-end
    }

    private void OnAfterAutoHandleState(Entity<SpriteMovementComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_spriteQuery.TryGetComponent(ent, out var sprite))
            return;

        // DS14-start
        // Keep the incapacitated body sprite when the last movement update arrives after a mob-state change.
        if (ent.Comp.EquipmentAnimationLayer != null && _mobState.IsIncapacitated(ent.Owner))
        {
            UpdateEquipment(ent);
            return;
        }
        // DS14-end

        if (ent.Comp.IsMoving)
        {
            foreach (var (layer, state) in ent.Comp.MovementLayers)
            {
                _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
        else
        {
            foreach (var (layer, state) in ent.Comp.NoMovementLayers)
            {
                _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }

        UpdateEquipment(ent); // DS14
    }

    // DS14-start
    private void OnEquipmentVisualsUpdated(EquipmentVisualsUpdatedEvent args)
    {
        if (TryComp<SpriteMovementComponent>(args.Equipee, out var movement))
            UpdateEquipment((args.Equipee, movement));
    }

    private void OnMobStateChanged(Entity<SpriteMovementComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateEquipment(ent);
    }

    private void UpdateEquipment(Entity<SpriteMovementComponent> ent)
    {
        if (ent.Comp.EquipmentAnimationLayer is not { } bodyKey ||
            !TryComp<SpriteComponent>(ent, out var sprite) ||
            !TryComp<InventorySlotsComponent>(ent, out var slots) ||
            !_sprite.TryGetLayer((ent.Owner, sprite), bodyKey, out var body, false))
            return;

        var alive = !_mobState.IsIncapacitated(ent.Owner);
        var suffix = alive && ent.Comp.IsMoving ? "walk" : "idle";
        foreach (var (slot, keys) in slots.VisualLayerKeys)
        {
            if (!_inventory.TryGetSlotEntity(ent.Owner, slot, out var item) ||
                !TryComp<ClothingComponent>(item, out var clothing))
                continue;

            foreach (var key in keys)
            {
                if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var layer, false) ||
                    layer.State.Name is not { } state ||
                    !(state.EndsWith("idle", StringComparison.Ordinal) || state.EndsWith("walk", StringComparison.Ordinal)))
                    continue;

                var equipped = state.IndexOf("equipped-", StringComparison.Ordinal);
                if (equipped < 0)
                    continue;

                var prefix = string.IsNullOrEmpty(clothing.EquippedPrefix) ? "" : clothing.EquippedPrefix + "-";
                var nextState = prefix + state[equipped..^4] + suffix;
                var rsi = layer.RSI ?? sprite.BaseRSI;
                var hasState = rsi != null && rsi.TryGetState(nextState, out _);
                _sprite.LayerSetVisible((ent.Owner, sprite), key, hasState);
                if (!hasState)
                    continue;

                _sprite.LayerSetRsiState(layer, nextState);
                _sprite.LayerSetAutoAnimated(layer, alive);
                _sprite.LayerSetAnimationTime(layer, alive ? body.AnimationTime : 0f);
            }
        }
    }
    // DS14-end
}

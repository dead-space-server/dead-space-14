using Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Armutant.Abilities.Shield;

public sealed class CreateArmorShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public EntProtoId ArmorPrototype = "ClothingOuterArmorArmutant";
    public EntProtoId ArmorHelmetPrototype = "ClothingHeadHelmetArmutant";

    public override void Initialize()
    {
        SubscribeLocalEvent<ShieldCreateArmorComponent, CreateArmorShieldToggleEvent>(OnCreateArmorShield);
        SubscribeLocalEvent<ShieldCreateArmorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShieldCreateArmorComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, ShieldCreateArmorComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionShieldArmorEntity, component.ActionToggleShieldArmorSpawn, uid);
    }

    private void OnComponentShutdown(EntityUid uid, ShieldCreateArmorComponent component, ComponentShutdown args)
    {
        RemoveAllArmor(uid, component);
        _action.RemoveAction(uid, component.ActionShieldArmorEntity);
    }

    private void OnCreateArmorShield(EntityUid uid, ShieldCreateArmorComponent component, ref CreateArmorShieldToggleEvent args)
    {
        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }

        if (component.Equipment.Count > 0)
        {
            RemoveAllArmor(uid, component);
            _popup.PopupEntity(Loc.GetString("armutant-armor-removed"), uid, uid);
        }
        else
        {
            var success = TryEquipArmor(uid, ArmorPrototype, component, "outerClothing") &
                           TryEquipArmor(uid, ArmorHelmetPrototype, component, "head");

            if (!success)
            {
                _popup.PopupEntity(Loc.GetString("armutant-equip-armor-fail"), uid, uid);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("armutant-armor-equipped"), uid, uid);
            }
        }

        args.Handled = true;
    }

    private bool TryEquipArmor(EntityUid uid, EntProtoId proto, ShieldCreateArmorComponent component, string slot)
    {
        if (component.Equipment.ContainsKey(proto.Id))
            return false;

        var item = Spawn(proto, Transform(uid).Coordinates);

        if (!_inventory.TryEquip(uid, item, slot, force: true))
        {
            QueueDel(item);
            return false;
        }

        component.Equipment.Add(proto.Id, item);
        return true;
    }

    private void RemoveAllArmor(EntityUid uid, ShieldCreateArmorComponent component)
    {
        foreach (var item in component.Equipment.Values)
        {
            if (EntityManager.EntityExists(item))
            {
                if (_inventory.TryGetSlotContainer(uid, item.ToString(), out var containerSlot, out var slotDefinition))
                {
                    _inventory.TryUnequip(uid, slotDefinition.Name);
                }
                QueueDel(item);
            }
        }

        component.Equipment.Clear();
    }
}

using Content.Shared.DeadSpace.Abductor;
using Content.Shared.DeadSpace.ItemSwitch.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth.Components;

namespace Content.Server.DeadSpace.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly ClothingSystem _clothing = default!;

    public void InitializeVest()
    {
        SubscribeLocalEvent<AbductorVestComponent, AfterInteractEvent>(OnVestInteract);
        SubscribeLocalEvent<AbductorVestComponent, ItemSwitchedEvent>(OnItemSwitch);
        SubscribeLocalEvent<AbductorVestComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<AbductorVestComponent, GotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<AbductorVestComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Equipee != null && !HasComp<StealthComponent>(args.Equipee) && ent.Comp.CurrentState != AbductorArmorModeType.Combat)
        {
            AddComp<StealthComponent>(args.Equipee);
            AddComp<StealthOnMoveComponent>(args.Equipee);
        }
    }

    private void OnUnequipped(Entity<AbductorVestComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Equipee != null && HasComp<StealthComponent>(args.Equipee))
        {
            RemComp<StealthComponent>(args.Equipee);
            RemComp<StealthOnMoveComponent>(args.Equipee);
        }
    }

    private void OnItemSwitch(Entity<AbductorVestComponent> ent, ref ItemSwitchedEvent args)
    {
        if (Enum.TryParse<AbductorArmorModeType>(args.State, ignoreCase: true, out var state))
            ent.Comp.CurrentState = state;

        var user = Transform(ent.Owner).ParentUid;

        if (state == AbductorArmorModeType.Combat)
        {
            if (TryComp<ClothingComponent>(ent.Owner, out var clothingComponent))
                _clothing.SetEquippedPrefix(ent.Owner, "combat", clothingComponent);

            if (HasComp<MobStateComponent>(user) && HasComp<StealthComponent>(user))
            {
                RemComp<StealthComponent>(user);
                RemComp<StealthOnMoveComponent>(user);
            }
        }
        else
        {
            if (TryComp<ClothingComponent>(ent.Owner, out var clothingComponent))
                _clothing.SetEquippedPrefix(ent.Owner, null, clothingComponent);

            if (HasComp<MobStateComponent>(user) && !HasComp<StealthComponent>(user))
            {
                AddComp<StealthComponent>(user);
                AddComp<StealthOnMoveComponent>(user);
            }
        }
    }

    private void OnVestInteract(Entity<AbductorVestComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInteract(args.User, args.Target)) return;
        if (!args.Target.HasValue) return;

        if (TryComp<AbductorConsoleComponent>(args.Target, out var console))
        {
            console.Armor = GetNetEntity(ent.Owner);
            _popup.PopupEntity(Loc.GetString("abductors-ui-vest-linked"), args.User);
            return;
        }
    }
}
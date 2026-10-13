using Content.Shared.Clothing.Components;
using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Provides the toggle action and handles examining and unequipping.
/// </summary>
public abstract class SharedNinjaGlovesSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    //DS14-start
    [Dependency] private readonly InventorySystem _inventory = default!;
    //DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGlovesComponent, ToggleClothingCheckEvent>(OnToggleCheck);
        SubscribeLocalEvent<NinjaGlovesComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<NinjaGlovesComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<NinjaGlovesComponent, ExaminedEvent>(OnExamined);
        //DS14-start
        SubscribeLocalEvent<SpiderOSComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
        //DS14-end
    }

    /// <summary>
    /// Disable glove abilities and show the popup if they were enabled previously.
    /// </summary>
    private void DisableGloves(Entity<NinjaGlovesComponent> ent)
    {
        var (uid, comp) = ent;

        // already disabled?
        if (comp.User is not { } user)
            return;

        comp.User = null;
        Dirty(uid, comp);

        foreach (var ability in comp.Abilities)
        {
            EntityManager.RemoveComponents(user, ability.Components);
        }
    }

    /// <summary>
    /// Adds the toggle action when equipped by a ninja only.
    /// </summary>
    private void OnToggleCheck(Entity<NinjaGlovesComponent> ent, ref ToggleClothingCheckEvent args)
    {
        if (!_ninja.IsNinja(args.User))
            args.Cancelled = true;
    }

    /// <summary>
    /// Show if the gloves are enabled when examining.
    /// </summary>
    private void OnExamined(Entity<NinjaGlovesComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var on = _toggle.IsActivated(ent.Owner) ? "on" : "off";
        args.PushText(Loc.GetString($"ninja-gloves-examine-{on}"));
    }

    private void OnActivateAttempt(Entity<NinjaGlovesComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.User is not { } user
            || !_ninja.NinjaQuery.TryComp(user, out var ninja)
            // need to wear suit to enable gloves
            || !HasComp<NinjaSuitComponent>(ninja.Suit))
        {
            args.Cancelled = true;
            args.Popup = Loc.GetString("ninja-gloves-not-wearing-suit");
            return;
        }

        //DS14-start
        // gloves only work while the Spider OS is running
        if (!TryComp<SpiderOSComponent>(ninja.Suit, out var os) || !os.SuitActivated)
        {
            args.Cancelled = true;
            args.Popup = Loc.GetString("ninja-gloves-spider-os-inactive");
        }
    }

    private void OnSpiderOSPowerChanged(EntityUid suit, SpiderOSComponent component, ref SpiderOSPowerChangedEvent args)
    {
        if (args.Activated)
            return;

        if (!_inventory.TryGetSlotEntity(args.Wearer, "gloves", out var glovesUid) || glovesUid is not { } gloves
            || !TryComp<NinjaGlovesComponent>(gloves, out _))
        {
            return;
        }

        _toggle.TryDeactivate(gloves, user: args.Wearer, predicted: false, showPopup: false);
    }
    //DS14-end

    private void OnToggled(Entity<NinjaGlovesComponent> ent, ref ItemToggledEvent args)
    {
        if ((args.User ?? ent.Comp.User) is not { } user)
            return;

        var message = Loc.GetString(args.Activated ? "ninja-gloves-on" : "ninja-gloves-off");
        _popup.PopupClient(message, user, user);

        if (args.Activated && _ninja.NinjaQuery.TryComp(user, out var ninja))
            EnableGloves(ent, (user, ninja));
        else
            DisableGloves(ent);
    }

    protected virtual void EnableGloves(Entity<NinjaGlovesComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        var (uid, comp) = ent;
        comp.User = user;
        Dirty(uid, comp);
        _ninja.AssignGloves(user, uid);

        // yeah this is just ComponentToggler but with objective checking
        foreach (var ability in comp.Abilities)
        {
            // can't predict the objective related abilities
            if (ability.Objective == null)
                EntityManager.AddComponents(user, ability.Components);
        }
    }

    // TODO: generic event thing
    /// <summary>
    /// GloveCheck but for abilities stored on the player, skips some checks.
    /// Intended to be more generic, doesn't require the user to be a ninja or have any ninja equipment.
    /// </summary>
    public bool AbilityCheck(EntityUid uid, BeforeInteractHandEvent args, out EntityUid target)
    {
        target = args.Target;
        return !_combatMode.IsInCombatMode(uid)
            && _hands.GetActiveItem(uid) == null
            && _interaction.InRangeUnobstructed(uid, target);
    }
}

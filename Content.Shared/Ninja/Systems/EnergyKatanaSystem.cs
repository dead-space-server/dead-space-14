using Content.Shared.Inventory.Events;
using Content.Shared.Hands; // DS14
using Content.Shared.Ninja.Components;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// System for katana binding and dash events. Recalling is handled by the suit.
/// </summary>
public sealed partial class EnergyKatanaSystem : EntitySystem
{
    [Dependency] private SharedSpaceNinjaSystem _ninja = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergyKatanaComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<EnergyKatanaComponent, GotEquippedHandEvent>(OnEquippedHand); // DS14
        SubscribeLocalEvent<EnergyKatanaComponent, CheckDashEvent>(OnCheckDash);
    }

    /// <summary>
    /// When equipped by a ninja, try to bind it.
    /// </summary>
    private void OnEquipped(Entity<EnergyKatanaComponent> ent, ref GotEquippedEvent args)
    {
        _ninja.BindKatana(args.Equipee, ent);
    }

    // DS14-start
    /// <summary>
    /// When picked up by a ninja, try to bind it.
    /// This covers cases like ninja sheaths that store the katana in a container instead of an equipment slot.
    /// </summary>
    private void OnEquippedHand(Entity<EnergyKatanaComponent> ent, ref GotEquippedHandEvent args)
    {
        _ninja.BindKatana(args.User, ent);
    }
    // DS14-end

    private void OnCheckDash(Entity<EnergyKatanaComponent> ent, ref CheckDashEvent args)
    {
        // Just use a whitelist fam
        if (!_ninja.IsNinja(args.User))
            args.Cancelled = true;
    }
}

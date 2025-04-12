using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeBlade()
    {
        SubscribeLocalEvent<ArmutantComponent, ArmutantBladeActiveEvent>(OnBladeActive);
    }
    private void OnBladeActive(Entity<ArmutantComponent> ent, ref ArmutantBladeActiveEvent args)
    {
        if (args.Handled)
            return;

        if (!_net.IsServer)
            return;



        args.Handled = true;
    }
}

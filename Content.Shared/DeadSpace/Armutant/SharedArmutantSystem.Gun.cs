using System.Numerics;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeGun()
    {
        SubscribeLocalEvent<ArmutantComponent, GunSmokeActionEvent>(OnGunBallAction);
    }

    private void OnGunBallAction(Entity<ArmutantComponent> ent, ref GunSmokeActionEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var effect = Spawn(args.SpawnPrototype, Transform(ent).Coordinates);

        args.Handled = true;
    }
}

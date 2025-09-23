using System.Numerics;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeGun()
    {
        SubscribeLocalEvent<ArmutantComponent, GunSmokeActionEvent>(OnGunBallAction);
<<<<<<< HEAD
=======
        SubscribeLocalEvent<ArmutantComponent, GunZoomActionEvent>(OnZoomAction);
>>>>>>> aae74230027de3995009b60cae20059374e38691
    }

    private void OnGunBallAction(Entity<ArmutantComponent> ent, ref GunSmokeActionEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

<<<<<<< HEAD
        var effect = Spawn(args.SpawnPrototype, Transform(ent).Coordinates);

        args.Handled = true;
    }
=======
        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantGunActionComponent>(actionEnt, out var armutantActionComp))
            return;

        var effect = Spawn(armutantActionComp.SpawnPrototype, Transform(ent).Coordinates);

        args.Handled = true;
    }

    private void OnZoomAction(Entity<ArmutantComponent> uid, ref GunZoomActionEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantGunActionComponent>(actionEnt, out var armutantActionComp))
            return;

        armutantActionComp.Enabled = !armutantActionComp.Enabled;

        if (armutantActionComp.Enabled)
        {
            _eye.SetMaxZoom(uid, armutantActionComp.Zoom);
            _eye.SetZoom(uid, armutantActionComp.Zoom);
        }
        else
        {
            _eye.ResetZoom(uid);
            armutantActionComp.Offset = Vector2.Zero;
        }
        args.Handled = true;
    }
>>>>>>> aae74230027de3995009b60cae20059374e38691
}

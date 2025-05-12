using System.Numerics;

namespace Content.Shared.DeadSpace.Armutant;

public partial class SharedArmutantSystem
{
    private void InitializeGun()
    {
        SubscribeLocalEvent<ArmutantComponent, GunSmokeActionEvent>(OnGunBallAction);
        SubscribeLocalEvent<ArmutantComponent, GunZoomActionEvent>(OnZoomAction);
    }

    private void OnGunBallAction(Entity<ArmutantComponent> ent, ref GunSmokeActionEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

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
}

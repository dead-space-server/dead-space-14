using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Guardian;

public sealed partial class GuardianLightningArcSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new GuardianLightningArcOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<GuardianLightningArcOverlay>();
    }
}

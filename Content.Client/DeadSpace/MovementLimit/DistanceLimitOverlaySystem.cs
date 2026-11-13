// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Shared.IoC;

namespace Content.Client.DeadSpace.MovementLimit;

public sealed partial class DistanceLimitOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayManager.AddOverlay(new DistanceLimitOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<DistanceLimitOverlay>();
    }
}

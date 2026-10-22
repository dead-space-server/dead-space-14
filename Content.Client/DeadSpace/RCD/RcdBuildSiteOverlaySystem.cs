// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.RCD;

public sealed class RcdBuildSiteOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new RcdBuildSiteOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<RcdBuildSiteOverlay>();
    }
}

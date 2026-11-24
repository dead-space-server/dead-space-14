// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;

namespace Content.Client.DeadSpace.PlanetSurvey;

public sealed partial class SurveyObjectivesSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _cache = default!;

    private SurveyObjectivesOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new SurveyObjectivesOverlay(EntityManager, _player, _cache);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}

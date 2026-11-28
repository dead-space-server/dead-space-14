// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.PlanetSurvey;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDebugSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private SurveyDebugOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SurveyDebugMessage>(OnDebug);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    private void OnDebug(SurveyDebugMessage message)
    {
        if (message.Entries.Count == 0)
        {
            if (_overlay != null)
            {
                _overlays.RemoveOverlay(_overlay);
                _overlay = null;
            }

            return;
        }

        if (_overlay == null)
        {
            _overlay = new SurveyDebugOverlay(EntityManager, _eye, _xform, _cache);
            _overlays.AddOverlay(_overlay);
        }

        _overlay.Entries = message.Entries;
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Lavaland;

public sealed class LavalandBossRageAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private LavalandBossRageAnnouncementOverlay? _announcement;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandBossRageAnnouncementEvent>(OnAnnouncement);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_announcement != null)
            _overlay.RemoveOverlay(_announcement);
        _announcement = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _announcement?.FrameUpdate(frameTime);
    }

    private void OnAnnouncement(LavalandBossRageAnnouncementEvent ev)
    {
        if (_announcement == null)
        {
            _announcement = new LavalandBossRageAnnouncementOverlay();
            _overlay.AddOverlay(_announcement);
        }

        _announcement.Show(ev.Text, ev.Duration);
    }
}

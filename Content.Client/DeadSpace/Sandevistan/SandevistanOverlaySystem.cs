// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Sandevistan;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.DeadSpace.Sandevistan;

public sealed partial class SandevistanOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    private SandevistanOverlay _overlay = default!;
    private bool _overlayAdded;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        SyncOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        SyncOverlay();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void SyncOverlay()
    {
        var player = _player.LocalEntity;
        if (player != null &&
            (HasComp<ActiveSandevistanComponent>(player.Value) ||
             HasComp<SandevistanVisualFadeoutComponent>(player.Value)))
        {
            AddOverlay();
            return;
        }

        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlayAdded)
            return;

        _overlay.Reset();
        _overlayAdded = true;

        if (!_overlayMan.HasOverlay<SandevistanOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (!_overlayAdded)
            return;

        _overlay.Reset();
        _overlayMan.RemoveOverlay(_overlay);
        _overlayAdded = false;
    }
}

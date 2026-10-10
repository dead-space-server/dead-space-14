using Content.Client.Camera;                     // CameraRecoilSystem
using Content.Shared.Camera;                     // CameraRecoilComponent
using Content.Shared.DeadSpace.Camera;           // ScreenshakeSystem, ScreenshakeParameters
using Content.Shared.Sectants;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Random;

namespace Content.Client.Sectants;

public sealed class ClientSectantAnnouncementSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CameraRecoilSystem _recoil = default!;
    [Dependency] private readonly ScreenshakeSystem _screenshake = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<SectantAnnouncementEvent>(OnAnnounce);
    }

    private void OnAnnounce(SectantAnnouncementEvent ev)
    {
        // 1. Красная вспышка + винетка
        _overlay.AddOverlay(new SectantFlashOverlay(ev.Color, ev.FlashDuration));

        if (_player.LocalEntity is not { } local)
            return;

        // 2. Тряска камеры (KickCamera)
        var kick = new Vector2(
            _random.NextFloat(-ev.ShakeIntensity, ev.ShakeIntensity),
            _random.NextFloat(-ev.ShakeIntensity, ev.ShakeIntensity));
        _recoil.KickCamera(local, kick);

        // 3. Дополнительная тряска экрана через ScreenshakeSystem
        _screenshake.Screenshake(local, null, new ScreenshakeParameters
        {
            Trauma = 0.5f,          // сила удара (0..1)
            DecayRate = 1.2f,       // как быстро затухает
            Frequency = 0.008f,     // частота дрожания
        });
    }
}

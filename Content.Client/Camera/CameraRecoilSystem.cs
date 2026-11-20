using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Camera;
using Robust.Shared.Configuration;

namespace Content.Client.Camera;

public sealed partial class CameraRecoilSystem : SharedCameraRecoilSystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private ScreenshakeSystem _screenshake = default!;

    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CameraKickEvent>(OnCameraKick);

        Subs.CVar(_configManager, CCVars.ScreenShakeIntensity, OnCvarChanged, true);
    }

    private void OnCvarChanged(float value)
    {
        _intensity = value;
    }

    private void OnCameraKick(CameraKickEvent ev)
    {
        KickCamera(GetEntity(ev.NetEntity), ev.Recoil);
    }

    public override void KickCamera(EntityUid uid, Vector2 recoil, CameraRecoilComponent? component = null)
    {
        if (_intensity == 0)
            return;

        if (!Resolve(uid, ref component, false))
            return;

        var recoilLength = recoil.Length();
        if (recoilLength > 0.001f)
        {
            _screenshake.Screenshake(uid, null, new ScreenshakeParameters
            {
                Trauma = MathF.Min(0.12f, recoilLength * 0.07f),
                DecayRate = 1.2f,
                Frequency = 0.008f,
            });
        }

        var linearScale = MathHelper.Lerp(1f, 0.26f, Math.Clamp(_intensity, 0f, 1f));
        recoil *= _intensity * linearScale;

        // Use really bad math to "dampen" kicks when we're already kicked.
        var existing = component.CurrentKick.Length();
        var dampen = existing / KickMagnitudeMax;
        component.CurrentKick += recoil * (1 - dampen);

        if (component.CurrentKick.Length() > KickMagnitudeMax)
            component.CurrentKick = component.CurrentKick.Normalized() * KickMagnitudeMax;

        component.LastKickTime = 0;
    }
}

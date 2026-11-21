using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Sanity;

public sealed partial class SanityEffectsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private SanityOverlay? _sanityOverlay;
    private EntityUid? _overlayEntity;
    private const float EyeNudge = 0.04f;
    private Vector2 _eyeNudge;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SanityComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<SanityOverlayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SanityOverlayComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_timing.IsFirstTimePredicted) return;

        var local = _player.LocalEntity;
        if (local == null || !TryComp<SanityComponent>(local, out var c) || !HasComp<SanityOverlayComponent>(local))
        {
            RemoveOverlay();
            return;
        }

        EnsureOverlay(local.Value);

        _cameraRecoil.KickCamera(local.Value,
            new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)) * ((c.MaxSanityLevel - c.SanityLevel) / 100));
        var target = Math.Clamp((c.MaxSanityLevel - c.SanityLevel) / c.MaxSanityLevel, 0f, 1f);
        _sanityOverlay!.Value = MathHelper.Lerp(_sanityOverlay.Value, target, Math.Clamp(frameTime * 2f, 0f, 1f));
        var t = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)) * EyeNudge;
        _eyeNudge = Vector2.Lerp(_eyeNudge, t, 0.35f);
    }

    private void OnEyeOffset(EntityUid uid, SanityComponent comp, ref GetEyeOffsetEvent args)
    {
        if (uid != _player.LocalEntity)
            return;
        args.Offset += _eyeNudge;
    }
    private void OnStartup(EntityUid uid, SanityOverlayComponent comp, ComponentStartup args)
    {
        if (_player.LocalEntity != uid || !HasComp<SanityComponent>(uid))
            return;

        EnsureOverlay(uid);
    }
    private void OnShutdown(EntityUid uid, SanityOverlayComponent comp, ComponentShutdown args)
    {
        if (_overlayEntity == uid)
            RemoveOverlay();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RemoveOverlay();

        if (HasComp<SanityComponent>(args.Entity) && HasComp<SanityOverlayComponent>(args.Entity))
            EnsureOverlay(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_overlayEntity == args.Entity)
            RemoveOverlay();
    }

    private void EnsureOverlay(EntityUid uid)
    {
        if (_sanityOverlay != null && _overlayEntity == uid)
            return;

        RemoveOverlay();

        _sanityOverlay = new SanityOverlay();
        _overlayEntity = uid;
        _overlayManager.AddOverlay(_sanityOverlay);
    }

    private void RemoveOverlay()
    {
        _eyeNudge = Vector2.Zero;

        if (_sanityOverlay == null)
        {
            _overlayEntity = null;
            return;
        }

        _overlayManager.RemoveOverlay(_sanityOverlay);
        _sanityOverlay = null;
        _overlayEntity = null;
    }
}

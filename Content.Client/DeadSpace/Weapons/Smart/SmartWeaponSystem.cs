// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Implants;
using Content.Shared.DeadSpace.Weapons.Smart;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Client.DeadSpace.Weapons.Smart;

public sealed class SmartWeaponSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatModeSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;

    private SmartWeaponOverlay _overlay = default!;
    private Texture? _innerRingTexture;
    private Texture? _outerRingTexture;
    private Texture? _cornerStickTexture;
    private Texture? _crossTexture;
    private Texture?[] _ammoDigitTextures = new Texture?[10];

    public bool IsActive { get; private set; }
    public Color CrosshairColor { get; private set; }
    public Color TargetHighlightColor { get; private set; }
    public Color TargetMarkerCrossColor { get; private set; }
    public Color DebugZoneColor { get; private set; }
    public bool ShowDebugZone { get; private set; }
    public float TargetRadiusPixels { get; private set; }
    public float PulseFrequency { get; private set; }
    public float PulseAmplitude { get; private set; }
    public float AnimationSpeed { get; private set; }
    public float LockRotation { get; private set; }
    public float LockScaleFactor { get; private set; }
    public float TargetAnimationDuration { get; private set; }
    public float CrosshairScale { get; private set; }
    public float CrosshairAlpha { get; private set; }
    public Vector2 MouseScreenPosition { get; private set; }
    public EntityUid? LockedTarget { get; private set; }
    public Vector2? TargetScreenPosition { get; private set; }
    public float CrosshairProgress { get; private set; }
    public int AmmoCount { get; private set; }
    public Color AmmoDigitsColor { get; private set; }
    public float AmmoDigitsScale { get; private set; }

    public Color ShadowColor { get; private set; }
    public Vector2 ShadowOffset { get; private set; }
    public bool AmmoHoloEffect { get; private set; }
    public float GlitchMinInterval { get; private set; }
    public float GlitchMaxInterval { get; private set; }
    public float GlitchDuration { get; private set; }
    public float GlitchStrength { get; private set; }

    public Texture? InnerRingTexture => _innerRingTexture;
    public Texture? OuterRingTexture => _outerRingTexture;
    public Texture? CornerStickTexture => _cornerStickTexture;
    public Texture? CrossTexture => _crossTexture;
    public Texture?[] AmmoDigitTextures => _ammoDigitTextures;

    public IReadOnlyList<TargetAnimationState> TargetAnimations => _targetAnimations;

    private List<TargetAnimationState> _targetAnimations = new();
    private bool _targetWasValid;
    private EntityUid? _lastSentTarget;
    private EntityUid? _lastSentGun;

    private int _displayedAmmoCount;
    private TimeSpan _lastAmmoChangeTime;

    private bool _hasPendingLost;
    private TimeSpan? _lostTargetTime;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new SmartWeaponOverlay(this, _timing);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;
        if (playerEntity == null)
        {
            SetInactive();
            return;
        }

        if (!_combatModeSystem.IsInCombatMode(playerEntity.Value))
        {
            SetInactive();
            return;
        }

        if (!HasComp<SmartLinkImplantComponent>(playerEntity.Value))
        {
            SetInactive();
            return;
        }

        if (!TryComp(playerEntity.Value, out HandsComponent? handsComp))
        {
            SetInactive();
            return;
        }

        var handsEntity = new Entity<HandsComponent?>(playerEntity.Value, handsComp);
        if (!_handsSystem.TryGetActiveItem(handsEntity, out var gunEntity) || gunEntity == null)
        {
            SetInactive();
            return;
        }

        if (!TryComp(gunEntity.Value, out SmartWeaponComponent? smartWeapon))
        {
            SetInactive();
            return;
        }

        IsActive = true;

        CrosshairColor = smartWeapon.CrosshairColor;
        TargetHighlightColor = smartWeapon.TargetHighlightColor;
        TargetMarkerCrossColor = smartWeapon.TargetMarkerCrossColor;
        DebugZoneColor = smartWeapon.DebugZoneColor;
        ShowDebugZone = smartWeapon.ShowDebugZone;
        TargetRadiusPixels = smartWeapon.TargetRadiusPixels;
        PulseFrequency = smartWeapon.PulseFrequency;
        PulseAmplitude = smartWeapon.PulseAmplitude;
        AnimationSpeed = smartWeapon.AnimationSpeed;
        LockRotation = MathHelper.DegreesToRadians(smartWeapon.LockRotationDegrees);
        LockScaleFactor = smartWeapon.LockScaleFactor;
        TargetAnimationDuration = smartWeapon.TargetAnimationDuration;
        CrosshairScale = smartWeapon.CrosshairScale;
        CrosshairAlpha = smartWeapon.CrosshairAlpha;
        AmmoDigitsColor = smartWeapon.AmmoDigitsColor;
        AmmoDigitsScale = smartWeapon.AmmoDigitsScale;

        ShadowColor = smartWeapon.ShadowColor;
        ShadowOffset = smartWeapon.ShadowOffset;
        AmmoHoloEffect = smartWeapon.AmmoHoloEffect;
        GlitchMinInterval = smartWeapon.GlitchMinInterval;
        GlitchMaxInterval = smartWeapon.GlitchMaxInterval;
        GlitchDuration = smartWeapon.GlitchDuration;
        GlitchStrength = smartWeapon.GlitchStrength;

        LoadTextures(smartWeapon);

        MouseScreenPosition = _inputManager.MouseScreenPosition.Position;

        int realAmmo = GetRealAmmoCount(gunEntity.Value);
        SmoothAmmoCount(realAmmo);
        AmmoCount = _displayedAmmoCount;

        EntityUid? bestTarget = FindTarget(playerEntity.Value, MouseScreenPosition, TargetRadiusPixels);

        bool targetValidNow = false;
        if (LockedTarget != null)
        {
            targetValidNow = IsTargetValid(playerEntity.Value, LockedTarget.Value, MouseScreenPosition, TargetRadiusPixels);
        }

        if (LockedTarget != null)
        {
            if (targetValidNow)
            {
                if (_hasPendingLost)
                {
                    _hasPendingLost = false;
                    _lostTargetTime = null;
                }

                if (!_targetWasValid)
                {
                    _targetWasValid = true;
                    SetAnimationDirection(LockedTarget.Value, true);
                }
            }
            else
            {
                bool isDead = _mobStateSystem.IsDead(LockedTarget.Value);
                if (_targetWasValid)
                {
                    _audio.PlayGlobal(smartWeapon.TargetLostSound, Filter.Local(), false);
                    _targetWasValid = false;

                    bool fullyAcquired = false;
                    var anim = FindAnimation(LockedTarget.Value);
                    if (anim.HasValue && anim.Value.IsLocking && anim.Value.Progress >= 1f)
                    {
                        fullyAcquired = true;
                    }

                    if (fullyAcquired && !isDead)
                    {
                        _hasPendingLost = true;
                        _lostTargetTime = _timing.CurTime + TimeSpan.FromSeconds(2);
                    }
                    else
                    {
                        SetAnimationDirection(LockedTarget.Value, false);
                        _hasPendingLost = false;
                        _lostTargetTime = null;
                    }
                }
                else if (_hasPendingLost && _timing.CurTime >= _lostTargetTime)
                {
                    _targetWasValid = false;
                    SetAnimationDirection(LockedTarget.Value, false);
                    _hasPendingLost = false;
                    _lostTargetTime = null;
                }
            }
        }

        if (bestTarget != null)
        {
            if (LockedTarget == null)
            {
                LockedTarget = bestTarget;
                _targetWasValid = true;
                _audio.PlayGlobal(smartWeapon.TargetLockSound, Filter.Local(), false);
                AddTargetAnimation(bestTarget.Value, true);
            }
            else if (LockedTarget != bestTarget)
            {
                if (_targetWasValid)
                {
                    _targetWasValid = false;
                    SetAnimationDirection(LockedTarget.Value, false);
                }
                else
                {
                    SetAnimationDirection(LockedTarget.Value, false);
                }

                LockedTarget = bestTarget;
                _targetWasValid = true;
                AddTargetAnimation(bestTarget.Value, true);
            }
        }

        float crosshairTarget = LockedTarget != null ? 1f : 0f;
        float crosshairDelta = AnimationSpeed * frameTime;
        CrosshairProgress = crosshairTarget > 0
            ? MathF.Min(1f, CrosshairProgress + crosshairDelta)
            : MathF.Max(0f, CrosshairProgress - crosshairDelta);

        UpdateTargetAnimations(frameTime);

        if (LockedTarget != null && !_targetWasValid)
        {
            var anim = FindAnimation(LockedTarget.Value);
            if (anim == null || (!anim.Value.IsLocking && anim.Value.Progress <= 0f))
            {
                LockedTarget = null;
                _hasPendingLost = false;
                _lostTargetTime = null;
            }
        }

        if (LockedTarget != null)
            TargetScreenPosition = GetScreenPosition(LockedTarget.Value);
        else
            TargetScreenPosition = null;

        if (_lastSentTarget != LockedTarget)
        {
            _lastSentTarget = LockedTarget;
            _lastSentGun = gunEntity.Value;
            RaiseNetworkEvent(new SmartWeaponTargetChangedEvent(
                GetNetEntity(gunEntity.Value),
                LockedTarget != null ? GetNetEntity(LockedTarget.Value) : null));
        }
    }

    private void SmoothAmmoCount(int realAmmo)
    {
        if (realAmmo == _displayedAmmoCount)
        {
            _lastAmmoChangeTime = _timing.CurTime;
            return;
        }

        if (realAmmo == _displayedAmmoCount - 1 &&
            _timing.CurTime - _lastAmmoChangeTime < TimeSpan.FromSeconds(0.15))
        {
            return;
        }

        _displayedAmmoCount = realAmmo;
        _lastAmmoChangeTime = _timing.CurTime;
    }

    private int GetRealAmmoCount(EntityUid gun)
    {
        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref ev);
        if (ev.Count > 0)
            return ev.Count;

        int ammo = 0;

        var magazine = _itemSlotsSystem.GetItemOrNull(gun, "gun_magazine");
        if (magazine != null)
        {
            var magEv = new GetAmmoCountEvent();
            RaiseLocalEvent(magazine.Value, ref magEv);
            ammo += magEv.Count;
        }
        else if (TryComp<BallisticAmmoProviderComponent>(gun, out var internalProvider))
        {
            ammo += internalProvider.Count;
        }

        var chamber = _itemSlotsSystem.GetItemOrNull(gun, "gun_chamber");
        if (chamber != null)
            ammo += 1;

        return ammo;
    }

    private void LoadTextures(SmartWeaponComponent smartWeapon)
    {
        if (_innerRingTexture == null)
            _innerRingTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(smartWeapon.CrosshairInnerTexture));

        if (_outerRingTexture == null)
            _outerRingTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(smartWeapon.CrosshairOuterTexture));

        if (_cornerStickTexture == null)
            _cornerStickTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(smartWeapon.CornerStickTexture));

        if (_crossTexture == null)
            _crossTexture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(smartWeapon.CrossTexture));

        if (_ammoDigitTextures[0] == null)
        {
            for (int i = 0; i < 10; i++)
            {
                _ammoDigitTextures[i] = _spriteSystem.Frame0(new SpriteSpecifier.Rsi(smartWeapon.AmmoDigitsRsi, i.ToString()));
            }
        }
    }

    private void UpdateTargetAnimations(float frameTime)
    {
        for (int i = _targetAnimations.Count - 1; i >= 0; i--)
        {
            var anim = _targetAnimations[i];
            if (anim.IsLocking)
            {
                anim.Progress = MathF.Min(TargetAnimationDuration, anim.Progress + frameTime);
            }
            else
            {
                anim.Progress = MathF.Max(0f, anim.Progress - frameTime);
                if (anim.Progress <= 0f)
                {
                    _targetAnimations.RemoveAt(i);
                    continue;
                }
            }
            _targetAnimations[i] = anim;
        }
    }

    private void AddTargetAnimation(EntityUid target, bool isLocking)
    {
        var existing = FindAnimation(target);
        if (existing.HasValue)
        {
            var anim = existing.Value;
            anim.IsLocking = isLocking;
            if (isLocking)
                anim.Progress = MathF.Max(anim.Progress, 0f);
            else
                anim.Progress = MathF.Min(anim.Progress, TargetAnimationDuration);
            for (int i = 0; i < _targetAnimations.Count; i++)
            {
                if (_targetAnimations[i].Target == target)
                {
                    _targetAnimations[i] = anim;
                    break;
                }
            }
        }
        else
        {
            _targetAnimations.Add(new TargetAnimationState
            {
                Target = target,
                Progress = 0f,
                IsLocking = isLocking
            });
        }
    }

    private void SetAnimationDirection(EntityUid target, bool isLocking)
    {
        int index = _targetAnimations.FindIndex(a => a.Target == target);
        if (index >= 0)
        {
            var anim = _targetAnimations[index];
            anim.IsLocking = isLocking;
            _targetAnimations[index] = anim;
        }
        else
        {
            AddTargetAnimation(target, isLocking);
        }
    }

    private TargetAnimationState? FindAnimation(EntityUid target)
    {
        for (int i = 0; i < _targetAnimations.Count; i++)
        {
            if (_targetAnimations[i].Target == target)
                return _targetAnimations[i];
        }
        return null;
    }

    private bool IsTargetValid(EntityUid player, EntityUid target, Vector2 mousePosPx, float radiusPx)
    {
        if (_mobStateSystem.IsDead(target))
            return false;

        var screenPos = GetScreenPosition(target);
        var dist = Vector2.Distance(mousePosPx, screenPos);
        if (dist > radiusPx)
            return false;

        return _examineSystem.InRangeUnOccluded(player, target, ExamineSystemShared.ExamineRange);
    }

    private EntityUid? FindTarget(EntityUid player, Vector2 mousePosPx, float radiusPx)
    {
        EntityUid? closestEntity = null;
        float closestDist = float.MaxValue;

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (uid == player || _mobStateSystem.IsDead(uid))
                continue;

            if (transform.MapID == MapId.Nullspace)
                continue;

            var worldPos = _transformSystem.GetWorldPosition(uid);
            var screenPos = _eyeManager.WorldToScreen(worldPos);
            var dist = Vector2.Distance(mousePosPx, screenPos);

            if (dist > radiusPx || dist >= closestDist)
                continue;

            if (!_examineSystem.InRangeUnOccluded(player, uid, ExamineSystemShared.ExamineRange))
                continue;

            closestDist = dist;
            closestEntity = uid;
        }

        return closestEntity;
    }

    public Vector2 GetScreenPosition(EntityUid entity)
    {
        var worldPos = _transformSystem.GetWorldPosition(entity);
        return _eyeManager.WorldToScreen(worldPos);
    }

    private void SetInactive()
    {
        if (_lastSentGun != null && _lastSentTarget != null)
        {
            RaiseNetworkEvent(new SmartWeaponTargetChangedEvent(
                GetNetEntity(_lastSentGun.Value),
                null));
        }

        IsActive = false;
        LockedTarget = null;
        TargetScreenPosition = null;
        CrosshairProgress = 0f;
        AmmoCount = 0;
        _displayedAmmoCount = 0;
        _targetAnimations.Clear();
        _targetWasValid = false;
        _lastSentTarget = null;
        _lastSentGun = null;

        _hasPendingLost = false;
        _lostTargetTime = null;
    }
}

public struct TargetAnimationState
{
    public EntityUid Target;
    public float Progress;
    public bool IsLocking;
}
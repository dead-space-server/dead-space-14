// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Shared.DeadSpace.Weapons.Smart;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmartWeaponComponent : Component
{
    [DataField("crosshairColor")]
    public Color CrosshairColor = Color.Red;

    [DataField("targetHighlightColor")]
    public Color TargetHighlightColor = Color.Red;

    [DataField("targetMarkerCrossColor")]
    public Color TargetMarkerCrossColor = Color.Cyan;

    [DataField("debugZoneColor")]
    public Color DebugZoneColor = Color.Lime.WithAlpha(0.25f);

    [DataField("showDebugZone")]
    public bool ShowDebugZone = false;

    [DataField("targetRadiusPixels")]
    public float TargetRadiusPixels = 150f;

    [DataField("crosshairScale")]
    public float CrosshairScale = 1f;

    [DataField("crosshairAlpha")]
    public float CrosshairAlpha = 1f;

    [DataField("pulseFrequency")]
    public float PulseFrequency = 2.5f;

    [DataField("pulseAmplitude")]
    public float PulseAmplitude = 3f;

    [DataField("animationSpeed")]
    public float AnimationSpeed = 4f;

    [DataField("lockRotationDegrees")]
    public float LockRotationDegrees = 45f;

    [DataField("lockScaleFactor")]
    public float LockScaleFactor = 0.8f;

    [DataField("targetAnimationDuration")]
    public float TargetAnimationDuration = 1.5f;

    [DataField("cornerStickTexture")]
    public ResPath CornerStickTexture = new("/Textures/_DeadSpace/Interface/SmartWeapon/corner_stick.png");

    [DataField("crossTexture")]
    public ResPath CrossTexture = new("/Textures/_DeadSpace/Interface/SmartWeapon/cross.png");

    [DataField("targetLockSound")]
    public SoundSpecifier? TargetLockSound;

    [DataField("targetLostSound")]
    public SoundSpecifier? TargetLostSound;

    [DataField("crosshairInnerTexture")]
    public ResPath CrosshairInnerTexture = new("/Textures/_DeadSpace/Interface/SmartWeapon/ring_inner.png");

    [DataField("crosshairOuterTexture")]
    public ResPath CrosshairOuterTexture = new("/Textures/_DeadSpace/Interface/SmartWeapon/ring_outer.png");

    [DataField("ammoDigitsRsi")]
    public ResPath AmmoDigitsRsi = new("/Textures/_DeadSpace/Interface/SmartWeapon/ammo_digits.rsi");

    [DataField("ammoDigitsColor")]
    public Color AmmoDigitsColor = Color.Yellow;

    [DataField("ammoDigitsScale")]
    public float AmmoDigitsScale = 3f;

    [DataField]
    public EntityUid? Target;

    [DataField]
    public float MagnetismStrength = 2f;

    [DataField]
    public Angle MagnetismMaxAngle = Angle.FromDegrees(10);

    [DataField]
    public float MagnetismUpdateInterval = 0.1f;

    [DataField]
    public bool MagnetismRotateWithImpulse = true;

    [DataField]
    public float MagnetismDelay = 0.02f;

    [DataField("ammoHoloEffect")]
    public bool AmmoHoloEffect = true;

    [DataField("shadowColor")]
    public Color ShadowColor = Color.Black.WithAlpha(0.5f);

    [DataField("shadowOffset")]
    public Vector2 ShadowOffset = new(2f, 2f);

    [DataField("glitchMinInterval")]
    public float GlitchMinInterval = 8f;

    [DataField("glitchMaxInterval")]
    public float GlitchMaxInterval = 16f;

    [DataField("glitchDuration")]
    public float GlitchDuration = 0.15f;

    [DataField("glitchStrength")]
    public float GlitchStrength = 2f;
}
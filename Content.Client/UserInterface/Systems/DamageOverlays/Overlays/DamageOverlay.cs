using System.Numerics;
using Content.Client.DeadSpace.Heartbeat;
using Content.Shared.CCVar;
using Content.Shared.Mobs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.DamageOverlays.Overlays;

public sealed class DamageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> DamageShader = "DeadSpaceDamageBurn";
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => !_cfg.GetCVar(CCVars.OldCritScreen);

    public MobState State = MobState.Alive;
    public float PainLevel;
    public float OxygenLevel;
    public float PreCriticalLevel;
    public float CritLevel;
    public float DeadLevel = 1f;

    private readonly ShaderInstance _shader;
    private readonly ShaderInstance _critShader;
    private readonly ShaderInstance _oxygenShader;
    private readonly ShaderInstance _bruteShader;
    private CritHeartbeatSystem? _heartbeat;
    private float _pain;
    private float _oxygen;
    private float _preCritical;
    private float _critical;
    private float _preCriticalState;
    private float _criticalState;
    private float _deadState;

    private float _oldPainLevel = 0f;
    private float _oldOxygenLevel = 0f;
    private float _oldCritLevel = 0f;

    public DamageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(DamageShader).InstanceUnique();
        _critShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _oxygenShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _bruteShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    public void Reset()
    {
        State = MobState.Alive;
        PainLevel = 0f;
        OxygenLevel = 0f;
        PreCriticalLevel = 0f;
        CritLevel = 0f;
        DeadLevel = 0f;
        _pain = 0f;
        _oxygen = 0f;
        _preCritical = 0f;
        _critical = 0f;
        _preCriticalState = 0f;
        _criticalState = 0f;
        _deadState = 0f;
        _oldPainLevel = 0f;
        _oldOxygenLevel = 0f;
        _oldCritLevel = 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eye) ||
            args.Viewport.Eye != eye.Eye)
        {
            return;
        }

        if (_cfg.GetCVar(CCVars.OldCritScreen))
            DrawLegacy(args);
        else
            DrawBloodBurn(args);
    }

    /// <summary>
    ///     The pre-rework crit screen: a red pulsing vignette for brute damage,
    ///     a dark oxygen overlay and a white tunnel that closes in during crit.
    /// </summary>
    private void DrawLegacy(in OverlayDrawArgs args)
    {
        /*
         * Here's the rundown:
         * 1. There's lerping for each level so the transitions are smooth.
         * 2. There's 3 overlays, 1 for brute damage, 1 for oxygen damage (that also doubles as a crit overlay),
         * and a white one during crit that closes in as you progress towards death. When you die it slowly disappears.
         * The crit overlay also occasionally reduces its alpha as a "blink"
         */

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        var time = (float) _timing.RealTime.TotalSeconds;
        var lastFrameTime = (float) _timing.FrameTime.TotalSeconds;

        // If they just died then lerp out the white overlay.
        if (State != MobState.Dead)
        {
            DeadLevel = 1f;
        }
        else if (!MathHelper.CloseTo(0f, DeadLevel, 0.001f))
        {
            var diff = -DeadLevel;
            DeadLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            DeadLevel = 0f;
        }

        if (!MathHelper.CloseTo(_oldPainLevel, PainLevel, 0.001f))
        {
            var diff = PainLevel - _oldPainLevel;
            _oldPainLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        if (!MathHelper.CloseTo(_oldOxygenLevel, OxygenLevel, 0.001f))
        {
            var diff = OxygenLevel - _oldOxygenLevel;
            _oldOxygenLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldOxygenLevel = OxygenLevel;
        }

        if (!MathHelper.CloseTo(_oldCritLevel, CritLevel, 0.001f))
        {
            var diff = CritLevel - _oldCritLevel;
            _oldCritLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldCritLevel = CritLevel;
        }

        /*
         * darknessAlphaOuter is the maximum alpha for anything outside of the larger circle
         * darknessAlphaInner (on the shader) is the alpha for anything inside the smallest circle
         *
         * outerCircleRadius is what we end at for max level for the outer circle
         * outerCircleMaxRadius is what we start at for 0 level for the outer circle
         *
         * innerCircleRadius is what we end at for max level for the inner circle
         * innerCircleMaxRadius is what we start at for 0 level for the inner circle
         */

        // Makes debugging easier don't @ me
        float level = 0f;
        level = _oldPainLevel;

        // TODO: Lerping
        if (level > 0f && _oldCritLevel <= 0f)
        {
            var pulseRate = 3f;
            var adjustedTime = time * pulseRate;
            float outerMaxLevel = 2.0f * distance;
            float outerMinLevel = 0.8f * distance;
            float innerMaxLevel = 0.6f * distance;
            float innerMinLevel = 0.2f * distance;

            var outerRadius = outerMaxLevel - level * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - level * (innerMaxLevel - innerMinLevel);

            var pulse = MathF.Max(0f, MathF.Sin(adjustedTime));

            _bruteShader.SetParameter("time", pulse);
            _bruteShader.SetParameter("color", new Vector3(1f, 0f, 0f));
            _bruteShader.SetParameter("darknessAlphaOuter", 0.8f);

            _bruteShader.SetParameter("outerCircleRadius", outerRadius);
            _bruteShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            _bruteShader.SetParameter("innerCircleRadius", innerRadius);
            _bruteShader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);
            handle.UseShader(_bruteShader);
            handle.DrawRect(viewport, Color.White);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        level = State != MobState.Critical ? _oldOxygenLevel : 1f;

        if (level > 0f)
        {
            float outerMaxLevel = 0.6f * distance;
            float outerMinLevel = 0.06f * distance;
            float innerMaxLevel = 0.02f * distance;
            float innerMinLevel = 0.02f * distance;

            var outerRadius = outerMaxLevel - level * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - level * (innerMaxLevel - innerMinLevel);

            float outerDarkness;
            float critTime;

            // If in crit then just fix it; also pulse it very occasionally so they can see more.
            if (_oldCritLevel > 0f)
            {
                var adjustedTime = time * 2f;
                critTime = MathF.Max(0, MathF.Sin(adjustedTime) + 2 * MathF.Sin(2 * adjustedTime / 4f) + MathF.Sin(adjustedTime / 4f) - 3f);

                if (critTime > 0f)
                {
                    outerDarkness = 1f - critTime / 1.5f;
                }
                else
                {
                    outerDarkness = 1f;
                }
            }
            else
            {
                outerDarkness = MathF.Min(0.98f, 0.3f * MathF.Log(level) + 1f);
            }

            _oxygenShader.SetParameter("time", 0.0f);
            _oxygenShader.SetParameter("color", new Vector3(0f, 0f, 0f));
            _oxygenShader.SetParameter("darknessAlphaOuter", outerDarkness);
            _oxygenShader.SetParameter("innerCircleRadius", innerRadius);
            _oxygenShader.SetParameter("innerCircleMaxRadius", innerRadius);
            _oxygenShader.SetParameter("outerCircleRadius", outerRadius);
            _oxygenShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            handle.UseShader(_oxygenShader);
            handle.DrawRect(viewport, Color.White);
        }

        level = State != MobState.Dead ? _oldCritLevel : DeadLevel;

        if (level > 0f)
        {
            float outerMaxLevel = 2.0f * distance;
            float outerMinLevel = 1.0f * distance;
            float innerMaxLevel = 0.6f * distance;
            float innerMinLevel = 0.02f * distance;

            var outerRadius = outerMaxLevel - level * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - level * (innerMaxLevel - innerMinLevel);

            var pulse = MathF.Max(0f, MathF.Sin(time));

            // If in crit then just fix it; also pulse it very occasionally so they can see more.
            _critShader.SetParameter("time", pulse);
            _critShader.SetParameter("color", new Vector3(1f, 1f, 1f));
            _critShader.SetParameter("darknessAlphaOuter", 1.0f);
            _critShader.SetParameter("innerCircleRadius", innerRadius);
            _critShader.SetParameter("innerCircleMaxRadius", innerRadius + 0.005f * distance);
            _critShader.SetParameter("outerCircleRadius", outerRadius);
            _critShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            handle.UseShader(_critShader);
            handle.DrawRect(viewport, Color.White);
        }

        handle.UseShader(null);
    }

    /// <summary>
    ///     The default DeadSpace blood/burn crit screen.
    /// </summary>
    private void DrawBloodBurn(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var frameTime = (float) _timing.FrameTime.TotalSeconds;
        _pain = Approach(_pain, PainLevel, frameTime, 3.4f);
        _oxygen = Approach(_oxygen, OxygenLevel, frameTime, 3.4f);
        _preCritical = Approach(_preCritical, PreCriticalLevel, frameTime, 3f);
        _critical = Approach(_critical, CritLevel, frameTime, 2.8f);

        var preCriticalTarget = State == MobState.PreCritical ? 1f : 0f;
        var criticalTarget = State == MobState.Critical ? 1f : 0f;
        var deadTarget = State == MobState.Dead ? 1f : 0f;
        _preCriticalState = Approach(_preCriticalState, preCriticalTarget, frameTime, 2.8f);
        _criticalState = Approach(_criticalState, criticalTarget, frameTime, 2.8f);
        _deadState = Approach(_deadState, deadTarget, frameTime, 2.8f);

        if (State != MobState.Dead)
            DeadLevel = 1f;
        else
            DeadLevel = Approach(DeadLevel, 0f, frameTime, 1.35f);

        if (_pain <= 0f &&
            _oxygen <= 0f &&
            _preCriticalState <= 0f &&
            _criticalState <= 0f &&
            _deadState <= 0f)
        {
            return;
        }

        if (_heartbeat == null)
            _entitySystemManager.TryGetEntitySystem(out _heartbeat);

        _shader.SetParameter("DamageLevel", Math.Clamp(_pain, 0f, 1f));
        _shader.SetParameter("OxygenLevel", Math.Clamp(_oxygen, 0f, 1f));
        _shader.SetParameter("PreCriticalLevel", Math.Clamp(_preCritical, 0f, 1f));
        _shader.SetParameter("CriticalLevel", Math.Clamp(_critical, 0f, 1f));
        _shader.SetParameter("PreCriticalState", _preCriticalState);
        _shader.SetParameter("CriticalState", _criticalState);
        _shader.SetParameter("DeadState", _deadState);
        _shader.SetParameter("DeathFade", DeadLevel);
        _shader.SetParameter("Pulse", Math.Clamp(_heartbeat?.VisualPulse ?? 0f, 0f, 1f));
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }

    private float GetDiff(float value, float lastFrameTime)
    {
        var adjustment = value * 5f * lastFrameTime;

        if (value < 0f)
            adjustment = Math.Clamp(adjustment, value, -value);
        else
            adjustment = Math.Clamp(adjustment, -value, value);

        return adjustment;
    }

    private static float Approach(float current, float target, float frameTime, float speed)
    {
        var difference = target - current;
        if (MathHelper.CloseTo(difference, 0f, 0.001f))
            return target;

        return current + difference * Math.Clamp(speed * frameTime, 0f, 1f);
    }
}

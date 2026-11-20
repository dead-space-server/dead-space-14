using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Noise;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Camera;

public sealed partial class ScreenshakeSystem : EntitySystem
{
    private const float MaxOffset = 0.15f;
    private const float MaxRotationDegrees = 20f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Dictionary<string, TimeSpan>> _cooldowns = [];
    private readonly List<ScreenshakeCommand> _expiredCommands = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreenshakeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<ScreenshakeComponent, GetEyeRotationEvent>(OnGetEyeRotation);
        SubscribeLocalEvent<ScreenshakeComponent, EntityUnpausedEvent>(OnEntityUnpaused);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EyeComponent, ScreenshakeComponent>();
        while (query.MoveNext(out var uid, out _, out var shake))
        {
            if (shake.Commands.Count == 0)
            {
                RemCompDeferred<ScreenshakeComponent>(uid);
                continue;
            }

            _expiredCommands.Clear();

            foreach (var command in shake.Commands)
            {
                if (_timing.CurTime >= command.End)
                    _expiredCommands.Add(command);
            }

            if (_expiredCommands.Count == 0)
                continue;

            foreach (var command in _expiredCommands)
                shake.Commands.Remove(command);

            Dirty(uid, shake);
        }
    }

    private void OnGetEyeOffset(Entity<ScreenshakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (!HasComp<EyeComponent>(ent))
            return;

        var intensity = _cfg.GetCVar(CCVars.ScreenShakeIntensity);
        if (intensity <= 0)
            return;

        var noise = CreateNoise(67);
        var offset = Vector2.Zero;

        foreach (var command in ent.Comp.Commands)
        {
            if (command.Translation == null)
                continue;

            var trauma = GetTrauma(command.Translation, command.Start) * intensity;
            if (trauma <= 0)
                continue;

            noise.SetFrequency(command.Translation.Frequency);
            var time = (float) _timing.RealTime.TotalMilliseconds;
            var start = (float) command.Start.TotalMilliseconds;
            var x = MaxOffset * trauma * noise.GetNoise(time, start);

            noise.SetSeed(68);
            var y = MaxOffset * trauma * noise.GetNoise(time, start);
            noise.SetSeed(67);

            offset += new Vector2(x, y);
        }

        args.Offset += offset;
    }

    private void OnGetEyeRotation(Entity<ScreenshakeComponent> ent, ref GetEyeRotationEvent args)
    {
        if (!HasComp<EyeComponent>(ent))
            return;

        var intensity = _cfg.GetCVar(CCVars.ScreenShakeIntensity);
        if (intensity <= 0)
            return;

        var noise = CreateNoise(487);
        var rotation = Angle.Zero;

        foreach (var command in ent.Comp.Commands)
        {
            if (command.Rotation == null)
                continue;

            var trauma = GetTrauma(command.Rotation, command.Start) * intensity;
            if (trauma <= 0)
                continue;

            noise.SetFrequency(command.Rotation.Frequency);
            var angle = MaxRotationDegrees
                * trauma
                * noise.GetNoise((float) _timing.RealTime.TotalMilliseconds, (float) command.Start.TotalMilliseconds);

            rotation += Angle.FromDegrees(angle);
        }

        args.Rotation += rotation;
    }

    private void OnEntityUnpaused(Entity<ScreenshakeComponent> ent, ref EntityUnpausedEvent args)
    {
        var commands = new HashSet<ScreenshakeCommand>();

        foreach (var command in ent.Comp.Commands)
        {
            commands.Add(command with
            {
                Start = command.Start + args.PausedTime,
                End = command.End + args.PausedTime,
            });
        }

        ent.Comp.Commands = commands;
        Dirty(ent);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _cooldowns.Clear();
    }

    public bool IsOnCooldown(EntityUid uid, string key)
    {
        if (!_cooldowns.TryGetValue(uid, out var cooldowns))
        {
            _cooldowns[uid] = [];
            return false;
        }

        if (!cooldowns.TryGetValue(key, out var cooldown))
            return false;

        if (_timing.CurTime < cooldown)
            return true;

        cooldowns.Remove(key);
        return false;
    }

    public void Screenshake(
        EntityUid uid,
        ScreenshakeParameters? translation,
        ScreenshakeParameters? rotation,
        string key,
        float cooldown)
    {
        Screenshake(uid, translation, rotation, key, TimeSpan.FromSeconds(cooldown));
    }

    public void Screenshake(
        EntityUid uid,
        ScreenshakeParameters? translation,
        ScreenshakeParameters? rotation,
        string key,
        TimeSpan cooldown)
    {
        if (!_cooldowns.TryGetValue(uid, out var cooldowns))
            cooldowns = _cooldowns[uid] = [];

        if (cooldowns.TryGetValue(key, out var time))
        {
            if (_timing.CurTime < time)
                return;

            cooldowns.Remove(key);
        }

        cooldowns[key] = _timing.CurTime + cooldown;
        Screenshake(uid, translation, rotation);
    }

    public void Screenshake(EntityUid uid, ScreenshakeParameters? translation, ScreenshakeParameters? rotation)
    {
        if (!HasComp<EyeComponent>(uid))
            return;

        var start = _timing.CurTime;
        var end = GetEndTime(translation, rotation, start);
        var command = new ScreenshakeCommand(translation, rotation, start, end);
        var component = EnsureComp<ScreenshakeComponent>(uid);

        component.Commands.Add(command);
        Dirty(uid, component);
    }

    public void Screenshake(Filter filter, ScreenshakeParameters? translation, ScreenshakeParameters? rotation)
    {
        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } uid)
                Screenshake(uid, translation, rotation);
        }
    }

    private static FastNoiseLite CreateNoise(int seed)
    {
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        return noise;
    }

    private static TimeSpan GetEndTime(ScreenshakeParameters? translation, ScreenshakeParameters? rotation, TimeSpan start)
    {
        var translationEnd = translation != null ? MathF.Sqrt(translation.Trauma / translation.DecayRate) : 0f;
        var rotationEnd = rotation != null ? MathF.Sqrt(rotation.Trauma / rotation.DecayRate) : 0f;
        return start + TimeSpan.FromSeconds(MathF.Max(translationEnd, rotationEnd));
    }

    private float GetTrauma(ScreenshakeParameters parameters, TimeSpan start)
    {
        var elapsed = _timing.CurTime - start;
        if (elapsed < TimeSpan.Zero)
            return 0f;

        var seconds = (float) elapsed.TotalSeconds;
        return MathF.Max(0f, parameters.Trauma - seconds * seconds * parameters.DecayRate);
    }
}

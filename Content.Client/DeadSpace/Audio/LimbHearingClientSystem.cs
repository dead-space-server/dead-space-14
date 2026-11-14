// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Audio;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.StatusEffectNew;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.DeadSpace.Audio;

public sealed class LimbHearingClientSystem : EntitySystem
{
    private const float MuffleOcclusion = 3.5f;
    private const float DeafOcclusion = 20f;

    private static readonly SoundPathSpecifier Ringing = new("/Audio/_DeadSpace/Effects/Heartbeat/ear_ringing.ogg", AudioParams.Default.WithVolume(-8f));

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SpaceMufflingSystem _muffling = default!;
    [Dependency] private readonly AreaEchoSystem _echo = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private LimbHearingState _state = LimbHearingState.Healthy;
    private float _ringIn = 10f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        SubscribeLocalEvent<PlayAmbientMusicEvent>(OnAmbientMusic);
    }

    private void OnAmbientMusic(ref PlayAmbientMusicEvent ev)
    {
        if (_state == LimbHearingState.Deaf)
            ev.Cancelled = true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Apply(LimbHearingState.Healthy);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var state = LimbHearingState.Healthy;
        if (_player.LocalEntity is { } local)
        {
            state = LimbHearing.State(EntityManager, local);
            if (_status.HasEffectComp<DeafStatusEffectComponent>(local))
                state = LimbHearingState.Deaf;
        }
        if (state != _state)
            Apply(state);

        if (_state == LimbHearingState.Deaf)
            Silence();

        if (_state != LimbHearingState.Damaged)
            return;

        _ringIn -= frameTime;
        if (_ringIn > 0f)
            return;

        _ringIn = _random.NextFloat(20f, 45f);
        _audio.PlayGlobal(Ringing, Filter.Local(), false);
    }

    private void Apply(LimbHearingState state)
    {
        _state = state;
        _muffling.HearingOcclusion = state switch
        {
            LimbHearingState.Damaged => MuffleOcclusion,
            LimbHearingState.Deaf => DeafOcclusion,
            _ => 0f,
        };
        _echo.HearingEchoOverride = state == LimbHearingState.Damaged;
        _audioManager.SetMasterGain(state == LimbHearingState.Deaf ? 0f : _cfg.GetCVar(CVars.AudioMasterVolume));
        if (state == LimbHearingState.Deaf)
        {
            Silence();
            EntityManager.System<ContentAudioSystem>().DisableAmbientMusic();
        }
        _ringIn = 3f;
    }

    private void Silence()
    {
        _audioManager.SetMasterGain(0f);
        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (audio.Global)
                _audio.SetGain(uid, 0f, audio);
        }
    }
}

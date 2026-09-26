// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Music;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Music;

/// <summary>
/// Cycles through the loudspeaker's playlist, advancing to the next song
/// whenever the current one finishes playing.
/// </summary>
public sealed class MusicalLoudspeakerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MusicalLoudspeakerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MusicalLoudspeakerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, MusicalLoudspeakerComponent component, MapInitEvent args)
    {
        if (component.Playing && component.Songs.Count > 0)
            PlayCurrent(uid, component);
    }

    private void OnShutdown(EntityUid uid, MusicalLoudspeakerComponent component, ComponentShutdown args)
    {
        StopStream(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MusicalLoudspeakerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Playing || component.Songs.Count == 0)
            {
                if (component.AudioStream is { } idleStream)
                    StopStream(uid, component);
                continue;
            }

            // Something should always be playing while enabled.
            if (component.AudioStream is null)
            {
                PlayCurrent(uid, component);
                continue;
            }

            // Song finished - advance to the next one.
            if (!_audio.IsPlaying(component.AudioStream))
            {
                component.CurrentIndex = (component.CurrentIndex + 1) % component.Songs.Count;
                PlayCurrent(uid, component);
            }
        }
    }

    private void PlayCurrent(EntityUid uid, MusicalLoudspeakerComponent component)
    {
        if (component.Songs.Count == 0)
            return;

        component.CurrentIndex %= component.Songs.Count;
        var song = component.Songs[component.CurrentIndex];

        component.AudioStream = _audio.Stop(component.AudioStream);

        var audioParams = AudioParams.Default
            .WithMaxDistance(component.Range)
            .WithVolume(component.Volume);

        try
        {
            component.AudioStream = _audio.PlayPvs(song, uid, audioParams)?.Entity;
        }
        catch (Exception)
        {
            // Unplayable song (e.g. corrupt file) - skip to the next one and try again.
            component.AudioStream = null;
            component.CurrentIndex = (component.CurrentIndex + 1) % component.Songs.Count;
        }

        Dirty(uid, component);
    }

    private void StopStream(EntityUid uid, MusicalLoudspeakerComponent component)
    {
        if (component.AudioStream is null)
            return;

        component.AudioStream = _audio.Stop(component.AudioStream);
        Dirty(uid, component);
    }
}

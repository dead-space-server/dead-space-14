// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Music;

/// <summary>
/// A wall-mounted loudspeaker that cycles through a list of songs.
/// Each song plays to completion, then the next song begins automatically.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MusicalLoudspeakerComponent : Component
{
    /// <summary>
    /// Playlist of songs to cycle through.
    /// </summary>
    [DataField("songs"), AutoNetworkedField]
    public List<SoundSpecifier> Songs = new();

    /// <summary>
    /// How far away the music can be heard.
    /// </summary>
    [DataField("range")]
    public float Range = 10f;

    /// <summary>
    /// Volume adjustment in dB.
    /// </summary>
    [DataField("volume")]
    public float Volume = -2f;

    /// <summary>
    /// Index of the currently playing song in the playlist.
    /// </summary>
    [DataField("currentIndex"), AutoNetworkedField]
    public int CurrentIndex;

    /// <summary>
    /// Entity UID of the currently playing audio stream. Null when nothing is playing.
    /// </summary>
    [DataField("audioStream")]
    public EntityUid? AudioStream;

    /// <summary>
    /// Whether the loudspeaker is actively playing music.
    /// </summary>
    [DataField("playing"), AutoNetworkedField]
    public bool Playing = true;
}

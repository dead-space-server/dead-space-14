// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.Audio;

[RegisterComponent]
public sealed partial class SpeechAudioComponent : Component
{
    public float EchoGain = 1f;
}

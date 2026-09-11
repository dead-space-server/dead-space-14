// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.GhostRoleIntroduction;

[RegisterComponent]
public sealed partial class GhostRoleIntroductionComponent : Component
{
    [DataField(required: true)]
    public string Text = string.Empty;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Duration = 8f;

    [DataField]
    public float FadeFromBlackDuration = 2.5f;

    [DataField]
    public float TextDelay = 0.75f;

    [DataField]
    public float CharactersPerSecond = 28f;
}

[Serializable, NetSerializable]
public sealed class GhostRoleIntroductionEvent(
    string text,
    float duration,
    float fadeFromBlackDuration,
    float textDelay,
    float charactersPerSecond) : EntityEventArgs
{
    public string Text = text;
    public float Duration = duration;
    public float FadeFromBlackDuration = fadeFromBlackDuration;
    public float TextDelay = textDelay;
    public float CharactersPerSecond = charactersPerSecond;
}
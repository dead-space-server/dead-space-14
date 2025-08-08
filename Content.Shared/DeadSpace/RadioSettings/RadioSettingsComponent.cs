// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.RadioSettings;

/// <summary>
/// This is the component that allows the radio handheld to switch between microphone and speaker modes.
/// </summary>
[RegisterComponent]
public sealed partial class RadioSettingsComponent : Component
{
    public RadioSettingsButton Listening = new(false);

    public RadioSettingsButton Speaking = new(false);
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.RadioSettings;

[Serializable, NetSerializable]
public sealed class RadioSettingsState : BoundUserInterfaceState
{
    public RadioSettingsButton ListeningMode;
    public RadioSettingsButton SpeakingMode;

    public RadioSettingsState(RadioSettingsButton listeningMode, RadioSettingsButton speakingMode)
    {
        ListeningMode = listeningMode;
        SpeakingMode = speakingMode;
    }
}

[Serializable, NetSerializable]
public sealed class RadioSettingsButton
{
    public bool IsToggle { get; set; }

    public RadioSettingsButton(bool isToggle)
    {
        IsToggle = isToggle;
    }
}

[Serializable, NetSerializable]
public sealed class RadioSettingsMessage : BoundUserInterfaceMessage
{
    public RadioMode Mode;
    public RadioSettingsButton IsToggle;

    public RadioSettingsMessage(RadioMode mode, RadioSettingsButton isToggle)
    {
        Mode = mode;
        IsToggle = isToggle;
    }
}

[Serializable, NetSerializable]
public enum RadioSettingsUiKey : byte
{
    Key
}
public enum RadioMode
{
    Listening,
    Speaking
}

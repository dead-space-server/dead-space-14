// Dead Space, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.GhostRoleIntroduction;

[RegisterComponent]
public sealed partial class GhostRoleIntroductionComponent : Component
{
    /// <summary>
    /// A localized dataset has priority over operationNames and operationName.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? OperationNameDataset;

    /// <summary>
    /// Raw operation names declared directly in the entity prototype.
    /// </summary>
    [DataField]
    public List<string> OperationNames = [];

    /// <summary>
    /// A fixed localization key used when no dataset or inline list is specified.
    /// </summary>
    [DataField]
    public string OperationName = string.Empty;

    /// <summary>
    /// Use the name of the active nuclear operative game rule as the operation title.
    /// This keeps the full-screen introduction synchronized with the chat briefing.
    /// </summary>
    [DataField]
    public bool UseNukeopsOperationName;

    /// <summary>
    /// A localized dataset has priority over textLoc and text.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? TextDataset;

    /// <summary>
    /// A fixed localization key for the briefing body.
    /// </summary>
    [DataField]
    public string TextLoc = string.Empty;

    /// <summary>
    /// Raw briefing text declared directly in the entity prototype.
    /// </summary>
    [DataField]
    public string Text = string.Empty;

    [DataField]
    public Color TextColor = Color.White;

    [DataField]
    public string Font = "/Fonts/Bedstead/Bedstead.otf";

    [DataField]
    public int FontSize = 18;

    [DataField]
    public int OperationFontSize = 26;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Duration = 8f;

    [DataField]
    public float FadeFromBlackDuration = 2.5f;

    [DataField]
    public float FadeOutDuration = 1.5f;

    [DataField]
    public float TextDelay = 0.75f;

    [DataField]
    public float CharactersPerSecond = 28f;

    public bool Shown;
}

[Serializable, NetSerializable]
public sealed class GhostRoleIntroductionEvent(
    string operationName,
    string text,
    Color textColor,
    string font,
    int fontSize,
    int operationFontSize,
    float duration,
    float fadeFromBlackDuration,
    float fadeOutDuration,
    float textDelay,
    float charactersPerSecond,
    bool showBlackBackground = true,
    bool typeOperationName = false,
    string senderName = "",
    string senderJobTitle = "",
    int senderFontSize = 14,
    bool targetedAnnouncement = false,
    SoundSpecifier? announcementSound = null,
    SoundSpecifier? interferenceSound = null,
    float interferenceDuration = 1f
    ) : EntityEventArgs
{
    public string OperationName = operationName;
    public string Text = text;
    public Color TextColor = textColor;
    public string Font = font;
    public int FontSize = fontSize;
    public int OperationFontSize = operationFontSize;
    public float Duration = duration;
    public float FadeFromBlackDuration = fadeFromBlackDuration;
    public float FadeOutDuration = fadeOutDuration;
    public float TextDelay = textDelay;
    public float CharactersPerSecond = charactersPerSecond;

    public bool ShowBlackBackground = showBlackBackground;
    public bool TypeOperationName = typeOperationName;
    public string SenderName = senderName;
    public string SenderJobTitle = senderJobTitle;
    public int SenderFontSize = senderFontSize;
    public bool TargetedAnnouncement = targetedAnnouncement;
    public SoundSpecifier? AnnouncementSound = announcementSound;
    public SoundSpecifier? InterferenceSound = interferenceSound;
    public float InterferenceDuration = interferenceDuration;
}

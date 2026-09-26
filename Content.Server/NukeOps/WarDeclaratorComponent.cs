using Content.Server.GameTicking.Rules;
using Content.Shared.NukeOps;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.NukeOps;

/// <summary>
/// Used with NukeOps game rule to send war declaration announcement
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(WarDeclaratorSystem), typeof(NukeopsRuleSystem))]
public sealed partial class WarDeclaratorComponent : Component
{
    /// <summary>
    /// Custom war declaration message. If empty, use default.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public string Message = "war-declarator-default-message";

    /// <summary>
    /// Permission to customize message text
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool AllowEditingMessage = true;

    /// <summary>
    /// War declaration text color
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public Color Color = Color.Red;

    /// <summary>
    /// War declaration sound file path
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/war.ogg");

    /// <summary>
    /// Fluent ID for the declaration sender title
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public LocId SenderTitle = "comms-console-announcement-title-nukie";

    /// <summary>
    /// Time allowed for declaration of war
    /// </summary>
    [DataField]
    public float WarDeclarationDelay = 6.0f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DisableAt;

    /// <summary>
    /// How long the shuttle will be disabled for
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ShuttleDisabledTime;

    [DataField]
    public WarConditionStatus? CurrentStatus;

    // DS14-start
    /// <summary>
    /// Enables the optional targeted full-screen announcement tab.
    /// This does not affect the normal declaration-of-war announcement.
    /// </summary>
    [DataField]
    public bool SecondaryAnnouncementEnabled = false;

    /// <summary>
    /// Entity prototype IDs. A player must be wearing at least one of these in an inventory slot.
    /// Items in hands, pockets, backpacks, or nested containers do not count.
    /// Empty list means there are no recipients.
    /// </summary>
    [DataField]
    public List<string> SecondaryAnnouncementRequiredEquipment = [];

    [DataField]
    public float SecondaryAnnouncementDuration = 10f;

    [DataField]
    public Color SecondaryAnnouncementTextColor = Color.Red;

    [DataField]
    public string SecondaryAnnouncementFont = "/Fonts/Bedstead/Bedstead.otf";

    [DataField]
    public int SecondaryAnnouncementFontSize = 20;

    [DataField]
    public int SecondaryAnnouncementTitleFontSize = 54;

    [DataField]
    public float SecondaryAnnouncementFadeFromBlackDuration = 2.5f;

    [DataField]
    public float SecondaryAnnouncementFadeOutDuration = 1.5f;

    [DataField]
    public float SecondaryAnnouncementTextDelay = 0.75f;

    [DataField]
    public float SecondaryAnnouncementCharactersPerSecond = 28f;

    [DataField]
    public bool SecondaryAnnouncementShowBlackBackground = false;

    [DataField]
    public bool SecondaryAnnouncementTypeTitle = true;

    /// <summary>
    /// Normal sound for the targeted secondary announcement.
    /// Played locally only if there is no already active targeted announcement.
    /// </summary>
    [DataField]
    public SoundSpecifier? SecondaryAnnouncementSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/briefing_warning.ogg");

    /// <summary>
    /// Sound used when this announcement collides with another targeted announcement.
    /// </summary>
    [DataField]
    public SoundSpecifier? SecondaryAnnouncementInterferenceSound =
        new SoundPathSpecifier("/Audio/Effects/multitool_pulse.ogg");

    [DataField]
    public float SecondaryAnnouncementInterferenceDuration = 1f;
    // DS14-end

}

[ByRefEvent]
public record struct WarDeclaredEvent(WarConditionStatus? Status, Entity<WarDeclaratorComponent> DeclaratorEntity);

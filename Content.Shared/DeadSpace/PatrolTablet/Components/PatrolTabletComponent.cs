using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PatrolTablet;

[RegisterComponent, NetworkedComponent]
public sealed partial class PatrolTabletComponent : Component
{
    /// <summary>
    /// Enables squad distribution/tracking UI and related server actions.
    /// When false, this tablet is announcement-only.
    /// </summary>
    [DataField]
    public bool SquadManagementEnabled = true;

    [DataField]
    public List<NetEntity> TrackedPersonnel = new();

    [DataField]
    public List<SquadData> Squads = new();

    /// <summary>
    /// Personal cooldown of this specific tablet after a successful announcement.
    /// Other tablets are not affected by this cooldown.
    /// </summary>
    [DataField]
    public float AnnouncementCooldown = 60f;

    /// <summary>
    /// Total lifetime of the full-screen announcement.
    /// While it is active, only tablets whose AnnouncementRequiredEquipment list intersects
    /// this tablet's recipient equipment are blocked from sending another announcement.
    /// </summary>
    [DataField]
    public float AnnouncementDuration = 10f;

    /// <summary>
    /// Color used for both the large title and the details text.
    /// </summary>
    [DataField]
    public Color AnnouncementTextColor = Color.White;

    [DataField]
    public string AnnouncementFont = "/Fonts/Bedstead/Bedstead.otf";

    [DataField]
    public int AnnouncementFontSize = 20;

    [DataField]
    public int AnnouncementTitleFontSize = 54;

    [DataField]
    public int AnnouncementSenderFontSize = 14;

    [DataField]
    public float AnnouncementFadeFromBlackDuration = 2.5f;

    [DataField]
    public float AnnouncementFadeOutDuration = 1.5f;

    [DataField]
    public float AnnouncementTextDelay = 0.75f;

    [DataField]
    public float AnnouncementCharactersPerSecond = 28f;

    /// <summary>
    /// Whether this announcement should draw the black full-screen background.
    /// Tablet announcements default to false so the screen does not flash black.
    /// </summary>
    [DataField]
    public bool AnnouncementShowBlackBackground = false;

    /// <summary>
    /// Whether the large title should use the same typewriter effect as the details text.
    /// </summary>
    [DataField]
    public bool AnnouncementTypeTitle = true;

    // DS14-start
    [DataField]
    public SoundSpecifier? AnnouncementSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Announcements/briefing_warning.ogg");

    /// <summary>
    /// Sound played when a second targeted announcement collides with an already visible one.
    /// </summary>
    [DataField]
    public SoundSpecifier? AnnouncementInterferenceSound =
        new SoundPathSpecifier("/Audio/Effects/multitool_pulse.ogg");

    /// <summary>
    /// How long the jammed text remains visible before disappearing instantly.
    /// </summary>
    [DataField]
    public float AnnouncementInterferenceDuration = 1f;
    // DS14-end

    /// <summary>
    /// Entity prototype IDs that allow a player to receive tablet announcements while worn in an inventory slot.
    /// An empty list intentionally means there are no recipients.
    /// </summary>
    [DataField]
    public List<string> AnnouncementRequiredEquipment = [];

    /// <summary>
    /// Runtime end time of the personal cooldown for this tablet instance.
    /// </summary>
    public TimeSpan NextAnnouncementTime;

}

[Serializable, NetSerializable]
public sealed class SquadData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IconId { get; set; } = string.Empty;

    public SquadData(string id, string name, string iconId)
    {
        Id = id;
        Name = name;
        IconId = iconId;
    }
}

[Serializable, NetSerializable]
public sealed class PatrolOfficerInfo
{
    public string OfficerId { get; set; }
    public string Name { get; set; }
    public string JobTitle { get; set; }
    public string SquadId { get; set; } = string.Empty;
    public string SquadIcon { get; set; } = "DeadSpaceSquadIconAlpha";

    public PatrolOfficerInfo(
        string officerId,
        string name,
        string jobTitle)
    {
        OfficerId = officerId;
        Name = name;
        JobTitle = jobTitle;
    }
}

[Serializable, NetSerializable]
public sealed class PatrolSquadDef
{
    public string SquadId { get; set; }
    public string Name { get; set; }
    public int AssignedCount { get; set; }
    public string IconId { get; set; }
    public List<string> Members { get; set; } = new();

    public PatrolSquadDef(string squadId, string name, string iconId)
    {
        SquadId = squadId;
        Name = name;
        IconId = iconId;
    }
}

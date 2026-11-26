// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.DeadSpace.UserInterface.Hologram.Options;

/// <summary>
/// Style classes of the escape menu, the settings window and the menu buttons of the lobby. They only carry rules in
/// the "Dead Space" style (<see cref="HologramMenuSheetlet"/>); the other styles ignore them.
/// </summary>
public static class HologramMenuStyleClass
{
    /// <summary>A wide outlined menu row with a left-aligned caption (escape menu).</summary>
    public const string MenuRow = "DeadSpaceHoloMenuRow";

    /// <summary>A destructive action: red frame and caption. Keeps the geometry of an action, a menu row or a plain button.</summary>
    public const string Danger = "DeadSpaceHoloDanger";

    /// <summary>The main action of a group, a brighter projection (apply settings).</summary>
    public const string Primary = "DeadSpaceHoloPrimary";

    /// <summary>A tab strip: uppercase captions, the active tab underlined.</summary>
    public const string Tabs = "DeadSpaceHoloTabs";

    /// <summary>A slim slider: thin track, bright fill, narrow grabber.</summary>
    public const string Slider = "DeadSpaceHoloSlider";

    /// <summary>The numeric read-out next to a slider.</summary>
    public const string SliderValue = "DeadSpaceHoloSliderValue";

    /// <summary>A settings dropdown that fills the rest of its row.</summary>
    public const string DropDown = "DeadSpaceHoloDropDown";

    /// <summary>The footer with the reset/apply buttons of a settings tab.</summary>
    public const string Footer = "DeadSpaceHoloFooter";
}

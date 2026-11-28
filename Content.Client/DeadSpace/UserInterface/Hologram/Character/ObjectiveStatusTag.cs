// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.DeadSpace.UserInterface.Hologram.Medical;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Character;

/// <summary>
/// The status marker of an objective in the character window: "complete" in green, otherwise "in progress" in the
/// projection colour, with the share done when the objective reports one.
/// </summary>
public static class ObjectiveStatusTag
{
    /// <summary>Same bar as SharedObjectivesSystem.IsCompleted.</summary>
    private const float Done = 0.999f;

    public static HoloTag Create(float progress)
    {
        if (progress >= Done)
            return new HoloTag(Loc.GetString("hologram-character-status-done"), DeadSpaceStylePalette.PositiveBorderHover);

        var text = progress > 0f
            ? Loc.GetString("hologram-character-status-progress-percent", ("percent", (int) MathF.Round(progress * 100f)))
            : Loc.GetString("hologram-character-status-progress");
        var color = DeadSpaceStylePalette.Hologram ? DeadSpaceStylePalette.BorderHeader : DeadSpaceStylePalette.CyanBright;
        return new HoloTag(text, color);
    }
}

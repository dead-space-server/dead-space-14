// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Medical;

/// <summary>
/// Colours of the medical scanner. The "Dead Space" style takes them from the projection palette; the other styles keep
/// the colours the scanner had before it, so they look the same as they did.
/// </summary>
public static class MedicalHoloColors
{
    private static bool Holo => DeadSpaceStylePalette.Hologram;

    public static Color Heading => Holo ? DeadSpaceStylePalette.Heading : Color.FromHex("#d7b56d");

    /// <summary>Warnings: amber in every style.</summary>
    public static Color Warning => Holo ? DeadSpaceStylePalette.Amber : Color.FromHex("#d7b56d");

    public static Color Fine => DeadSpaceStylePalette.Text;

    public static Color Hurt => Holo ? Color.FromHex("#FFE25C") : Color.FromHex("#d6c14a");

    public static Color Bad => Holo ? DeadSpaceStylePalette.Amber : Color.FromHex("#e07a2f");

    public static Color Critical => Holo ? DeadSpaceStylePalette.NegativeBorderStrong : Color.FromHex("#c43838");

    public static Color Missing => Holo ? DeadSpaceStylePalette.TextDisabled : Color.FromHex("#6a6a72");

    public static Color Good => Holo ? DeadSpaceStylePalette.PositiveBorderHover : Color.Green;

    public static Color Off => Holo ? DeadSpaceStylePalette.NegativeBorderStrong : Color.Red;

    /// <summary>Colour of a limb scan shade (<see cref="LimbScan"/>), the same steps the body doll is tinted with.</summary>
    public static Color ForShade(byte shade)
    {
        return shade switch
        {
            LimbScan.Missing => Missing,
            LimbScan.Hurt => Hurt,
            LimbScan.Bad => Bad,
            LimbScan.Critical => Critical,
            _ => Fine,
        };
    }

    /// <summary>Colour of a health share from 0 to 1, with the same thresholds as the doll's shades.</summary>
    public static Color ForFraction(float left)
    {
        return left <= 0.34f
            ? Critical
            : left <= 0.67f
                ? Bad
                : left < 0.98f
                    ? Hurt
                    : Good;
    }
}

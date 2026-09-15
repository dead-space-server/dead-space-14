using Content.Client.Stylesheets.Palette;
// DS14-start: shared Dead Space neutral UI palette
using Content.Client.DeadSpace.Stylesheets;
// DS14-end

namespace Content.Client.Stylesheets.Stylesheets;

public sealed partial class NanotrasenStylesheet
{
    // DS14-start: safe dark chrome for interfaces that have not been explicitly migrated yet
    public override ColorPalette PrimaryPalette => DeadSpaceStylePalette.PrimaryPalette;
    public override ColorPalette SecondaryPalette => DeadSpaceStylePalette.SecondaryPalette;
    // DS14-end
    public override ColorPalette PositivePalette => Palettes.Green;
    public override ColorPalette NegativePalette => Palettes.Red;
    public override ColorPalette HighlightPalette => Palettes.Gold;
}

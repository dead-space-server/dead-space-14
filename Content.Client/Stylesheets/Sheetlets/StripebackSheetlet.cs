using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.DeadSpace.Stylesheets; // DS14
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class StripebackSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IStripebackConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IStripebackConfig stripebackCfg = sheet;

        // DS14-start: the stock diagonal texture becomes a black frame on the light palette.
        // Classic keeps the original Wizden texture; modern themes use a quiet tonal footer.
        StyleBox stripeBack;
        if (DeadSpaceStylePalette.ClassicChrome)
        {
            stripeBack = new StyleBoxTexture
            {
                Texture = sheet.GetTextureOr(stripebackCfg.StripebackPath, NanotrasenStylesheet.TextureRoot),
                Mode = StyleBoxTexture.StretchMode.Tile,
            };
        }
        else
        {
            stripeBack = new StyleBoxFlat
            {
                BackgroundColor = DeadSpaceStylePalette.SurfaceHeader,
            };
        }
        // DS14-end

        return
        [
            E<StripeBack>()
                .Prop(StripeBack.StylePropertyBackground, stripeBack),
        ];
    }
}

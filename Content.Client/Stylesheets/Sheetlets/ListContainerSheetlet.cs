using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class ListContainerSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig, IIconConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        // DS14-start: never use a white backing box; define every interactive state explicitly
        IButtonConfig buttonConfig = sheet;
        var normal = new StyleBoxFlat
        {
            BackgroundColor = buttonConfig.ButtonPalette.Element,
        };
        var hovered = new StyleBoxFlat(normal)
        {
            BackgroundColor = buttonConfig.ButtonPalette.HoveredElement,
        };
        var pressed = new StyleBoxFlat(normal)
        {
            BackgroundColor = buttonConfig.ButtonPalette.PressedElement,
        };
        var disabled = new StyleBoxFlat(normal)
        {
            BackgroundColor = buttonConfig.ButtonPalette.DisabledElement,
        };

        return
        [
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoNormal()
                .Box(normal)
                .Modulate(Color.White),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoHovered()
                .Box(hovered)
                .Modulate(Color.White),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoPressed()
                .Box(pressed)
                .Modulate(Color.White),
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .PseudoDisabled()
                .Box(disabled)
                .Modulate(Color.White),
        ];
        // DS14-end
    }
}

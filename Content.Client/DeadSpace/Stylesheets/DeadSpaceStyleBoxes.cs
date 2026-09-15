// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Stylesheets;

internal static class DeadSpaceStyleBoxes
{
    public static StyleBoxFlat Flat(
        Color background,
        Color? border = null,
        Thickness? thickness = null,
        float horizontalMargin = 0,
        float verticalMargin = 0)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? Color.Transparent,
            BorderThickness = thickness ?? new Thickness(0),
        };

        if (horizontalMargin != 0)
        {
            box.ContentMarginLeftOverride = horizontalMargin;
            box.ContentMarginRightOverride = horizontalMargin;
        }

        if (verticalMargin != 0)
        {
            box.ContentMarginTopOverride = verticalMargin;
            box.ContentMarginBottomOverride = verticalMargin;
        }

        return box;
    }
}

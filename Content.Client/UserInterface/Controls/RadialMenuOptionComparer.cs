// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Client.UserInterface.Controls;

/// <summary>
/// Orders radial menu options by explicit order first, then by tooltip.
/// Null order and tooltip values are placed last.
/// </summary>
public sealed class RadialMenuOptionComparer : IComparer<RadialMenuOptionBase>
{
    public int Compare(RadialMenuOptionBase? x, RadialMenuOptionBase? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (y == null)
            return -1;

        if (x == null)
            return 1;

        if (x.Order != y.Order)
        {
            if (y.Order is null)
                return -1;

            if (x.Order is null)
                return 1;

            return x.Order < y.Order ? -1 : 1;
        }

        if (x.ToolTip is null && y.ToolTip is null)
            return 0;

        if (y.ToolTip is null)
            return -1;

        if (x.ToolTip is null)
            return 1;

        return string.Compare(x.ToolTip, y.ToolTip, StringComparison.Ordinal);
    }
}

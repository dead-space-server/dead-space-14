// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Client.VendingMachines.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Vending;

/// <summary>
/// The goods of a vending machine as a scrolling grid of cards, used instead of the plain list in the "Dead Space"
/// style. Cards are only built while the grid is in use, and every one is the same size, so a long name, an odd
/// sprite or a hundred items can't break the layout; the column count follows the window width.
/// </summary>
public sealed class HologramVendingGrid : ScrollContainer
{
    public const float CardWidth = 150f;
    /// <summary>Approximate card height, for picking a window size before anything is measured.</summary>
    public const float CardHeight = 118f;
    public const float CardSeparation = 6f;

    private readonly HologramCardGrid _grid;
    private readonly List<HologramVendingCard> _cards = new();
    private readonly HologramStyleStamp _stamp = new();
    private string _filter = string.Empty;

    /// <summary>A key went down on an available card; same contract as <see cref="ListContainer.ItemKeyBindDown"/>.</summary>
    public event Action<GUIBoundKeyEventArgs, ListData>? OnItemKeyBindDown;

    public HologramVendingGrid()
    {
        HScrollEnabled = false;
        HorizontalExpand = true;
        VerticalExpand = true;

        _grid = new HologramCardGrid
        {
            MinCardWidth = CardWidth,
            Separation = CardSeparation,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 4, 0),
        };

        AddChild(_grid);
    }

    /// <summary>A window size that shows up to four columns and three rows of <paramref name="count"/> cards.</summary>
    public static Vector2 PreferredWindowSize(int count, Vector2 chrome)
    {
        var columns = Math.Clamp(count, 2, 4);
        var rows = Math.Clamp((int) MathF.Ceiling(count / (float) columns), 1, 3);

        return chrome + new Vector2(
            columns * CardWidth + (columns - 1) * CardSeparation + 16f,
            rows * CardHeight + (rows - 1) * CardSeparation);
    }

    public void Populate(
        IEnumerable<(ListData Data, VendorItemKey Key, string Name)> items,
        IReadOnlyDictionary<VendorItemKey, uint> amounts,
        bool enabled)
    {
        Clear();

        foreach (var (data, key, name) in items)
        {
            var card = new HologramVendingCard(data, key, name);
            card.SetAmount(amounts.GetValueOrDefault(key), enabled);
            card.OnKeyBindDown += args =>
            {
                if (!card.Disabled)
                    OnItemKeyBindDown?.Invoke(args, card.Data);
            };

            _cards.Add(card);
            _grid.AddChild(card);
        }

        ApplyFilter();
    }

    public void UpdateAmounts(IReadOnlyDictionary<VendorItemKey, uint> amounts, bool enabled)
    {
        foreach (var card in _cards)
        {
            card.SetAmount(amounts.GetValueOrDefault(card.Key), enabled);
        }
    }

    /// <summary>Removes every card; their preview entities go with them.</summary>
    public void Clear()
    {
        _cards.Clear();
        _grid.RemoveAllChildren();
    }

    public void SetFilter(string filter)
    {
        _filter = filter.Trim();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        foreach (var card in _cards)
        {
            card.Visible = _filter.Length == 0 ||
                           card.ItemName.Contains(_filter, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_stamp.Update())
            return;

        foreach (var card in _cards)
        {
            card.RefreshStyle();
        }
    }
}

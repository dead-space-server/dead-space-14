// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Comms;

/// <summary>
/// Alert levels of the communications console as a row of buttons, for the "Dead Space" style. It only mirrors the
/// console's level dropdown, which stays the single source of the selection: a pressed button selects that item and the
/// console's confirm button still sends the change, so nothing changes in what reaches the server.
/// </summary>
public sealed class HologramAlertLevelRow : BoxContainer
{
    private readonly WrapContainer _levels;
    private readonly List<(int Index, Button Button)> _buttons = [];

    /// <summary>A level button was pressed; the argument is the item index in the mirrored dropdown.</summary>
    public event Action<int>? LevelPressed;

    public HologramAlertLevelRow()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 10;
        HorizontalExpand = true;

        AddChild(new Label
        {
            Text = Loc.GetString("hologram-comms-alert-level"),
            StyleClasses = { DeadSpaceStyleClass.Subtitle },
            VerticalAlignment = VAlignment.Center,
        });

        _levels = new WrapContainer
        {
            HorizontalExpand = true,
            SeparationOverride = 6,
            CrossSeparationOverride = 6,
        };
        AddChild(_levels);
    }

    /// <summary>
    /// Rebuilds the buttons. <paramref name="levels"/> are the dropdown items that hold a level: their index in the
    /// dropdown, the level ID and its localized name.
    /// </summary>
    public void Update(
        IReadOnlyList<(int Index, string Level, string Name)> levels,
        string? currentLevel,
        Color currentColor,
        bool selectable,
        int selectedIndex)
    {
        _levels.RemoveAllChildren();
        _buttons.Clear();

        foreach (var (index, level, name) in levels)
        {
            var current = level == currentLevel;
            var button = new Button
            {
                Text = name,
                ToggleMode = true,
                // The level in force is shown as a locked selection: it can't be picked again.
                Disabled = !selectable || current,
                MinWidth = 90,
            };

            if (current)
            {
                button.AddStyleClass(DeadSpaceStyleClass.LockedSelected);
                button.Label.FontColorOverride = currentColor;
            }

            button.OnPressed += _ => LevelPressed?.Invoke(index);
            _buttons.Add((index, button));
            _levels.AddChild(button);
        }

        SyncSelection(selectedIndex);
    }

    /// <summary>Presses the button of the selected dropdown item and releases the others.</summary>
    public void SyncSelection(int selectedIndex)
    {
        foreach (var (index, button) in _buttons)
        {
            button.Pressed = index == selectedIndex;
        }
    }
}

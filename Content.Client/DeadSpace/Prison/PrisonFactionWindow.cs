using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.DeadSpace.Prison;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.DeadSpace.Prison;

public sealed class PrisonFactionWindow : DefaultWindow
{
    public event Action<string>? OnFactionConfirmed;

    private readonly BoxContainer _factions;
    private readonly Button _confirm;
    private readonly Label _timer;
    private PrisonFactionRow? _selectedRow;
    private string? _selectedFaction;

    public PrisonFactionWindow()
    {
        Title = Loc.GetString("prison-faction-window-title");
        MinSize = new Vector2(540, 290);
        SetSize = new Vector2(580, 310);
        Resizable = false;
        CloseButton.Visible = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(12),
        };

        _timer = new Label
        {
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalAlignment = HAlignment.Center,
            MinHeight = 24,
        };
        root.AddChild(_timer);

        _factions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
        };
        root.AddChild(_factions);

        _confirm = new Button
        {
            Text = Loc.GetString("prison-faction-window-confirm"),
            Disabled = true,
            MinSize = new Vector2(220, 36),
            HorizontalAlignment = HAlignment.Center,
        };
        _confirm.OnPressed += _ =>
        {
            if (_selectedFaction != null)
                OnFactionConfirmed?.Invoke(_selectedFaction);
        };
        root.AddChild(_confirm);
        Contents.AddChild(root);
    }

    public void UpdateState(PrisonFactionEuiState state)
    {
        _timer.Text = Loc.GetString(
            "prison-faction-window-timer",
            ("seconds", state.SecondsRemaining));

        if (_factions.ChildCount != 0)
            return;

        foreach (var option in state.Factions)
        {
            var row = new PrisonFactionRow(option);
            row.OnSelected += OnRowSelected;
            _factions.AddChild(row);
        }
    }

    private void OnRowSelected(PrisonFactionRow row)
    {
        _selectedRow?.SetSelected(false);
        _selectedRow = row;
        _selectedFaction = row.FactionId;
        row.SetSelected(true);
        _confirm.Disabled = false;
    }

    private sealed class PrisonFactionRow : ContainerButton
    {
        public event Action<PrisonFactionRow>? OnSelected;
        public string FactionId { get; }
        public string FactionName { get; }

        public PrisonFactionRow(PrisonFactionOption option)
        {
            FactionId = option.Id;
            FactionName = Loc.GetString(option.Name);
            ToggleMode = true;
            HorizontalExpand = true;
            VerticalExpand = true;
            MinHeight = 76;

            var content = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                VerticalExpand = true,
                SeparationOverride = 10,
                Margin = new Thickness(8, 6),
            };

            content.AddChild(new PanelContainer
            {
                MinWidth = 4,
                VerticalExpand = true,
                PanelOverride = new StyleBoxFlat { BackgroundColor = option.Color },
            });

            var text = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                SeparationOverride = 3,
            };
            text.AddChild(new Label
            {
                Text = FactionName,
                StyleClasses = { StyleClass.LabelHeading },
                FontColorOverride = option.Color,
            });

            var feature = new RichTextLabel
            {
                HorizontalExpand = true,
                MinHeight = 34,
                StyleClasses = { StyleClass.LabelSubText },
            };
            feature.SetMessage(Loc.GetString(option.Feature));
            text.AddChild(feature);
            content.AddChild(text);
            AddChild(content);

            OnPressed += _ => OnSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            Pressed = selected;
        }
    }
}

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.Lavaland.Bosses;

public sealed class LavalandBossHudControl : PanelContainer
{
    private readonly Label _bossName;
    private readonly Label _participants;
    private readonly Label _healthText;
    private readonly ProgressBar _healthBar;

    public LavalandBossHudControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalAlignment = HAlignment.Center;
        MinSize = new Vector2(280, 48);
        AddStyleClass(DeadSpaceStyleClass.SurfaceDark);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            // SurfaceDark contributes three pixels; retain the original 10x6 effective padding.
            Margin = new Thickness(7, 3),
        };
        AddChild(root);

        var titleRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        root.AddChild(titleRow);

        _bossName = new Label
        {
            Text = "Boss",
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = Color.FromHex("#f0f0f0"),
        };
        titleRow.AddChild(_bossName);

        _participants = new Label
        {
            Text = Loc.GetString("lavaland-boss-hud-participants", ("count", 0)),
            Align = Label.AlignMode.Right,
            FontColorOverride = Color.FromHex("#b8b8c0"),
        };
        titleRow.AddChild(_participants);

        _healthBar = new ProgressBar
        {
            HorizontalExpand = true,
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            SetHeight = 10,
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = DeadSpaceStylePalette.NegativeBorder },
        };
        root.AddChild(_healthBar);

        _healthText = new Label
        {
            Align = Label.AlignMode.Center,
            Text = "0 / 0",
            FontColorOverride = Color.FromHex("#d8d8df"),
        };
        root.AddChild(_healthText);
    }

    public void UpdateState(string bossName, float currentHealth, float maxHealth, int participants)
    {
        maxHealth = MathF.Max(1f, maxHealth);
        currentHealth = Math.Clamp(currentHealth, 0f, maxHealth);

        _bossName.Text = bossName;
        _participants.Text = Loc.GetString("lavaland-boss-hud-participants", ("count", participants));
        _healthBar.Value = currentHealth / maxHealth;
        _healthText.Text = $"{MathF.Ceiling(currentHealth)} / {MathF.Ceiling(maxHealth)}";
    }
}

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Silicons.AiBorgControl;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Silicons.AiBorgControl;

public sealed class AiBorgControlWindow : FancyWindow
{
    public event Action<NetEntity>? OnTake;

    private readonly BoxContainer _list = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 6,
        Margin = new Thickness(8)
    };

    public AiBorgControlWindow()
    {
        Title = Loc.GetString("ai-borg-control-title");
        MinSize = new Vector2(520, 300);
        SetSize = new Vector2(620, 420);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        ContentsContainer.AddChild(scroll);
    }

    public void Update(AiBorgControlState state)
    {
        _list.RemoveAllChildren();

        if (state.Borgs.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("ai-borg-control-empty") });
            return;
        }

        foreach (var b in state.Borgs)
        {
            var top = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8
            };
            top.AddChild(new Label
            {
                Text = $"{b.Name} ({b.ChassisName})",
                HorizontalExpand = true,
                ClipText = true
            });

            var btn = new Button
            {
                Text = Loc.GetString("ai-borg-control-take"),
                Disabled = b.Status != AiBorgStatus.Ready
            };
            var net = b.Borg;
            btn.OnPressed += _ => OnTake?.Invoke(net);
            top.AddChild(btn);

            var hp = Loc.GetString("ai-borg-control-hp", ("hp", (int)(b.HpPercent * 100)));
            var charge = Loc.GetString("ai-borg-control-charge", ("charge", (int)(b.Charge * 100)));
            var modules = Loc.GetString("ai-borg-control-modules", ("count", b.ModuleCount));
            var status = Loc.GetString($"ai-borg-control-status-{b.Status.ToString().ToLowerInvariant()}");

            var stats = new Label
            {
                Text = $"{hp}  |  {charge}  |  {modules}  |  {status}",
                Modulate = Color.FromHex("#9a9aa8")
            };

            var content = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 4,
                Margin = new Thickness(8, 6)
            };
            content.AddChild(top);
            content.AddChild(stats);

            var card = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#2b2b38") }
            };
            card.AddChild(content);

            _list.AddChild(card);
        }
    }
}

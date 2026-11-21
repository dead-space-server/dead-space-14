using System;
using System.Diagnostics.CodeAnalysis;
using Content.Client.Paper.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;
using static Content.Shared.Paper.PaperComponent;
using static Content.Shared.Paper.PaperSystem;

namespace Content.Client.DeadSpace.UserInterface.RichText;

public sealed class CheckTagHandler : IMarkupTagHandler
{
    private static readonly Color InkColor = new(25, 25, 25);

    public string Name => "check";

    public static bool Interactive = true;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var state = GetNodeState(node);
        var glyph = CheckGlyphs[(int) state];

        if (!Interactive)
        {
            control = new Label
            {
                Text = glyph,
                Margin = new Thickness(1, 0, 1, 0),
                ModulateSelfOverride = InkColor,
            };
            return true;
        }

        var btn = new PaperCheckButton(state)
        {
            Text = glyph,
            Margin = new Thickness(1, 0, 1, 0),
        };

        btn.StyleBoxOverride = new StyleBoxEmpty();
        btn.Label.FontColorOverride = InkColor;

        btn.OnPressed += _ =>
        {
            if (FindPaperWindow(btn) is { } window)
            {
                var next = btn.State == PaperCheckState.Blank
                    ? PaperCheckState.Check
                    : PaperCheckState.Blank;
                window.SetCheckState(CountPaperCheckButtonsBefore(btn), next);
            }
        };

        control = btn;
        return true;
    }

    private static PaperCheckState GetNodeState(MarkupNode node)
    {
        var value = node.Value.LongValue ?? (long) PaperCheckState.Blank;
        return (PaperCheckState) Math.Clamp(value, (long) PaperCheckState.Blank, CheckGlyphs.Length - 1);
    }

    private static PaperWindow? FindPaperWindow(Control control)
    {
        var parent = control.Parent;
        while (parent != null && parent is not PaperWindow)
            parent = parent.Parent;

        return parent as PaperWindow;
    }

    private static int CountPaperCheckButtonsBefore(Control clickedButton)
    {
        if (FindPaperWindow(clickedButton) is not { } window)
            return 0;

        var count = 0;
        var found = false;
        CountPaperCheckButtonsRecursive(window, clickedButton, ref count, ref found);
        return found ? count : 0;
    }

    private static void CountPaperCheckButtonsRecursive(Control control, Control target, ref int count, ref bool found)
    {
        if (found)
            return;

        if (control is PaperCheckButton)
        {
            if (control == target)
            {
                found = true;
                return;
            }

            count++;
        }

        foreach (var child in control.Children)
        {
            CountPaperCheckButtonsRecursive(child, target, ref count, ref found);
        }
    }
}

public sealed class PaperCheckButton : Button
{
    public PaperCheckState State { get; }

    public PaperCheckButton(PaperCheckState state)
    {
        State = state;
    }
}

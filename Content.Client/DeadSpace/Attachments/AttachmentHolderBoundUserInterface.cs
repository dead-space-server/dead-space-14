// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Attachments;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.DeadSpace.Attachments;

/// <summary>Small menu (verb "Attachments") to put attachments on and take them off slot by slot.</summary>
[UsedImplicitly]
public sealed class AttachmentHolderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AttachmentHolderWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AttachmentHolderWindow>();
        _window.Title = Loc.GetString("attachment-menu-title");
        _window.OnInsert += slot => SendMessage(new AttachmentInsertMessage(slot));
        _window.OnEject += slot => SendMessage(new AttachmentEjectMessage(slot));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is AttachmentHolderBuiState holder)
            _window?.SetSlots(holder.Slots);
    }
}

public sealed class AttachmentHolderWindow : DefaultWindow
{
    private readonly BoxContainer _rows;

    public event Action<string>? OnInsert;
    public event Action<string>? OnEject;

    public AttachmentHolderWindow()
    {
        MinSize = new Vector2(360, 120);
        SetSize = new Vector2(420, 260);
        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        Contents.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _rows },
        });
    }

    public void SetSlots(List<AttachmentSlotState> slots)
    {
        _rows.RemoveAllChildren();
        foreach (var slot in slots)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 2),
            };

            row.AddChild(new Label
            {
                Text = slot.Name,
                MinSize = new Vector2(110, 0),
                FontColorOverride = Color.FromHex("#ffb56b"),
            });
            row.AddChild(new Label
            {
                Text = slot.Item ?? Loc.GetString("attachment-menu-empty"),
                HorizontalExpand = true,
                ClipText = true,
                FontColorOverride = slot.Item == null ? Color.Gray : Color.White,
            });

            if (slot.Item != null)
            {
                var eject = new Button { Text = Loc.GetString("attachment-menu-eject") };
                var id = slot.SlotId;
                eject.OnPressed += _ => OnEject?.Invoke(id);
                row.AddChild(eject);
            }
            else
            {
                var insert = new Button
                {
                    Text = Loc.GetString("attachment-menu-insert"),
                    Disabled = !slot.CanInsert,
                };
                var id = slot.SlotId;
                insert.OnPressed += _ => OnInsert?.Invoke(id);
                row.AddChild(insert);
            }

            _rows.AddChild(row);
        }
    }
}

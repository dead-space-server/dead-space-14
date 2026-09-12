// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface;

/// <summary>Marks the focused input without traversing the UI tree or overriding its style box.</summary>
public sealed class DeadSpaceInputFocusUIController : UIController
{
    private LineEdit? _focused;

    public override void FrameUpdate(FrameEventArgs args)
    {
        var focused = UIManager.KeyboardFocused is LineEdit { Editable: true } edit &&
                      !edit.HasStyleClass(ChatInputBox.StyleClassChatLineEdit) ? edit : null;
        if (ReferenceEquals(_focused, focused))
            return;

        _focused?.RemoveStyleClass(DeadSpaceStyleClass.InputFocused);
        _focused = focused;
        _focused?.AddStyleClass(DeadSpaceStyleClass.InputFocused);
    }
}

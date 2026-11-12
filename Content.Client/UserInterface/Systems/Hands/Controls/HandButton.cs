using Content.Client.UserInterface.Controls;
using Content.Shared.Hands.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Hands.Controls;

public sealed class HandButton : SlotControl
{
    public HandLocation HandLocation { get; }

    public HandButton(string handName, HandLocation handLocation)
    {
        HandLocation = handLocation;
        Name = "hand_" + handName;
        SlotName = handName;
        SetBackground(handLocation);

        // DS14-start
        // Preserve fixed 2x hand-slot rendering for padded 64x64 weapon sprites.
        foreach (var child in Children)
        {
            if (child is SpriteView spriteView)
                spriteView.Stretch = SpriteView.StretchMode.None;
        }
        // DS14-end
    }

    private void SetBackground(HandLocation handLoc)
    {
        ButtonTexturePath = handLoc switch
        {
            HandLocation.Left => "Slots/hand_l",
            HandLocation.Middle => "Slots/hand_m",
            HandLocation.Right => "Slots/hand_r",
            _ => ButtonTexturePath
        };
    }
}

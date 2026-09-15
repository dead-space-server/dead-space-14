using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// Makes a clothing item disappear when it is unequipped or otherwise removed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FleetingClothingComponent : Component
{
    [DataField]
    public SoundSpecifier? RemovedSound;

    [DataField]
    public bool PlaySoundOnSelfUnequip = true;

    [DataField]
    public LocId? SelfUnquipPopupWearer = "fleeting-clothing-component-default-popup";

    [DataField]
    public LocId? SelfUnquipPopupOthers = "fleeting-clothing-component-default-popup";

    [DataField]
    public LocId? RemovedPopup = "fleeting-clothing-component-default-popup";

    [DataField]
    public LocId? ExamineWearer = "fleeting-clothing-component-default-examine";

    [DataField]
    public LocId? ExamineOthers = "fleeting-clothing-component-default-examine";

    [DataField]
    public bool DestroyOnUnequip = true;
}

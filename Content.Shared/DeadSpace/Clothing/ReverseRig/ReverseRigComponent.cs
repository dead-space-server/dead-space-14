// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Clothing.ReverseRig;

/// <summary>
///     The Reverse RIG (СОР) system. Unlike a standard RIG - a backpack that unfolds into a suit - this
///     component is placed on the suit itself. While the suit is worn it automatically equips its attached
///     backpack into the back slot, displacing whatever previously occupied that slot when normal unequip rules
///     permit it. The backpack can not be removed manually and is stowed back inside the suit when the suit is taken off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReverseRigComponent : Component
{
    public const string DefaultBackpackContainerId = "reverse-rig-backpack";

    /// <summary>
    ///     Prototype of the backpack that is spawned inside the suit while the suit is on a map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId BackpackPrototype = "ClothingBackpackRIGReverse";

    /// <summary>
    ///     Container on the suit that holds the backpack while the suit is not worn.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string BackpackContainerId = DefaultBackpackContainerId;

    /// <summary>
    ///     Inventory slot on the wearer that the backpack is equipped to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Slot = "back";

    /// <summary>
    ///     Slot flags required for this component to function. The suit must be worn in outer clothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags RequiredFlags = SlotFlags.OUTERCLOTHING;

    /// <summary>
    ///     The backpack entity attached to this suit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? BackpackUid;

    /// <summary>
    ///     Container that holds the backpack while it is not equipped.
    /// </summary>
    [ViewVariables]
    public ContainerSlot? BackpackContainer;
}

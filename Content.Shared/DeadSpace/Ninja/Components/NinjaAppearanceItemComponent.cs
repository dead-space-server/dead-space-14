using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedNinjaAppearanceSystem))]
public sealed partial class NinjaAppearanceItemComponent : Component
{
    [DataField]
    public NinjaAppearanceItemType ItemType = NinjaAppearanceItemType.Color;

    [DataField, AutoNetworkedField]
    public NinjaColorway? FrozenColor;

    [DataField, AutoNetworkedField]
    public NinjaStyle? FrozenStyle;

    [ViewVariables]
    public Dictionary<HumanoidVisualLayers, SlotFlags>? SavedHideLayers;
}
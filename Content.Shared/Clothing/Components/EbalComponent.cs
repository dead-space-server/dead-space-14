using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Light.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EbalComponent : Component
{
    [DataField("turnOnSound")]
    public SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");

    [DataField("toggleAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ToggleAction = "ActionToggleLight";

    /// <summary>
    /// Whether or not the light can be toggled via standard interactions
    /// (alt verbs, using in hand, etc)
    /// </summary>
    [DataField("toggleOnInteract")]
    public bool ToggleOnInteract = true;

    [DataField("toggleActionEntity")]
    public EntityUid? ToggleActionEntity;

    [DataField]
    public EntityUid? SelfToggleActionEntity;

    [DataField("requiredSlot"), AutoNetworkedField]
    public SlotFlags RequiredFlags = SlotFlags.HEAD;
    public DoAfterId? s;

}
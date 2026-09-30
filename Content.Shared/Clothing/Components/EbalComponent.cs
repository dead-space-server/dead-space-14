using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Light.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EbalComponent : Component
{
    [DataField]
    public FixedPoint2? TransferAmount = FixedPoint2.New(5);
    [DataField]
    public string Solution = "drink";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1f);

    [DataField]
    public SoundSpecifier? UseSound;

    /// <summary>
    /// Verb, icon, and sound data for our edible.
    /// </summary>
    [DataField]
    public ProtoId<EdiblePrototype> Edible = IngestionSystem.Drink;

    [DataField("Action", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]

    public string Action = "ActionEat";
    [DataField("ActionEntity")]
    public EntityUid? ActionEntity;

    [DataField("requiredSlot"), AutoNetworkedField]
    public SlotFlags RequiredFlags = SlotFlags.HEAD;

}
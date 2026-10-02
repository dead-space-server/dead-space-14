using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.DeadSpace.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BeerHatComponent : Component
{
    [DataField]
    public FixedPoint2? TransferAmount = FixedPoint2.New(5);
    [DataField]
    public string Solution = "drink";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1f);

    [DataField]
    public SoundSpecifier? UseSound;

    [DataField]
    public ProtoId<EdiblePrototype> Edible = IngestionSystem.Drink;

    [DataField("Action", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]

    public string Action = "ActionDrinkFromBeerHat";
    [DataField("ActionEntity")]
    public EntityUid? ActionEntity;

    [DataField("requiredSlot"), AutoNetworkedField]
    public SlotFlags RequiredFlags = SlotFlags.HEAD;

}
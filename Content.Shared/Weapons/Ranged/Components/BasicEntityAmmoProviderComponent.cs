using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
///     Simply provides a certain capacity of entities that cannot be reloaded through normal means and have
///     no special behavior like cycling, magazine
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)] // DS14: the client redraws the counter on a recharge
public sealed partial class BasicEntityAmmoProviderComponent : AmmoProviderComponent
{
<<<<<<< ours
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("proto", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<EntityPrototype>))]
    [AutoNetworkedField] // DS14: added at runtime (a crusher capsule) or switched (fire modes), the client must know it
    public string Proto = default!;
||||||| base
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("proto", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Proto = default!;
=======
    [DataField(required: true)]
    public EntProtoId Proto = default!;
>>>>>>> theirs

    /// <summary>
    ///     Max capacity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? Capacity = null;

    /// <summary>
    ///     Actual ammo left. Initialized to capacity unless they are non-null and differ.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? Count = null;
}

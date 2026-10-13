using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Utility; // DS14

namespace Content.Shared.Weapons.Marker;

/// <summary>
/// Applies <see cref="DamageMarkerComponent"/> when colliding with an entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedDamageMarkerSystem))]
public sealed partial class DamageMarkerOnCollideComponent : Component
{
    [DataField("whitelist"), AutoNetworkedField]
    public EntityWhitelist? Whitelist = new();

    [ViewVariables(VVAccess.ReadWrite), DataField("duration"), AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Additional damage to be applied.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// How many more times we can apply it.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("amount"), AutoNetworkedField]
    public int Amount = 1;
    // DS14-start
    /// <summary>
    /// Sprite to apply to the entity when damagemark is applied and overrides default sprite
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("effect")]
    public SpriteSpecifier.Rsi? Effect;
    // DS14-end
}

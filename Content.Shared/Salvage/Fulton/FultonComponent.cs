using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Salvage.Fulton;

/// <summary>
/// Applies <see cref="FultonedComponent"/> to the target so they teleport to <see cref="FultonBeaconComponent"/> after a time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FultonComponent : Component
{
    /// <summary>
    /// How long it takes to apply the fulton to an entity.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("applyDuration"), AutoNetworkedField]
    public TimeSpan ApplyFultonDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Linked fulton beacon.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("beacon"), AutoNetworkedField]
    public EntityUid? Beacon;

    // DS14-start
    /// <summary>
    /// Stacks of fultons linked to different beacons, merged: the first fultons of the stack go to these beacons, in
    /// order, and the rest to <see cref="Beacon"/>.
    /// </summary>
    [ViewVariables, DataField, AutoNetworkedField]
    public List<EntityUid> BeaconQueue = new(); // EntityUid.Invalid - no beacon
    // DS14-end

    /// <summary>
    /// Applies Removeable to the <see cref="FultonedComponent"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("removeable"), AutoNetworkedField]
    public bool Removeable = true;

    /// <summary>
    /// How long the fulton will remain before teleporting to the beacon.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("duration")]
    public TimeSpan FultonDuration = TimeSpan.FromSeconds(45);

    [ViewVariables(VVAccess.ReadWrite), DataField("whitelist"), AutoNetworkedField]
    public EntityWhitelist? Whitelist = new()
    {
        Components = new[]
        {
            "Item",
            "Anchorable"
        }
    };

    /// <summary>
    /// Sound that gets played when fulton is applied.
    /// </summary>
    /// <returns></returns>
    [ViewVariables(VVAccess.ReadWrite), DataField("soundFulton"), AutoNetworkedField]
    public SoundSpecifier? FultonSound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_deploy.ogg");
}

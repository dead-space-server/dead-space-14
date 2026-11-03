// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryInfectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<LimbZone> Rotting = new();

    [DataField, AutoNetworkedField]
    public List<LimbZone> Latent = new();

    [DataField]
    public List<SurgeryInfection> Pending = new();

    [DataField]
    public TimeSpan NextRot;

    public bool Rots(LimbZone zone)
    {
        return Rotting.Contains(zone);
    }
}

[DataDefinition, Serializable, NetSerializable]
public partial struct SurgeryInfection
{
    [DataField]
    public LimbZone Zone;

    [DataField]
    public TimeSpan At;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryProneComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryRejectionComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<LimbZone> Zones = new();

    [DataField]
    public List<SurgeryRejection> Entries = new();
}

[DataDefinition, Serializable, NetSerializable]
public partial struct SurgeryRejection
{
    [DataField]
    public LimbZone Zone;

    [DataField]
    public TimeSpan Since;

    [DataField]
    public string Mutagen;

    [DataField]
    public string? Organ;

    [DataField]
    public string? Appendage;

    [DataField]
    public bool Shocked;

    [DataField]
    public bool Black;

    [DataField]
    public float Stage;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryPartsGoneComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> Parts = new();
}

[ByRefEvent]
public record struct SurgeryEffectEvent(EntityUid User, EntityUid? Tool, string Effect, bool Handled = false);

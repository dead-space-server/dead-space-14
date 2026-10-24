// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.OrganDamage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganDamageableComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxDamage = 300;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Damage = FixedPoint2.Zero;

    [DataField, AutoNetworkedField]
    public bool Dead;

    [DataField, AutoNetworkedField]
    public Dictionary<string, FixedPoint2> DamageMultipliers = new();

    [DataField]
    public string? SurgerySlotId;

    [DataField, AutoNetworkedField]
    public Dictionary<string, float> DeadBodyDamagePerSecond = new();

    [DataField]
    public List<OrganWound> Wounds = new();

    [DataField]
    public int MaxWounds = 60;
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class OrganWound
{
    [DataField]
    public Guid Id { get; private set; }

    [DataField]
    public TimeSpan Time { get; private set; }

    [DataField]
    public string DamageType { get; private set; } = default!;

    [DataField]
    public FixedPoint2 Amount { get; private set; }

    [DataField]
    public string? Source { get; private set; }

    public OrganWound(Guid id, TimeSpan time, string damageType, FixedPoint2 amount, string? source)
    {
        Id = id;
        Time = time;
        DamageType = damageType;
        Amount = amount;
        Source = source;
    }

    public void Merge(FixedPoint2 amount, TimeSpan time)
    {
        Amount += amount;
        if (time > Time)
            Time = time;
    }
}

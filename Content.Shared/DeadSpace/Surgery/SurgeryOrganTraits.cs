// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[DataDefinition]
public sealed partial class SurgeryOrganTraits
{
    [DataField]
    public float HungerDrain;

    [DataField]
    public float ThirstDrain;

    [DataField]
    public float Alcohol = 1f;

    [DataField]
    public bool Eat = true;

    [DataField]
    public bool Drink = true;

    public void Merge(SurgeryOrganTraits other)
    {
        HungerDrain += other.HungerDrain;
        ThirstDrain += other.ThirstDrain;
        Alcohol *= other.Alcohol;
        Eat &= other.Eat;
        Drink &= other.Drink;
    }

    public bool Plain => HungerDrain == 0f && ThirstDrain == 0f && Alcohol == 1f && Eat && Drink;
}

[Prototype]
public sealed partial class SurgeryOrganRolePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Organ = "";

    [DataField]
    public SurgeryOrganTraits Missing = new();

    [DataField]
    public float AlcoholPoison = 0.03f;
}

[RegisterComponent]
public sealed partial class SurgeryOrganTraitsComponent : Component
{
    [DataField]
    public SurgeryOrganTraits Traits = new();
}

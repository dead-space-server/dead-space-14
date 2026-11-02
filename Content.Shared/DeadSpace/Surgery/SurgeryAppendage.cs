// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[Prototype]
public sealed partial class SurgeryAppendagePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public MarkingCategories Category;

    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public Dictionary<string, EntProtoId> Items = new();

    [DataField]
    public List<string> Carry = new();

    [DataField]
    public List<string> Except = new();

    [DataField]
    public List<string> Slots = new();

    public static bool Allowed(IPrototypeManager prototypes, IEntityManager entities, EntityUid patient, string id)
    {
        return prototypes.TryIndex<SurgeryAppendagePrototype>(id, out var appendage)
               && entities.TryGetComponent<Content.Shared.Humanoid.HumanoidAppearanceComponent>(patient, out var look)
               && !appendage.Except.Contains(look.Species.Id);
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SurgeryAppendageComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Appendage = "";

    [DataField, AutoNetworkedField]
    public List<Marking> Markings = new();

    [DataField, AutoNetworkedField]
    public List<string> Carry = new();

    [DataField]
    public ComponentRegistry Grant = new();

    [DataField]
    public ComponentRegistry Revoke = new();
}

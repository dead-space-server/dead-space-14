// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
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

    [DataField, AutoNetworkedField]
    public bool Base;

    [DataField]
    public ComponentRegistry Grant = new();

    [DataField]
    public ComponentRegistry Revoke = new();

    [DataField]
    public ProtoId<InventoryTemplatePrototype>? Template;

    [DataField]
    public List<ProtoId<InventoryTemplatePrototype>> TemplateFrom = new();
}

public static class SurgeryAppendageRules
{
    public const string MarkingAnchor = "MobHumanoidAnyMarking";

    public static bool Layer(SurgeryAppendagePrototype appendage, out HumanoidVisualLayers layer)
        => Enum.TryParse(appendage.Category.ToString(), out layer);

    public static bool BaseSprite(HumanoidAppearanceComponent look, SurgeryAppendagePrototype appendage,
        IPrototypeManager prototypes, out HumanoidVisualLayers layer, out string sprite)
    {
        sprite = string.Empty;
        if (!Layer(appendage, out layer)
            || !prototypes.TryIndex<SpeciesPrototype>(look.Species.Id, out var species)
            || !prototypes.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet, out var set)
            || !set.Sprites.TryGetValue(layer, out var found)
            || found == MarkingAnchor)
            return false;

        sprite = found;
        return true;
    }

    public static bool Present(HumanoidAppearanceComponent look, SurgeryAppendagePrototype appendage, IPrototypeManager prototypes)
    {
        if (look.MarkingSet.Markings.TryGetValue(appendage.Category, out var list) && list.Count > 0)
            return true;

        return BaseSprite(look, appendage, prototypes, out _, out _);
    }
}

[RegisterComponent]
public sealed partial class SurgeryTemplateSwapComponent : Component
{
    [DataField]
    public ProtoId<InventoryTemplatePrototype> Original;

    [DataField]
    public EntProtoId? Item;
}

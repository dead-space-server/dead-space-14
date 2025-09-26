using Robust.Shared.GameStates;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Shared.DeadSpace.Ports.UniformAccessories.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class UniformAccessoryComponent : Component
{
    /// <summary>Категория аксессуара.</summary>
    [DataField] [AutoNetworkedField]
    public string Category = string.Empty;

    /// <summary>Цвет категории при осмотре.</summary>
    [DataField("color")] public Color? Color;

    /// <summary>Если true — этот аксессуар также дорисовывается поверх иконки предмета-держателя (в инвентаре/мире).</summary>
    [DataField] [AutoNetworkedField]
    public bool DrawOnItemIcon = true;

    /// <summary>Если true — аксессуар может дорисовываться поверх одежды на персонаже (иконка на спрайте одежды).</summary>
    [DataField] [AutoNetworkedField]
    public bool HasIconSprite;

    /// <summary>Скрывать ли аксессуар на персонаже.</summary>
    [DataField] [AutoNetworkedField]
    public bool Hidden;

    /// <summary>Явный ключ слоя (если нужно привязаться к конкретному layer-key на спрайте персонажа).</summary>
    [DataField] [AutoNetworkedField]
    public string? LayerKey;

    /// <summary>Сколько аксессуаров этой категории может быть в держателе одновременно.</summary>
    [DataField] [AutoNetworkedField]
    public int Limit = 1;

    [DataField] [AutoNetworkedField]
    public Rsi? PlayerSprite;

    [DataField] [AutoNetworkedField]
    public NetEntity? User;
}

using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Компонент игрового правила для режима сохранения/восстановления раунда.
///     Настраивает поведение: загружать ли карту при старте, сохранять ли при конце,
///     удалять ли сохранение после себя и на какой пресет переключаться.
/// </summary>
[RegisterComponent]
public sealed partial class SaveGamemodeRuleComponent : Component
{
    /// <summary>
    ///     Имя слота сохранения (папка внутри /Saves/GameMode).
    /// </summary>
    [DataField]
    public string SaveSlot = "Deathmatch";

    /// <summary>
    ///     Загружать ли карту из сохранения при старте раунда.
    /// </summary>
    [DataField]
    public bool LoadOnStart;

    /// <summary>
    ///     Сохранять ли карту при завершении раунда.
    /// </summary>
    [DataField]
    public bool SaveOnRoundEnd;

    /// <summary>
    ///     Удалять ли файлы сохранения при завершении раунда (после сохранения, если оно включено).
    /// </summary>
    [DataField]
    public bool DeleteSaveOnRoundEnd;

    /// <summary>
    ///     ID следующего пресета, который будет автоматически выставлен при конце раунда.
    ///     Если пустое — пресет не меняется.
    /// </summary>
    [DataField]
    public string? NextPreset;
}

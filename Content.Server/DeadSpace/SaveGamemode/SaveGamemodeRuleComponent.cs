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
    ///     Используется, когда <see cref="PerMapSlot"/> выключен, и как запасное имя, если карту
    ///     за раунд определить не удалось.
    /// </summary>
    [DataField]
    public string SaveSlot = "Deathmatch";

    /// <summary>
    ///     Свой слот (папка) для каждой карты: имя слота — id прототипа выбранной на раунд карты.
    ///     Папки, которой нет, просто нет: раунд стартует на обычной карте и записывает её в новую
    ///     папку в конце. Папка, которая уже есть, поднимается через этот же режим.
    ///     Выключено — все раунды пишут в один слот <see cref="SaveSlot"/>, как было раньше.
    /// </summary>
    [DataField]
    public bool PerMapSlot;

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
    ///     Не спавнить обязательные гриды станции (карго-шаттл, торговую станцию, шаттл утилизаторов,
    ///     руины) при регистрации карты, поднятой из сохранения: в файле они уже есть, иначе в раунде
    ///     будет по два комплекта.
    /// </summary>
    [DataField]
    public bool SuppressRoundstartGrids = true;

    /// <summary>
    ///     ID следующего пресета, который будет автоматически выставлен при конце раунда.
    ///     Если пустое — пресет не меняется.
    /// </summary>
    [DataField]
    public string? NextPreset;
}

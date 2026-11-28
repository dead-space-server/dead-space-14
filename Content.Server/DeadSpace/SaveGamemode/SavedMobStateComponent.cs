using System.Collections.Generic;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Метка режима: состояние существа, которого в файле сохранения нет.
/// </summary>
/// <remarks>
///     <para>
///     «Существо было мертво» складывается из двух вещей, и ни одна в файл не пишется:
///     </para>
///     <list type="bullet">
///     <item>
///     <c>MobStateComponent.CurrentState</c> — свойство без <c>[DataField]</c>, писатель его не видит;
///     </item>
///     <item>
///     <c>DamageableComponent.Damage</c> — <c>[DataField(readOnly: true)]</c>, а readOnly-поля
///     генератор сериализации из записи выкидывает.
///     </item>
///     </list>
///     <para>
///     Отсюда и баг: после загрузки у существа нулевой урон и состояние Alive, спрайт живой
///     (данные о состоянии в appearance тоже не сериализуются — их ставит только момент смены
///     состояния), а <c>RottingComponent</c> — маркерный компонент гниения — в файле остаётся.
///     Получается «мёртвое существо с спрайтом живого», а разбуженный режимом HTN ещё и бегает:
///     «мёртвые животные возрождаются, при этом всё ещё считаясь гнилыми».
///     </para>
///     <para>
///     Метку навешивает <see cref="SaveGamemodeMobStateSystem"/> перед сериализацией карты и снимает
///     сразу после неё — в живой мир она не выходит. На загрузке она читается и удаляется.
///     Сохранения, записанные до появления метки, чинятся по <c>MobThresholdsComponent</c> и гниению,
///     см. <see cref="SaveGamemodeMobStateSystem.InferOldSaveState"/>.
///     </para>
/// </remarks>
[RegisterComponent]
public sealed partial class SavedMobStateComponent : Component
{
    /// <summary>
    ///     Состояние существа на момент сохранения.
    /// </summary>
    [DataField]
    public MobState State = MobState.Alive;

    /// <summary>
    ///     Урон существа по типам на момент сохранения.
    /// </summary>
    /// <remarks>
    ///     Свой словарь, а не <c>DamageSpecifier</c>: поле урона внутри него помечено readOnly
    ///     и в файл не пишется — та же причина, по которой существует вся метка.
    ///     Пустой словарь значит «урона не было», нулевые записи в файл не пишутся.
    /// </remarks>
    [DataField]
    public Dictionary<string, FixedPoint2> Damage = new();
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Machines.EntitySystems;
using Content.Server.ParticleAccelerator.Components;
using Content.Server.ParticleAccelerator.EntitySystems;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Shared.Singularity.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Возвращает на загруженной карте режимы работы машин, чей режим до файла не доезжает:
///     ускоритель частиц, генератор сдерживающего поля и коллектор радиации.
/// </summary>
/// <remarks>
///     <para>
///     Это тот же класс «на загрузке чего-то нет в файле», что и <see cref="FixLoadedTransientStates"/>,
///     но противоположная его половина: там состояние в файле было лишним и его надо снять, здесь его в
///     файле нет и его надо навести. Снять маркер или раздать ключ здесь нечем — у машин есть настоящая
///     логика включения, и обходить её нельзя, поэтому проход зовёт публичные методы тех же систем,
///     какими ходит игрок.
///     </para>
///     <list type="number">
///     <item><description>
///     <b>Ускоритель частиц.</b> <c>Enabled</c> и <c>SelectedStrength</c> блока управления были только
///     <c>[ViewVariables]</c> — в <c>map.yml</c> не писались ни того, ни другого, и после загрузки
///     ускоритель всегда оказывался выключенным. Теперь оба поля <c>[DataField]</c> (DS14-правка
///     компонента), а проход включает машину назад: <c>SwitchOn</c>/<c>SetStrength</c> пересчитывают
///     нагрузку, внешний вид блока и всех частей и подписку на питание. Гасить и включать «присваиванием
///     полей» нельзя — половина вида осталась бы заводской.
///     </description></item>
///     <item><description>
///     <b>Генератор сдерживающего поля.</b> <c>Connections</c> и <c>IsConnected</c> не сериализуются,
///     а сами поля — сущности, дети генератора — в файле остаются. Без словаря генератор поля не видит:
///     ни выключение тумблером, ни разрядка буфера, ни снятие с якоря их не убирают — поле висит
///     «независимо от режима генератора». Проход собирает связи заново по геометрии; поле, у которого
///     второго генератора не нашлось, больше нечем убрать никогда, поэтому оно удаляется.
///     </description></item>
///     <item><description>
///     <b>Коллектор радиации.</b> <c>Enabled</c> — обычное <c>[DataField]</c> и в файле есть, но режим
///     спрайта живёт только в appearance-ключе <c>RadiationCollectorVisuals.VisualState</c>, которого в
///     файле нет ни у кого: коллектор, вырабатывающий энергию, выглядел выключенным.
///     </description></item>
///     </list>
///     <para>
///     Старые сохранения (написанные до DS14-правок компонента) чинятся наполовину: у ускорителя режима в
///     файле не было и взять его негде — он останется выключенным. Поля генератора и вид коллектора
///     восстанавливаются для любого файла.
///     </para>
/// </remarks>
public sealed partial class SaveGamemodeLoadedStateSystem
{
    /// <summary>
    ///     Допуск в тайлах: генератор считается «на одной линии» с полем, если отстоит от луча не дальше.
    /// </summary>
    private const float FieldAxisTolerance = 0.6f;

    /// <summary>
    ///     Прогоняет починку режимов машин и пишет итог в лог одной строкой.
    /// </summary>
    /// <remarks>
    ///     Порядок внутри прохода важен только для поля: связи собираются до перерисовки лампочек,
    ///     иначе <c>FieldLight</c> посчитал бы пустой словарь связей.
    /// </remarks>
    public void FixLoadedMachineModes()
    {
        var linked = RestoreContainmentFields(out var removed);
        var accelerators = RestoreParticleAccelerators();
        var collectors = RestoreRadiationCollectors();

        Log.Info($"SaveGamemode: режимы машин на загруженной карте: связей поля восстановлено {linked}, " +
                 $"сирот полей удалено {removed}, ускорителей включено {accelerators}, " +
                 $"коллекторов обновлено {collectors}");
    }

    /// <summary>
    ///     Собирает заново связи сдерживающего поля и раздает генераторам внешний вид.
    /// </summary>
    /// <param name="removed">Сколько полей удалено как висящие без второй стороны.</param>
    /// <returns>Сколько восстановлено связей (со стороны генератора, натянувшего поле).</returns>
    private int RestoreContainmentFields(out int removed)
    {
        removed = 0;

        var gens = new Dictionary<EntityUid, ContainmentGen>();
        var genQuery = AllEntityQuery<ContainmentFieldGeneratorComponent, TransformComponent>();
        while (genQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            // Словарь из файла приходит пустым (он не [DataField]); чистим и то, что наведено руками,
            // чтобы связь не указала на уже удалённое поле.
            comp.Connections.Clear();
            comp.IsConnected = false;

            gens[uid] = new ContainmentGen(uid, comp,
                _transform.GetWorldPosition(xform),
                _transform.GetWorldRotation(xform),
                xform.GridUid);
        }

        if (gens.Count == 0)
            return 0;

        // Поле — ребёнок того генератора, от которого его «натянули» (SetParent в GenerateFieldConnection),
        // поэтому привязку поле → генератор брать из иерархии, а направление — из геометрии.
        var byGenerator = new Dictionary<EntityUid, Dictionary<Direction, List<EntityUid>>>();
        var fieldQuery = AllEntityQuery<ContainmentFieldComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (TerminatingOrDeleted(uid) || !gens.TryGetValue(xform.ParentUid, out var gen))
                continue; // поле не из связи генераторов — поставленное руками карты не трогаем

            // Направление — в системе координат генератора: генераторы ставят повёрнутыми, а связь
            // хранится именно по локальному направлению (dir в TryGenerateFieldConnection).
            var dir = (-gen.Rotation).RotateVec(_transform.GetWorldPosition(xform) - gen.WorldPos).GetDir();
            if (!byGenerator.TryGetValue(gen.Uid, out var groups))
                byGenerator[gen.Uid] = groups = new Dictionary<Direction, List<EntityUid>>();
            if (!groups.TryGetValue(dir, out var fields))
                groups[dir] = fields = new List<EntityUid>();

            fields.Add(uid);
        }

        var linked = 0;
        foreach (var (uid, groups) in byGenerator)
        {
            var gen = gens[uid];
            foreach (var (dir, fields) in groups)
            {
                // Связи бывают только по кардинальным: цикл в ReceivePower идёт с шагом два.
                if (dir is not (Direction.North or Direction.South or Direction.East or Direction.West))
                {
                    // Ни связать, ни удалить по-хорошему нечем: направление не имеет смысла для связи.
                    // Оставляем как есть, но молча терять поле нельзя — иначе снова «живёт своей жизнью».
                    Log.Warning($"SaveGamemode: поле у {ToPrettyString(gen.Uid)} стоит не по стороне света ({dir}): связь не восстановлена");
                    continue;
                }

                // Предел длины в компоненте — физический максимум при создании связи. Если второй генератор
                // дальше (карта старой версии, генератор передвинули), связь всё равно нужнее удаления поля:
                // без поля сингулярка уходит из сдерживания, а лишняя связь ничего не ломает.
                var partner = FindPartnerGenerator(gen, dir, gens, gen.Comp.MaxLength + 1f)
                              ?? FindPartnerGenerator(gen, dir, gens, float.MaxValue);
                if (partner == null)
                {
                    // На луче нет ни одного генератора: второй умер ещё до записи, и апстрим удалил бы
                    // поле вместе с ним. Убрать нечем — RemoveConnections работает по словарю связей,
                    // а записи в словаре нет. Такое поле и есть «поле, живущее своей жизнью».
                    foreach (var field in fields)
                        QueueDel(field);

                    removed += fields.Count;
                    continue;
                }

                var self = (gen.Uid, gen.Comp);
                var other = (partner.Value.Uid, partner.Value.Comp);
                gen.Comp.Connections[dir] = (other, fields);

                // Список полей один на пару: вторая сторона получает те же поля, а не свой набор.
                // TryAdd — если этот генератор уже успел зарегистрировать свою сторону связи.
                partner.Value.Comp.Connections.TryAdd(dir.GetOpposite(), (self, fields));
                linked++;
            }
        }

        foreach (var gen in gens.Values)
        {
            gen.Comp.IsConnected = gen.Comp.Connections.Count > 0;
            // Заряд буфера в файле есть, а ключей лампочек нет: без этого генератор после загрузки чёрный,
            // даже когда тянет сингулярку.
            _fieldGenerator.RefreshLoadedVisuals((gen.Uid, gen.Comp));
        }

        return linked;
    }

    /// <summary>
    ///     Ищет генератор, к которому тянется связь в указанном направлении: ближайший на той же сетке,
    ///     стоящий на одной линии с этим и не дальше <paramref name="maxDistance"/>.
    /// </summary>
    private static ContainmentGen? FindPartnerGenerator(
        ContainmentGen gen,
        Direction dir,
        Dictionary<EntityUid, ContainmentGen> gens,
        float maxDistance)
    {
        var ray = (gen.Rotation + dir.ToAngle()).ToVec();
        ContainmentGen? best = null;
        var bestAlong = float.MaxValue;

        foreach (var other in gens.Values)
        {
            if (other.Uid == gen.Uid || other.Grid != gen.Grid)
                continue;

            var delta = other.WorldPos - gen.WorldPos;
            var along = Vector2.Dot(delta, ray);
            if (along <= 0f || along > maxDistance)
                continue;

            // Луч нормализован, поэтому векторное произведение — это и есть расстояние до луча.
            if (MathF.Abs(Vector2.Cross(ray, delta)) > FieldAxisTolerance)
                continue;

            if (along >= bestAlong)
                continue;

            best = other;
            bestAlong = along;
        }

        return best;
    }

    /// <summary>
    ///     Включает ускорители частиц обратно: режим теперь в файле, но сам механизм включения никто не вызывает.
    /// </summary>
    /// <returns>Сколько ускорителей включено.</returns>
    private int RestoreParticleAccelerators()
    {
        var turnedOn = 0;
        var query = AllEntityQuery<ParticleAcceleratorControlBoxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid) || !comp.Enabled)
                continue; // выключенный ускоритель — и есть заводское состояние

            var strength = comp.SelectedStrength;

            // Сбрасываем в «выключено» и поднимаем публичными методами: у выключенной машины уровень
            // должен быть «ожидание», а включение несёт за собой нагрузку, вид частей и UI.
            comp.Enabled = false;
            comp.SelectedStrength = ParticleAcceleratorPowerState.Standby;

            // Разобранный ускоритель включать нельзя — SwitchOn на этом и падает в отладке.
            if (!_multipart.IsAssembled((uid, null)) || !comp.CanBeEnabled)
                continue;

            _accelerator.SetStrength(uid, strength, null, comp);
            _accelerator.SwitchOn(uid, null, comp);
            turnedOn++;
        }

        return turnedOn;
    }

    /// <summary>
    ///     Раздает коллекторам радиации ключ внешнего вида по их <c>Enabled</c> из файла.
    /// </summary>
    /// <returns>Сколько коллекторов обновлено.</returns>
    private int RestoreRadiationCollectors()
    {
        var updated = 0;
        var query = AllEntityQuery<RadiationCollectorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            // Компонент с [Access], поэтому руками даже читаем через систему: метод сам достаёт компонент,
            // баллон в контейнере и ставит и режим, и давление.
            _collector.RefreshLoadedAppearance(uid);
            updated++;
        }

        return updated;
    }

    /// <summary>
    ///     Генератор сдерживающего поля в том виде, в каком его удобно сопоставлять по геометрии.
    /// </summary>
    /// <param name="Uid">Сущность генератора.</param>
    /// <param name="Comp">Его компонент.</param>
    /// <param name="WorldPos">Мировое положение.</param>
    /// <param name="Rotation">Мировой поворот: направления связей локальны для генератора.</param>
    /// <param name="Grid">Сетка, на которой генератор стоит: через пустоту связи не натягиваются.</param>
    private readonly record struct ContainmentGen(
        EntityUid Uid,
        ContainmentFieldGeneratorComponent Comp,
        Vector2 WorldPos,
        Angle Rotation,
        EntityUid? Grid);
}

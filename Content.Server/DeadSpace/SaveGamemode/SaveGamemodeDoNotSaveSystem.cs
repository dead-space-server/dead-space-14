using System.Collections.Generic;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Убирает из сохранения помеченные сущности (<see cref="DoNotSaveComponent"/>): метку навешивает
///     перед записью и снимает сразу после, а на загрузке удаляет то, что приехало из файла.
/// </summary>
/// <remarks>
///     <para>
///     Правило «не сохранять» живёт в двух местах сознательно:
///     </para>
///     <list type="number">
///     <item><description>
///     <b>запись</b> — метка, под которую пост-процессор уже настроен (<c>DoNotSave</c> в
///     <c>RemoveEntitiesWithComponents</c>), чтобы шаттл вообще не попадал в <c>map.yml</c> и файл
///     оставался читабельным руками;
///     </description></item>
///     <item><description>
///     <b>загрузка</b> — удаление по тем же признакам, но без метки: сохранения, написанные до появления
///     механизма, уже лежат на диске с сеткой шаттла внутри, и пересохранить их нечем.
///     </description></item>
///     </list>
///     <para>
///     Список признаков в <see cref="StampDoNotSave"/> и <see cref="PurgeLoaded"/> обязан совпадать:
///     первый решает, что уйдёт из НОВЫХ файлов, второй — что будет удалено из УЖЕ НАПИСАННЫХ.
///     Новый случай = одна строка в каждый из них (плюс метка в прототипе, если объект статичный).
///     </para>
///     <para>
///     Пометить можно тремя способами: компонентом <c>DoNotSave</c> в прототипе или карте, прогоном
///     <see cref="StampDoNotSave"/> перед записью (для всего, что создаётся во время раунда) и — самый
///     дешёвый для автора карты — тегом <c>DoNotSave</c> (<see cref="DoNotSaveTag"/>): он лежит в уже
///     существующем компоненте <c>Tag</c>, разметка не требует нового компонента и работает и на записи,
///     и на загрузке.
///     </para>
/// </remarks>
public sealed partial class SaveGamemodeDoNotSaveSystem : EntitySystem
{
    /// <summary>
    ///     Тег «не сохранять» — разметка автора карты или спавнера, не требующая компонента.
    /// </summary>
    public const string DoNotSaveTag = "DoNotSave";

    /// <summary>
    ///     Тот же тег в типизированном виде: методы <c>TagSystem</c> не принимают строковый литерал
    ///     (атрибут <c>ForbidLiteral</c>), поэтому литерал живёт здесь.
    /// </summary>
    private static readonly ProtoId<TagPrototype> DoNotSaveTagId = new(DoNotSaveTag);

    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private TagSystem _tag = default!;

    /// <summary>
    ///     Кому режим навесил метку в этом прогоне записи. Только эти сущности и снимают её:
    ///     метка из прототипа — отметка автора карты, снимать её нельзя.
    /// </summary>
    private readonly List<EntityUid> _stamped = new();

    /// <summary>
    ///     Навешивает метку на всё, что в сохранении быть не должно.
    /// </summary>
    /// <returns>Сколько сущностей помечено (без тех, кто был помечен прототипом).</returns>
    public int StampDoNotSave()
    {
        _stamped.Clear();

        // Эвакошаттл: сетка из /Maps/Shuttles/emergency.yml, пристыкованная к станции в конце раунда.
        StampAll<EmergencyShuttleComponent>();

        // Разметка автора карты/спавнера: тег DoNotSave. Компонент DoNotSave в прототипе сюда не
        // ходит — его пост-процессор видит и без нас (RemoveEntitiesWithComponents).
        StampTagged();

        return _stamped.Count;
    }

    /// <summary>
    ///     Снимает метку со всех, кому она навешивалась: метка — состояние записи, а не мира.
    /// </summary>
    public int ClearDoNotSaveStamps()
    {
        var cleared = 0;
        foreach (var uid in _stamped)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            RemComp<DoNotSaveComponent>(uid);
            cleared++;
        }

        _stamped.Clear();
        return cleared;
    }

    /// <summary>
    ///     Удаляет сущности, которым в сохранении не место, но которые приехали в файле.
    /// </summary>
    /// <remarks>
    ///     Вызывается ДО <c>TryRegisterStations</c>: сетка шаттла — обычная сетка на карте, и
    ///     регистрация забрала бы её станции (или, не дай бог, сделала бы «главной»), а
    ///     <c>EmergencyShuttleSystem</c> на <c>StationPostInitEvent</c> навесил бы поверх ещё один шаттл.
    ///     </remarks>
    /// <returns>Сколько сущностей удалено.</returns>
    public int PurgeLoaded()
    {
        var doomed = new HashSet<EntityUid>();

        // Метка есть в новых файлах: правило сработало ещё на записи, сюда попадает только то,
        // что метку не пропустил фильтр (например, сущность добавили уже после пост-обработки).
        Collect<DoNotSaveComponent>(doomed);

        // Старые файлы: метки в них нет, но сам компонент шаттла — рантаймный, значит в файл он попал.
        Collect<EmergencyShuttleComponent>(doomed);

        // Тег DoNotSave приехал вместе с прототипом сущности: правило одинаково для нового и старого файла.
        CollectTagged(doomed);

        if (doomed.Count == 0)
            return 0;

        var undocked = UndockDoomed(doomed);

        // Станция из файла (единственный держатель ссылки на шаттл — StationEmergencyShuttle)
        // умирает следом в PurgeSavedStations, поэтому ссылку на удалённый шаттл чинить нечем и незачем:
        // поле EntityUid?, мёртвая ссылка в нём ни на что не влияет.
        foreach (var uid in doomed)
        {
            Del(uid);
        }

        Log.Info($"SaveGamemode: с файлом карты пришли сущности, помеченные «не сохранять» ({doomed.Count}) — " +
                 $"удалены до регистрации станции (расстыковано до удаления: {undocked}). " +
                 $"Эвакуацию станции выдаст заново при регистрации");

        return doomed.Count;
    }

    /// <summary>
    ///     Собирает под метку все сущности с компонентом <typeparamref name="T"/>.
    /// </summary>
    private void StampAll<T>() where T : IComponent
    {
        var query = AllEntityQuery<T>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TerminatingOrDeleted(uid) || HasComp<DoNotSaveComponent>(uid))
                continue;

            EnsureComp<DoNotSaveComponent>(uid);
            _stamped.Add(uid);
        }
    }

    /// <summary>
    ///     Собирает под метку все сущности с тегом <c>DoNotSave</c>.
    /// </summary>
    /// <remarks>
    ///     Поля <c>TagComponent</c> закрыты атрибутом <c>[Access(typeof(TagSystem))]</c>, поэтому теги
    ///     не читаем напрямую, а спрашиваем у системы тегов (RA0002).
    /// </remarks>
    private void StampTagged()
    {
        var query = AllEntityQuery<TagComponent>();
        while (query.MoveNext(out var uid, out var tags))
        {
            if (TerminatingOrDeleted(uid) || HasComp<DoNotSaveComponent>(uid) || !_tag.HasAnyTag(tags, DoNotSaveTagId))
                continue;

            EnsureComp<DoNotSaveComponent>(uid);
            _stamped.Add(uid);
        }
    }

    /// <summary>
    ///     Собирает в <paramref name="doomed"/> все сущности с тегом <c>DoNotSave</c>.
    /// </summary>
    private void CollectTagged(HashSet<EntityUid> doomed)
    {
        var query = AllEntityQuery<TagComponent>();
        while (query.MoveNext(out var uid, out var tags))
        {
            if (!TerminatingOrDeleted(uid) && _tag.HasAnyTag(tags, DoNotSaveTagId))
                doomed.Add(uid);
        }
    }

    /// <summary>
    ///     Собирает в <paramref name="doomed"/> все сущности с компонентом <typeparamref name="T"/>.
    /// </summary>
    private void Collect<T>(HashSet<EntityUid> doomed) where T : IComponent
    {
        var query = AllEntityQuery<T>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                doomed.Add(uid);
        }
    }

    /// <summary>
    ///     Расстыковывает выжившие порты о порт, который сейчас удалят.
    /// </summary>
    /// <remarks>
    ///     На загрузке <c>DockingSystem.OnStartup</c> поднимает стыковку из файла заново — со ссылкой
    ///     <c>DockedWith</c> с обеих сторон, болтами шлюзов и порталом pathfinding, — поэтому у шаттла,
    ///     приехавшего из сохранения, стыковка живая. Удалять причаленного нельзя: <c>Cleanup</c>
    ///     умирающей стороны берёт <c>Transform(...).GridUid!.Value</c> и падает, когда сетка уже
    ///     разобрана. Поэтому расстыковка — публичным методом системы стыковок, тем же путём идёт
    ///     недок, и он обнуляет <c>DockedWith</c> с обеих сторон.
    /// </remarks>
    private int UndockDoomed(HashSet<EntityUid> doomed)
    {
        var undocked = 0;

        var query = AllEntityQuery<DockingComponent>();
        while (query.MoveNext(out var uid, out var dock))
        {
            // Удаляемую сторону расстыковывать не за чем — её порт умрёт вместе с сеткой,
            // а чтобы её shutdown не звал Cleanup, достаточно обнулить ссылку с нашей стороны.
            if (doomed.Contains(uid) || dock.DockedWith is not { } other || !doomed.Contains(other))
                continue;

            _docking.Undock((uid, dock));
            undocked++;
        }

        return undocked;
    }
}

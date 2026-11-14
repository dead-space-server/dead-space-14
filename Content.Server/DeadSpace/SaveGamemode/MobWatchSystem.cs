using System.Diagnostics;
using System.Text;
using Content.Shared.Body.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Следит, в какой момент существо перестаёт им быть: пишет в лог имя, карту и стек вызовов.
/// </summary>
/// <remarks>
///     Симптом «мобы куда-то деваются» ничем не отличается от «мобы никогда не были сохранены», пока
///     не видно самого удаления. В этом движке события удаления сущности нет, но есть
///     <c>ComponentShutdown</c>: он срабатывает и когда сущность удаляют, и когда с неё снимают
///     компонент. Стек вызовов показывает систему, которая это сделала, — дальше уже видно, наша это
///     правка или сторонняя механика.
///     Выключено по умолчанию: трассировка стека дорогая, её включают на время проверки.
/// </remarks>
public sealed class MobWatchSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<ActiveNPCComponent> _npcQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;

    /// <summary>
    ///     Идёт ли сейчас наблюдение.
    /// </summary>
    public bool Enabled { get; private set; }

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("mobwatch");
        _bodyQuery = GetEntityQuery<BodyComponent>();
        _npcQuery = GetEntityQuery<ActiveNPCComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();

        SubscribeLocalEvent<MobStateComponent, ComponentStartup>(OnAppeared);
        SubscribeLocalEvent<MobStateComponent, ComponentShutdown>(OnGone);
    }

    /// <summary>
    ///     Включает или выключает наблюдение.
    /// </summary>
    public bool SetEnabled(bool value)
    {
        if (Enabled == value)
            return value;

        Enabled = value;
        _sawmill.Warning(value
            ? "наблюдение включено: каждое существо при появлении и при пропаже попадёт в лог со стеком"
            : "наблюдение выключено");
        return value;
    }

    private void OnAppeared(EntityUid uid, MobStateComponent component, ComponentStartup args)
    {
        if (!Enabled)
            return;

        _sawmill.Info($"существо появилось: {Describe(uid)}");
    }

    private void OnGone(EntityUid uid, MobStateComponent component, ComponentShutdown args)
    {
        if (!Enabled)
            return;

        // Если тело и ИИ на месте, а снят только MobState, — существо живёт, у него вырезали компонент.
        // Если не осталось ничего, существо удалили целиком.
        var what = new StringBuilder();
        if (_bodyQuery.HasComponent(uid))
            what.Append(" Body");

        if (_npcQuery.HasComponent(uid))
            what.Append(" ActiveNPC");

        if (_mindQuery.HasComponent(uid))
            what.Append(" MindContainer");

        _sawmill.Warning(
            $"существо потеряло MobState ({(what.Length == 0 ? "компонентов не осталось — сущность удаляется" : "осталось:" + what)}): " +
            $"{Describe(uid)}\n{Stack()}");
    }

    private string Describe(EntityUid uid)
    {
        var map = _transform.GetMapId(uid);
        var mind = _mindQuery.TryGetComponent(uid, out var mindComp) && mindComp.Mind != null;
        return $"{ToPrettyString(uid)} | карта {map} | тик {_timing.CurTick.Value} | разум {(mind ? "есть" : "нет")}";
    }

    /// <summary>
    ///     Стек вызовов без собственных кадров — он показывает, кто именно вызвал снятие компонента.
    /// </summary>
    private static string Stack()
    {
        var sb = new StringBuilder("    стек:");
        var ownAssembly = typeof(MobWatchSystem).Assembly.GetName().Name;
        var shown = 0;

        foreach (var frame in new StackTrace(fNeedFileInfo: false).GetFrames() ?? Array.Empty<StackFrame>())
        {
            var method = frame.GetMethod();
            var declaring = method?.DeclaringType;
            if (declaring == null)
                continue;

            var assembly = declaring.Assembly.GetName().Name;
            // Кадры Robust.Analyzers/нашей подсистемы и сам механизм событий ничего не говорят.
            if (assembly == ownAssembly || declaring.Name == nameof(EntitySystem))
                continue;

            sb.Append("\n      ").Append(declaring.FullName).Append('.').Append(method!.Name);
            if (++shown >= 14)
                break;
        }

        return sb.ToString();
    }
}

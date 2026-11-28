using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Метка режима: этой сущности не должно быть в сохранении карты.
/// </summary>
/// <remarks>
///     <para>
///     Общий механизм «не сохранять»: метку можно поставить на любой объект, который режим обязан
///     выбросить из сохранения. Первый случай — эвакуационный шаттл: его навешивает
///     <c>EmergencyShuttleSystem.AddEmergencyShuttle</c> уже во время раунда, и если раунд кончился, а
///     шаттл остался на карте станции (не улетел, его не отозвали, его сняли с болтов), сетка шаттла
///     уезжает в <c>map.yml</c> вместе со своим содержимым, стыковками и <c>ProtectedGrid</c>.
///     В следующем раунде она приезжает обратно: чужая сетка в списке сеток станции, консоли и шлюзы,
///     которых никто не создавал, и «шаттл, который уже прилетел» — при попытке отозвать/отправить
///     эвакуацию второй шаттл не появляется, а этот никем не управляется.
///     </para>
///     <para>
///     Пометить сущность можно тремя способами, все работают:
///     </para>
///     <list type="bullet">
///     <item><description>
///     компонент <c>DoNotSave</c> в прототипе или в YAML карты — отметка «never save» для будущих
///     случаев, ничего кроме строки в YAML от автора карты не требует;
///     </description></item>
///     <item><description>
///     тег <c>DoNotSave</c> (компонент <c>Tag</c>, <c>tags: [DoNotSave]</c>) — то же самое одной строкой
///     там, где заводить компонент незачем. Тег — обычный компонент, он виден и прототипу, и карте,
///     а режим переводит его в метку перед записью (<see cref="SaveGamemodeDoNotSaveSystem.StampDoNotSave"/>)
///     и по нему же чистит загрузку;
///     </description></item>
///     <item><description>
///     метка, навешанная режимом прямо перед записью (<see cref="SaveGamemodeDoNotSaveSystem.StampDoNotSave"/>)
///     и снятая сразу после неё. Так помечается то, что прототип не знает: эвакошаттл — обычный
///     прототип шаттла, «транзитным» его делает только раунд. В живой мир метка не выходит, как и
///     <see cref="SavedMobStateComponent"/>.
///     </description></item>
///     </list>
///     <para>
///     На записи метка работает через пост-процессор (<c>MapFilterConfig</c>: <c>DoNotSave</c> в
///     <c>RemoveEntitiesWithComponents</c> и <c>RemovePrototypesWithComponents</c>). Файлы, написанные
///     до появления метки, чинятся на загрузке:
///     <see cref="SaveGamemodeDoNotSaveSystem.PurgeLoaded"/> удаляет те же сущности по их собственным
///     рантайм-компонентам и по тегу — старые сохранения обязаны грузиться без пересохранения.
///     </para>
/// </remarks>
[RegisterComponent]
public sealed partial class DoNotSaveComponent : Component
{
}

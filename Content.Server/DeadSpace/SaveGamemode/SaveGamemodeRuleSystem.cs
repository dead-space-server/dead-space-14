using System.IO;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Игровое правило для режима сохранения/восстановления раунда.
///     Заготовка — дорабатывайте логику загрузки/сохранения под конкретный режим.
/// </summary>
public sealed class SaveGamemodeRuleSystem : GameRuleSystem<SaveGamemodeRuleComponent>
{
    [Dependency] private readonly SaveSystem _save = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps, before: [typeof(StationSystem)]);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    /// <summary>
    ///     Перехватывает загрузку стандартных карт.
    ///     Если режим должен загружаться из сохранения — очищаем список карт,
    ///     движок создаст пустую DefaultMap, а в Started подменим её на сохранение.
    /// </summary>
    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (!comp.LoadOnStart || !_save.SlotExists(comp.SaveSlot))
                continue;

            Log.Info($"SaveGamemode: подмена стандартной карты сохранением '{comp.SaveSlot}'");
            ev.Maps.Clear();
            break;
        }
    }

    protected override void Started(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule started: {uid}");

        if (component.LoadOnStart && _save.SlotExists(component.SaveSlot))
        {
            var path = SaveSystem.GetSavePath(component.SaveSlot);
            if (IsSaveFileValid(path))
            {
                TryLoadSaveAsDefaultMap(component);
            }
            else
            {
                Log.Error($"SaveGamemode: сохранение '{component.SaveSlot}' повреждено или пустое ({path}), загрузка отменена. Будет использована стандартная карта.");
            }
        }

        // TODO: логика старта режима (спавн игроков, телепортация на карту и т.д.)
    }

    /// <summary>
    ///     Проверяет, что файл сохранения существует, не пустой и содержит хотя бы один YAML-документ.
    /// </summary>
    private bool IsSaveFileValid(ResPath path)
    {
        var rooted = path.ToRootedPath();
        if (!_res.UserData.Exists(rooted))
            return false;

        try
        {
            using var reader = _res.UserData.OpenText(rooted);
            var content = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(content))
                return false;

            using var stringReader = new StringReader(content);
            var yaml = new YamlDotNet.RepresentationModel.YamlStream();
            yaml.Load(stringReader);
            return yaml.Documents.Count > 0;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: ошибка при проверке сохранения {path}: {e.Message}");
            return false;
        }
    }

    protected override void Ended(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule ended: {uid}");

        // TODO: очистка/завершение режима
    }

    /// <summary>
    ///     При завершении раунда: сохраняем карту, удаляем временные файлы,
    ///     автоматически переключаем пресет на следующий.
    /// </summary>
    private void OnRoundEnded(RoundEndedEvent ev)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.SaveOnRoundEnd)
                TrySaveCurrentMap(comp);

            if (comp.DeleteSaveOnRoundEnd)
                _save.TryDeleteSlot(comp.SaveSlot);

            if (!string.IsNullOrWhiteSpace(comp.NextPreset))
            {
                Log.Info($"SaveGamemode: переключение пресета на '{comp.NextPreset}'");
                GameTicker.SetGamePreset(comp.NextPreset, force: false);
            }

            break;
        }
    }

    private void TryLoadSaveAsDefaultMap(SaveGamemodeRuleComponent component)
    {
        var defaultMap = GameTicker.DefaultMap;
        if (defaultMap == MapId.Nullspace)
        {
            Log.Error("SaveGamemode: DefaultMap не установлен, загрузка сохранения невозможна");
            return;
        }

        var path = SaveSystem.GetSavePath(component.SaveSlot);
        var opts = DeserializationOptions.Default with { InitializeMaps = true };

        // Удаляем пустую карту, созданную движком, и загружаем сохранение с тем же MapId.
        if (_mapSystem.TryGetMap(defaultMap, out var oldMapUid) && Exists(oldMapUid))
            _mapSystem.DeleteMap(defaultMap);

        if (!_mapLoader.TryLoadMapWithId(defaultMap, path, out var map, out _, opts))
        {
            Log.Error($"SaveGamemode: не удалось загрузить сохранение '{component.SaveSlot}' из {path}");
            return;
        }

        Log.Info($"SaveGamemode: сохранение '{component.SaveSlot}' загружено на карту {defaultMap}");
    }

    private void TrySaveCurrentMap(SaveGamemodeRuleComponent component)
    {
        var mapId = GameTicker.DefaultMap;
        if (mapId == MapId.Nullspace)
        {
            Log.Warning("SaveGamemode: DefaultMap не установлен, сохранение пропущено");
            return;
        }

        Log.Info($"SaveGamemode: попытка сохранить карту {mapId} в слот '{component.SaveSlot}'");

        if (_save.TrySaveSlot(mapId, component.SaveSlot, force: true))
            Log.Info($"SaveGamemode: карта {mapId} сохранена в слот '{component.SaveSlot}'");
        else
            Log.Error($"SaveGamemode: не удалось сохранить карту {mapId} в слот '{component.SaveSlot}'");
    }
}

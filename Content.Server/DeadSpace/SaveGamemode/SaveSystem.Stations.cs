using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Состояние станции, которое не попадает в файл карты: имя и балансы счетов.
/// </summary>
/// <remarks>
///     Станция — сущность вне карты (nullspace), поэтому сериализатор карты её не пишет: в файле
///     остаётся только <c>BecomesStationComponent.Id</c> у сеток. Всё остальное живёт здесь.
/// </remarks>
public sealed class SavedStationState
{
    /// <summary>
    ///     Значения <c>BecomesStationComponent.Id</c> сеток станции — по ним состояние находится при загрузке.
    /// </summary>
    public readonly List<string> Ids = new();

    /// <summary>Имя станции на момент сохранения.</summary>
    public string Name = string.Empty;

    /// <summary>Балансы: id <c>CargoAccountPrototype</c> -> деньги.</summary>
    public readonly Dictionary<string, int> Accounts = new();

    /// <summary>
    ///     Имена всех сеток станции на момент сохранения, включая шаттлы, капсулы и точки интереса.
    /// </summary>
    /// <remarks>
    ///     Принадлежность сетки станции (<c>StationMemberComponent</c>) в файл карты не пишется: ссылка
    ///     живая, а станции в файле нет. Без неё загруженный шаттл или капсула «ничейные», и всё, что
    ///     ищет станцию по сетке (банк карго, терминал заказов, доступы дверей), на них не работает.
    ///     Имя сетки — то, чем её можно найти в загруженной карте: оно сериализуется в <c>MetaData</c>.
    /// </remarks>
    public readonly List<string> GridNames = new();
}

public sealed partial class SaveSystem
{
    /// <summary>Имя файла с состоянием станции — лежит рядом с картой слота.</summary>
    public const string StationFileName = "station.yml";

    private const string StationsKey = "stations";
    private const string StationIdsKey = "ids";
    private const string StationNameKey = "name";
    private const string StationAccountsKey = "accounts";
    private const string StationGridsKey = "grids";

    /// <summary>
    ///     Путь к файлу состояния станций для слота.
    /// </summary>
    public static ResPath GetStationStatePath(string slot)
    {
        return SaveDir / slot / StationFileName;
    }

    /// <summary>
    ///     Пишет состояние станций в слот. Пишется даже пустой список: отсутствие файла означает
    ///     «состояния нет» и режим уходит на восстановление по имени сетки.
    /// </summary>
    public bool TryWriteStationStates(string slot, IReadOnlyList<SavedStationState> states)
    {
        var root = new MappingDataNode();
        var list = new SequenceDataNode();

        foreach (var state in states)
        {
            var node = new MappingDataNode();

            var ids = new SequenceDataNode();
            foreach (var id in state.Ids)
            {
                ids.Add(new ValueDataNode(id));
            }

            node[StationIdsKey] = ids;
            node[StationNameKey] = new ValueDataNode(state.Name);

            var accounts = new MappingDataNode();
            foreach (var (account, money) in state.Accounts)
            {
                accounts[account] = new ValueDataNode(money.ToString(CultureInfo.InvariantCulture));
            }

            node[StationAccountsKey] = accounts;

            var grids = new SequenceDataNode();
            foreach (var gridName in state.GridNames)
            {
                grids.Add(new ValueDataNode(gridName));
            }

            node[StationGridsKey] = grids;
            list.Add(node);
        }

        root[StationsKey] = list;
        return TryWriteTreeAtomically(root, GetStationStatePath(slot));
    }

    /// <summary>
    ///     Читает состояние станций из слота.
    /// </summary>
    /// <returns>
    ///     False, если файла нет или он не читается — вызывающий тогда пользуется старым способом
    ///     (имя берётся с сетки, балансы остаются заводскими).
    /// </returns>
    public bool TryReadStationStates(string slot, out List<SavedStationState> states)
    {
        states = new List<SavedStationState>();
        var path = GetStationStatePath(slot);

        if (!_res.UserData.Exists(path))
            return false;

        MappingDataNode? doc;
        try
        {
            if (!_mapLoader.TryReadFile(path, out doc))
            {
                Log.Warning($"SaveSystem: файл состояния станций '{path}' не разобрался, состояния не будет");
                return false;
            }
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: чтение '{path}' не удалось:\n{e}");
            return false;
        }

        if (doc == null || !doc.TryGet<SequenceDataNode>(StationsKey, out var list))
        {
            Log.Warning($"SaveSystem: в '{path}' нет раздела '{StationsKey}'");
            return false;
        }

        foreach (var entry in list)
        {
            if (entry is not MappingDataNode node)
                continue;

            var state = new SavedStationState();

            if (node.TryGet<ValueDataNode>(StationNameKey, out var name) && !string.IsNullOrWhiteSpace(name.Value))
                state.Name = name.Value;

            if (node.TryGet<SequenceDataNode>(StationIdsKey, out var ids))
            {
                foreach (var id in ids)
                {
                    if (id is ValueDataNode { Value: { Length: > 0 } idValue } && !state.Ids.Contains(idValue))
                        state.Ids.Add(idValue);
                }
            }

            if (node.TryGet<MappingDataNode>(StationAccountsKey, out var accounts))
            {
                foreach (var (account, value) in accounts)
                {
                    if (string.IsNullOrEmpty(account) || value is not ValueDataNode moneyValue)
                        continue;

                    // Счёт с испорченным числом не должен тащить за собой остальные.
                    if (int.TryParse(moneyValue.Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out var money))
                        state.Accounts[account] = money;
                    else
                        Log.Warning($"SaveSystem: счёт '{account}' станции '{state.Name}' в '{path}' " +
                                    $"с неразборным балансом '{moneyValue.Value}' — пропущен");
                }
            }

            if (node.TryGet<SequenceDataNode>(StationGridsKey, out var grids))
            {
                foreach (var grid in grids)
                {
                    // Повторяющиеся имена здесь норма: одинаковых капсул у станции несколько,
                    // и каждая из них — отдельная запись.
                    if (grid is ValueDataNode { Value: { Length: > 0 } gridName })
                        state.GridNames.Add(gridName);
                }
            }

            if (state.Ids.Count == 0)
            {
                Log.Warning($"SaveSystem: запись станции '{state.Name}' в '{path}' без id сеток — пропущена");
                continue;
            }

            if (state.Name.Length == 0 && state.Accounts.Count == 0 && state.GridNames.Count == 0)
            {
                Log.Warning($"SaveSystem: запись станции (id [{string.Join(", ", state.Ids)}]) в '{path}' " +
                            "пустая — пропущена");
                continue;
            }

            states.Add(state);
        }

        Log.Info($"SaveSystem: состояние станций прочитано из '{path}' (станций: {states.Count})");
        return states.Count > 0;
    }

    /// <summary>
    ///     Записывает небольшое дерево (не карту) через временный файл, как и карту.
    /// </summary>
    private bool TryWriteTreeAtomically(MappingDataNode data, ResPath path)
    {
        var rooted = path.ToRootedPath();
        var temp = rooted.WithName(rooted.Filename + ".tmp");

        try
        {
            _res.UserData.CreateDir(rooted.Directory);

            long length;
            using (var stream = _res.UserData.Open(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                {
                    data.Write(writer);
                    writer.Flush();
                }

                length = stream.Length;
            }

            if (length == 0)
            {
                Log.Error($"SaveSystem: временный файл '{temp}' оказался пустым, '{rooted}' не тронут");
                TryDeleteFile(temp);
                return false;
            }

            return TryReplaceFromTemp(temp, rooted);
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: не удалось записать '{rooted}':\n{e}");
            TryDeleteFile(temp);
            return false;
        }
    }
}

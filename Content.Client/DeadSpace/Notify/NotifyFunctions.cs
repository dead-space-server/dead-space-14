//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Robust.Shared.Prototypes;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using Robust.Shared.Log;
using Robust.Shared.IoC;
using System.Collections.Generic;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers;

public sealed class NotifyHelper : INotifyHelper
{
    private readonly ILogManager _logManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly IConfigurationManager _cfg;
    private readonly ISawmill _sawmill;

    public NotifyHelper(ILogManager logManager, IPrototypeManager prototypeManager, IConfigurationManager cfg)
    {
        _logManager = logManager;
        _prototypeManager = prototypeManager;
        _cfg = cfg;
        _sawmill = _logManager.GetSawmill("NotifyHelper");
    }

    // Остальной код

    //ILogManager _logManager = IoCManager.Resolve<ILogManager>();
    //IPrototypeManager _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
    //IConfigurationManager _cfg = IoCManager.Resolve<IConfigurationManager>();
    private ConcurrentDictionary<string, bool> DictCvar = new ConcurrentDictionary<string, bool>();
    private ConcurrentDictionary<string, bool> DictAccess = new ConcurrentDictionary<string, bool>();

    public bool GetValueAccess(string key)
    {
        if (DictAccess.TryGetValue(key, out bool value))
        {
            return value;
        }
        else
        {
            return false;
        }
    }
    public void SetValueAccess(string key, bool value)
    {
        DictAccess[key] = value;
    }
    public IReadOnlyDictionary<string, bool> GetDictionaryAccess()
    {
        return DictAccess;
    }

    public ConcurrentDictionary<string, bool> StringToPairList(string input)
    {
        var result = new ConcurrentDictionary<string, bool>();
        var parts = input.Split(";", StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i += 2)
        {
            if (i + 1 >= parts.Length)
            {
                _sawmill.Error($"Отсутствует булевое значение для ключа '{parts[i]}'. Пропускаю.");
                break;
            }
            string word = parts[i];
            string boolStr = parts[i + 1];
            if (!bool.TryParse(boolStr, out var value))
            {
                _sawmill.Error($"Некорректное булевое значение '{boolStr}' для '{word}'. Использую false.");
                value = false;
            }
            result[word] = value;
        }
        return result;
    }


    public void EnsureInitialized()
    {
        if (DictAccess.Count == 0)
        {
            GetDictionaryFromCCvar();
            CreateDictionaryForReciveSys();
        }
    }
    private Dictionary<string, bool> CreateSnapShot(IReadOnlyDictionary<string, bool> list)
    {
        Dictionary<string, bool> result = new Dictionary<string, bool>();
        foreach (var (word, value) in list)
        {
            result[word] = value;
        }
        return result;
    }
    public string PairListToString(IReadOnlyDictionary<string, bool> list)
    {
        Dictionary<string, bool> snapshot = CreateSnapShot(list);
        var parts = new List<string>();
        foreach (var (word, value) in list)
        {
            parts.Add(word);
            parts.Add(value.ToString());
        }
        return string.Join(";", parts);
    }
    private void GetDictionaryFromCCvar()
    {
        if (!string.IsNullOrWhiteSpace(_cfg.GetCVar(CCCCVars.SysNotifyCvar)))
        {
            DictCvar = StringToPairList(_cfg.GetCVar(CCCCVars.SysNotifyCvar));
        }
    }
    private void CreateDictionaryForReciveSys()
    {
        foreach (var proto in _prototypeManager.EnumeratePrototypes<GhostRoleGroupNotify>())
        {
            if (DictCvar.ContainsKey(proto.ID))
            {
                DictAccess.AddOrUpdate(proto.ID, DictCvar[proto.ID], (k, oldValue) => DictCvar[proto.ID]);
            }
            else
            {
                DictAccess.TryAdd(proto.ID, false);
            }
        }
    }
}

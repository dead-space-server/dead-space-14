//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Robust.Shared.Prototypes;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using Robust.Shared.Log;
using Robust.Shared.IoC;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers;

public sealed class NotifyHelper : INotifyHelper
{
    //[Dependency] private readonly ILogManager _logManager = default!;
    //[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    //[Dependency] private readonly IConfigurationManager _cfg = default!;

    ILogManager _logManager = IoCManager.Resolve<ILogManager>();
    IPrototypeManager _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
    IConfigurationManager _cfg = IoCManager.Resolve<IConfigurationManager>();
    private ConcurrentDictionary<string, bool> DictCvar = new ConcurrentDictionary<string, bool>();
    private ConcurrentDictionary<string, bool> DictAccess = new ConcurrentDictionary<string, bool>();

    public ISawmill _sawmill = default!;
    public NotifyHelper()
    {
        _sawmill = _logManager.GetSawmill("NotifyHelper");
    }


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
    public ConcurrentDictionary<string, bool> GetDictionaryAccess()
    {
        return DictAccess;
    }
    public ConcurrentDictionary<string, bool> StringToPairList(string input)
    {
        var result = new ConcurrentDictionary<string, bool>();
        var parts = input.Split(";", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length % 2 != 0)
        {
            _sawmill.Error($"Нечётное количество элементов в строке '{input}'.");
            return result;
        }

        for (int i = 0; i < parts.Length; i += 2)
        {
            string word = parts[i];
            string boolStr = parts[i + 1];
            bool value;
            if (bool.TryParse(boolStr, out value))
            {
                result[word] = value;
            }
            else
            {
                result[word] = false;
                //throw new ArgumentException($"Некорректное булевое значение {boolStr}");
                _sawmill.Error($"Некорректное булевое значение {boolStr}");
            }

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
    public string PairListToString(ConcurrentDictionary<string, bool> list)
    {
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

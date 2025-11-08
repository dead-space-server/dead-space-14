using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Robust.Shared.Prototypes;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers;

public sealed class NotifyHelper
{
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
    public ConcurrentDictionary<string, bool> GetDictionaryAccess()
    {
        return DictAccess;
    }
    public ConcurrentDictionary<string, bool> StringToPairList(string input)
    {
        var result = new ConcurrentDictionary<string, bool>();
        var parts = input.Split(";", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length % 2 != 0)
            throw new ArgumentException($"Нечётное количество элементов в строке '{input}'.");

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
                //throw new ArgumentException($"Некорректное булевое значение {boolStr}");
                Logger.Debug($"Некорректное булевое значение {boolStr}");
            }

        }

        return result;
    }

    public void EnsureInitialized(IConfigurationManager cfg, IPrototypeManager prototypeManager)
    {
        if (DictAccess.Count == 0)
        {
            GetDictionaryFromCCvar(cfg);
            CreateDictionaryForReciveSys(prototypeManager);
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
    private void GetDictionaryFromCCvar(IConfigurationManager cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.GetCVar(CCCCVars.SysNotifyCvar)))
        {
            DictCvar = StringToPairList(cfg.GetCVar(CCCCVars.SysNotifyCvar));
        }
    }
    private void CreateDictionaryForReciveSys(IPrototypeManager prototypeManager)
    {
        foreach (var proto in prototypeManager.EnumeratePrototypes<GhostRoleGroupNotify>())
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

public static class NotifyHelperProvider
{
    public static NotifyHelper Helper { get; private set; } = new NotifyHelper();

    public static void Initialize(IConfigurationManager cfg, IPrototypeManager prototypeManager)
    {
        Helper.EnsureInitialized(cfg, prototypeManager);
    }
}
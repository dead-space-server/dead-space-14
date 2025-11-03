using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers;

public sealed class NotifyHelper
{
    private Dictionary<string, bool> DictCvar = new Dictionary<string, bool>();
    private Dictionary<string, bool> DictAccess = new Dictionary<string, bool>();
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
    public Dictionary<string, bool> GetDictionaryAccess()
    {
        return DictAccess;
    }
    public Dictionary<string, bool> StringToPairList(string input)
    {
        var result = new Dictionary<string, bool>();
        var parts = input.Split("/", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length % 2 != 0)
            throw new ArgumentException("Строка должна содержать чётное число элементов (слово + значение).");

        for (int i = 0; i < parts.Length; i += 2)
        {
            string word = parts[i];
            string boolStr = parts[i + 1];
            bool value;
            if (!bool.TryParse(boolStr, out value))
                throw new ArgumentException($"Некорректное булевое значение {boolStr}");

            result[word] = value;
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
    public string PairListToString(Dictionary<string, bool> list)
    {
        var parts = new List<string>();
        foreach (var (word, value) in list)
        {
            parts.Add(word);
            parts.Add(value.ToString());
        }
        return string.Join("/", parts);
    }
    public void GetDictionaryFromCCvar(IConfigurationManager cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.GetCVar(CCCCVars.SysNotifyCvar)))
        {
            DictCvar = StringToPairList(cfg.GetCVar(CCCCVars.SysNotifyCvar));
        }
    }
    public void CreateDictionaryForReciveSys(IPrototypeManager prototypeManager)
    {
        foreach (var proto in prototypeManager.EnumeratePrototypes<GhostRoleGroupNotify>())
        {
            if (DictCvar.ContainsKey(proto.ID) && !DictAccess.ContainsKey(proto.ID))
            {
                DictAccess.Add(proto.ID, DictCvar[proto.ID]);
            }
            else
            {
                if (!DictAccess.ContainsKey(proto.ID))
                {
                    DictAccess.TryAdd(proto.ID, false);
                }
            }
        }
    }
}
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.NotifySystem.NotifyFunctions;

public sealed class NotifyFunction
{
    public static Dictionary<string, bool> DictCvar = new Dictionary<string, bool>();
    public static Dictionary<string, bool> _dictAccess = new Dictionary<string, bool>();
    public static bool GetValueAccess(string key)
    {
        if (key == null)
        {
            return false;
        }
        if (_dictAccess.TryGetValue(key, out bool value))
        {
            return value;
        }
        else
        {
            return false;
        }
    }

    public static Dictionary<string, bool> StringToPairList(string input)
    {
        var result = new Dictionary<string, bool>();
        var parts = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length % 2 != 0)
            throw new ArgumentException("Строка должна содержать чётное число элементов (слово + значение).");

        for (int i = 0; i < parts.Length; i += 2)
        {
            string word = parts[i];
            string boolStr = parts[i + 1];
            bool value;
            if (!bool.TryParse(boolStr, out value))
                throw new ArgumentException($"Некорректное булевое значение {boolStr}");

            result.Add(word, value);
        }

        return result;
    }

    public static string PairListToString(Dictionary<string, bool> list)
    {
        var parts = new List<string>();
        foreach (var (word, value) in list)
        {
            parts.Add(word);
            parts.Add(value.ToString());
        }
        return string.Join(" ", parts);
    }
    public static void GetDictionaryFromCCvar(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(CCCCVars.SysNotifyCvar) != "NOTHING")
        {
            DictCvar = StringToPairList(cfg.GetCVar(CCCCVars.SysNotifyCvar));
        }
    }
    public static void CreateDictionaryForReciveSys(IPrototypeManager prototypeManager)
    {
        foreach (var proto in prototypeManager.EnumeratePrototypes<GhostRoleGroupNotify>())
        {
            if (DictCvar.ContainsKey(proto.ID) & _dictAccess.ContainsKey(proto.ID))
            {
                _dictAccess.Add(proto.ID, DictCvar[proto.ID]);
            }
            else if (!_dictAccess.ContainsKey(proto.ID))
            {
                _dictAccess.Add(proto.ID, false);
            }
        }
    }
}
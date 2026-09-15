//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Notify;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Content.Shared.DeadSpace.Notify.Prototypes;
using Robust.Shared.Prototypes;
using System.Collections.Concurrent;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Notify;

public sealed partial class ReceiveNotifySystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    private ISawmill _sawmill = default!;

    private TimeSpan _lastNotifyTime;


    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("ReceiveNotifySystem");
        _lastNotifyTime = _timing.RealTime;
        SubscribeNetworkEvent<PingMessage>(CheckReceivedNotify);
        EnsureInitialized();
    }
    private void CheckReceivedNotify(PingMessage messege)
    {
        if (_cfg.GetCVar(CCCCVars.SysNotifyPerm) && GetValueAccess(messege.ID))
        {
            if (_timing.RealTime - _lastNotifyTime >= TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.SysNotifyCoolDown)))
            {
                _audio.PlayGlobal(new SoundPathSpecifier(_cfg.GetCVar(CCCCVars.SysNotifySoundPath)), Filter.Local(), false);

                if (!_clyde.MainWindow.IsFocused)
                    _clyde.RequestWindowAttention();

                _lastNotifyTime = _timing.RealTime;
            }
        }
    }

    #region Work With Dictionary
    private ConcurrentDictionary<string, bool> _dictCvar = new ConcurrentDictionary<string, bool>();
    private ConcurrentDictionary<string, bool> _dictAccess = new ConcurrentDictionary<string, bool>();

    public bool GetValueAccess(string key)
    {
        if (_dictAccess.TryGetValue(key, out bool value))
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
        _dictAccess[key] = value;
    }
    public IReadOnlyDictionary<string, bool> GetDictionaryAccess()
    {
        return _dictAccess;
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
        if (_dictAccess.Count == 0)
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
            _dictCvar = StringToPairList(_cfg.GetCVar(CCCCVars.SysNotifyCvar));
        }
    }
    private void CreateDictionaryForReciveSys()
    {
        foreach (var proto in _prototypeManager.EnumeratePrototypes<GhostRoleGroupNotify>())
        {
            if (_dictCvar.ContainsKey(proto.ID))
            {
                _dictAccess.AddOrUpdate(proto.ID, _dictCvar[proto.ID], (k, oldValue) => _dictCvar[proto.ID]);
            }
            else
            {
                _dictAccess.TryAdd(proto.ID, false);
            }
        }
    }
    #endregion
}

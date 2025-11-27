using System.Collections.Concurrent;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using System.Collections.Generic;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers
{
    public interface INotifyHelper
    {
        bool GetValueAccess(string key);
        void SetValueAccess(string key, bool value);
        IReadOnlyDictionary<string, bool> GetDictionaryAccess();
        ConcurrentDictionary<string, bool> StringToPairList(string input);
        void EnsureInitialized();
        string PairListToString(IReadOnlyDictionary<string, bool> list);
    }
}
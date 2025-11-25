using System.Collections.Concurrent;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.NotifySystem.NotifyHelpers
{
    public interface INotifyHelper
    {
        public bool GetValueAccess(string key);
        public void SetValueAccess(string key, bool value);
        public ConcurrentDictionary<string, bool> GetDictionaryAccess();
        public ConcurrentDictionary<string, bool> StringToPairList(string input);
        public void EnsureInitialized();
        public string PairListToString(ConcurrentDictionary<string, bool> list);
    }
}
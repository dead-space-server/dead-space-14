using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Communications
{
    [Virtual]
    public partial class SharedCommunicationsConsoleComponent : Component;

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleInterfaceState : BoundUserInterfaceState
    {
        public readonly bool CanAnnounce;
        public readonly bool CanBroadcast = true;
        public readonly bool CanCall;
        public readonly TimeSpan? ExpectedCountdownEnd;
        public readonly bool CountdownStarted;
<<<<<<< ours
        public List<string>? AlertLevels;
        public string CurrentAlert;
        public Color CurrentAlertColor; // DS14 - alert level prototypes are server-only on this branch.
        public float CurrentAlertDelay;
||||||| base
        public List<string>? AlertLevels;
        public string CurrentAlert;
        public float CurrentAlertDelay;
=======
>>>>>>> theirs

<<<<<<< ours
        public CommunicationsConsoleInterfaceState(bool canAnnounce,
            bool canCall,
            List<string>? alertLevels,
            string currentAlert,
            Color currentAlertColor, // DS14
            float currentAlertDelay,
            TimeSpan? expectedCountdownEnd = null)
||||||| base
        public CommunicationsConsoleInterfaceState(bool canAnnounce, bool canCall, List<string>? alertLevels, string currentAlert, float currentAlertDelay, TimeSpan? expectedCountdownEnd = null)
=======

        public CommunicationsConsoleInterfaceState(
            bool canAnnounce,
            bool canCall,
            TimeSpan? expectedCountdownEnd = null)
>>>>>>> theirs
        {
            CanAnnounce = canAnnounce;
            CanCall = canCall;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
<<<<<<< ours
            AlertLevels = alertLevels;
            CurrentAlert = currentAlert;
            CurrentAlertColor = currentAlertColor; // DS14
            CurrentAlertDelay = currentAlertDelay;
||||||| base
            AlertLevels = alertLevels;
            CurrentAlert = currentAlert;
            CurrentAlertDelay = currentAlertDelay;
=======
>>>>>>> theirs
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleSelectAlertLevelMessage(ProtoId<AlertLevelPrototype> level) : BoundUserInterfaceMessage
    {
        public ProtoId<AlertLevelPrototype> Level = level;
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleAnnounceMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;

        public CommunicationsConsoleAnnounceMessage(string message)
        {
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleBroadcastMessage : BoundUserInterfaceMessage
    {
        public readonly string Message;
        public CommunicationsConsoleBroadcastMessage(string message)
        {
            Message = message;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleCallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleRecallEmergencyShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public enum CommunicationsConsoleUiKey
    {
        Key
    }
}

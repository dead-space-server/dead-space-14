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
        public List<string>? AlertLevels;
        public string CurrentAlert;
        public Color CurrentAlertColor; // DS14 - alert level prototypes are server-only on this branch.
        public List<string>? AlertSecondLevels;
        public string CurrentSecondAlert;
        public Color CurrentSecondAlertColor; // DS14 - alert level prototypes are server-only on this branch.
        public float CurrentAlertDelay;

        public CommunicationsConsoleInterfaceState(bool canAnnounce,
            bool canCall,
            List<string>? alertLevels,
            string currentAlert,
            Color currentAlertColor, // DS14
            List<string>? alertSecondLevels,
            string currentSecondAlert,
            Color currentSecondAlertColor, // DS14
            float currentAlertDelay,
            TimeSpan? expectedCountdownEnd = null)
        {
            CanAnnounce = canAnnounce;
            CanCall = canCall;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            AlertLevels = alertLevels;
            CurrentAlert = currentAlert;
            CurrentAlertColor = currentAlertColor; // DS14
            AlertSecondLevels = alertSecondLevels;
            CurrentSecondAlert = currentSecondAlert;
            CurrentSecondAlertColor = currentSecondAlertColor; // DS14
            CurrentAlertDelay = currentAlertDelay;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleSelectAlertLevelMessage : BoundUserInterfaceMessage
    {
        public readonly string Level;

        public readonly bool IsSecondLevel;
        public CommunicationsConsoleSelectAlertLevelMessage(string level,bool isSecondLevel = false)
        {
            Level = level;
            IsSecondLevel = isSecondLevel;
        }
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

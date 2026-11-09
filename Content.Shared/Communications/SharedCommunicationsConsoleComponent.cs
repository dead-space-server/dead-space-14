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
        //DS14-start
        public Color CurrentAlertColor; // alert level prototypes are server-only on this branch.
        public List<string>? AlertSecondLevels;
        public string CurrentSecondAlert;
        public Color CurrentSecondAlertColor;
        //DS14-end
        public float CurrentAlertDelay;

        public CommunicationsConsoleInterfaceState(bool canAnnounce,
            bool canCall,
            List<string>? alertLevels,
            string currentAlert,
            //DS14-start
            Color currentAlertColor, 
            List<string>? alertSecondLevels,
            string currentSecondAlert,
            Color currentSecondAlertColor,
            //DS14-end
            float currentAlertDelay,
            TimeSpan? expectedCountdownEnd = null)
        {
            CanAnnounce = canAnnounce;
            CanCall = canCall;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            AlertLevels = alertLevels;
            CurrentAlert = currentAlert;
            //DS14-start
            CurrentAlertColor = currentAlertColor;
            AlertSecondLevels = alertSecondLevels;
            CurrentSecondAlert = currentSecondAlert;
            CurrentSecondAlertColor = currentSecondAlertColor;
            //DS14-end
            CurrentAlertDelay = currentAlertDelay;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CommunicationsConsoleSelectAlertLevelMessage : BoundUserInterfaceMessage
    {
        public readonly string Level;

        public readonly bool IsSecondLevel; //DS14
        public CommunicationsConsoleSelectAlertLevelMessage(string level, bool isSecondLevel = false) //DS14
        {
            Level = level;
            IsSecondLevel = isSecondLevel; //DS14
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

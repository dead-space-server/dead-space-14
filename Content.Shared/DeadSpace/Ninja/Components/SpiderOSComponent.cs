using Content.Shared.DeadSpace.Ninja.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpiderOSComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<int> LockedTiers = new();

    [DataField, AutoNetworkedField]
    public Dictionary<int, NinjaSkillsCategory> SelectedModules = new();

    [DataField, AutoNetworkedField]
    public HashSet<int> ActivatedTiers = new();

    [DataField, AutoNetworkedField]
    public ProtoId<SpiderOSPrototype> Skills = "SpiderOS";

    [DataField, AutoNetworkedField]
    public ProtoId<SpiderOSBootPrototype> ActivationBootScript = "SpiderOSActivationBoot";

    [DataField, AutoNetworkedField]
    public ProtoId<SpiderOSBootPrototype> DeactivationBootScript = "SpiderOSDeactivationBoot";

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Actions = new();

    [DataField, AutoNetworkedField]
    public NinjaColorway PendingColorway = NinjaColorway.Green;

    [DataField, AutoNetworkedField]
    public bool PendingHelmet = true;

    [DataField, AutoNetworkedField]
    public NinjaStyle PendingStyle = NinjaStyle.Old;

    [DataField, AutoNetworkedField]
    public bool SuitActivated = false;

    [DataField]
    public float EnergyConsumption = 2;
}

[Serializable, NetSerializable]
public enum NinjaSkillsCategory : byte
{
    Ghost = 0,
    Snake = 1,
    Steel = 2,
}

[Serializable, NetSerializable]
public enum SpiderOSUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SpiderOSSelectModuleMessage : BoundUserInterfaceMessage
{
    public int Tier;
    public NinjaSkillsCategory Category;

    public SpiderOSSelectModuleMessage()
    {
    }

    public SpiderOSSelectModuleMessage(int tier, NinjaSkillsCategory category)
    {
        Tier = tier;
        Category = category;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSSetAppearanceMessage : BoundUserInterfaceMessage
{
    public NinjaColorway Colorway;
    public bool Helmet;
    public NinjaStyle Style;

    public SpiderOSSetAppearanceMessage()
    {
    }

    public SpiderOSSetAppearanceMessage(NinjaColorway colorway, bool helmet, NinjaStyle style)
    {
        Colorway = colorway;
        Helmet = helmet;
        Style = style;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSShuttleControlMessage : BoundUserInterfaceMessage
{
    public SpiderOSShuttleControlMessage()
    {
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSSetSuitPowerMessage : BoundUserInterfaceMessage
{
    public bool Activated;

    public SpiderOSSetSuitPowerMessage()
    {
    }

    public SpiderOSSetSuitPowerMessage(bool activated)
    {
        Activated = activated;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSSecureRequestMessage : BoundUserInterfaceMessage
{
    public bool Secure;

    public SpiderOSBootCheck Check;

    public SpiderOSSecureRequestMessage()
    {
    }

    public SpiderOSSecureRequestMessage(bool secure, SpiderOSBootCheck check)
    {
        Secure = secure;
        Check = check;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSSecureConfirmedMessage : BoundUserInterfaceMessage
{
    public bool Success;

    public string FailReason = string.Empty;

    public SpiderOSSecureConfirmedMessage()
    {
    }

    public SpiderOSSecureConfirmedMessage(bool success, string failReason = "")
    {
        Success = success;
        FailReason = failReason;
    }
}

[Serializable, NetSerializable]
public sealed class SpiderOSBoundUserInterfaceState : BoundUserInterfaceState
{
    public HashSet<int> LockedTiers = new();
    public Dictionary<int, NinjaSkillsCategory> SelectedModules = new();
    public HashSet<int> ActivatedTiers = new();
    public string Skills = "SpiderOS";
    public NinjaColorway PendingColorway = NinjaColorway.Green;
    public bool PendingHelmet = true;
    public NinjaStyle PendingStyle = NinjaStyle.Old;
    public bool SuitActivated = false;

    public SpiderOSBoundUserInterfaceState()
    {
    }

    public SpiderOSBoundUserInterfaceState(
        HashSet<int> lockedTiers,
        Dictionary<int, NinjaSkillsCategory> selectedModules,
        HashSet<int> activatedTiers,
        string skills,
        NinjaColorway pendingColorway,
        bool pendingHelmet,
        NinjaStyle pendingStyle,
        bool suitActivated)
    {
        LockedTiers = lockedTiers;
        SelectedModules = selectedModules;
        ActivatedTiers = activatedTiers;
        Skills = skills;
        PendingColorway = pendingColorway;
        PendingHelmet = pendingHelmet;
        PendingStyle = pendingStyle;
        SuitActivated = suitActivated;
    }
}
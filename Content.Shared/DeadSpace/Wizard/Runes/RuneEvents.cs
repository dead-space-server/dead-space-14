// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Alert;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed partial class RuneCircleActionEvent : InstantActionEvent
{
}

public sealed partial class RuneDispelAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed class RuneDrawnMessage : EntityEventArgs
{
    public readonly string Rune;
    public readonly int Index;
    public readonly List<string> Chain;

    public RuneDrawnMessage(string rune, int index, List<string> chain)
    {
        Rune = rune;
        Index = index;
        Chain = chain;
    }
}

[Serializable, NetSerializable]
public record struct RuneCooldown(TimeSpan Ready, float Length, string Name, TimeSpan Active = default, float ActiveLength = 0f);

[Serializable, NetSerializable]
public sealed class RuneFizzleMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class RuneCastMessage : EntityEventArgs
{
    public readonly List<string> Runes;
    public readonly NetCoordinates Target;

    public RuneCastMessage(List<string> runes, NetCoordinates target)
    {
        Runes = runes;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class RuneCastResultEvent : EntityEventArgs
{
    public readonly bool Success;
    public readonly string? Reason;

    public RuneCastResultEvent(bool success, string? reason)
    {
        Success = success;
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public sealed class RuneQuillOpenEvent : EntityEventArgs
{
    public readonly NetEntity Scroll;

    public RuneQuillOpenEvent(NetEntity scroll)
    {
        Scroll = scroll;
    }
}

[Serializable, NetSerializable]
public sealed class RuneQuillInscribeMessage : EntityEventArgs
{
    public readonly NetEntity Scroll;
    public readonly List<string> Runes;

    public RuneQuillInscribeMessage(NetEntity scroll, List<string> runes)
    {
        Scroll = scroll;
        Runes = runes;
    }
}

[Serializable, NetSerializable]
public sealed class RuneQuillBurnMessage : EntityEventArgs
{
    public readonly NetEntity Scroll;

    public RuneQuillBurnMessage(NetEntity scroll)
    {
        Scroll = scroll;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class RuneLearnProductEvent : EntityEventArgs
{
    [DataField(required: true)]
    public ProtoId<RunePrototype> Rune;
}

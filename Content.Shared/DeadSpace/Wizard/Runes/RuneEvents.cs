// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public sealed partial class RuneCircleActionEvent : InstantActionEvent
{
}

public sealed partial class RuneScrollActionEvent : WorldTargetActionEvent
{
}

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
public record struct RuneCooldown(TimeSpan Ready, float Length, string Name);

[Serializable, NetSerializable]
public sealed class RuneFizzleMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class RuneCastMessage : EntityEventArgs
{
    public readonly List<string> Runes;
    public readonly NetCoordinates Target;
    public readonly bool Inscribe;
    public readonly NetEntity? Paper;

    public RuneCastMessage(List<string> runes, NetCoordinates target, bool inscribe, NetEntity? paper = null)
    {
        Runes = runes;
        Target = target;
        Inscribe = inscribe;
        Paper = paper;
    }
}

[Serializable, NetSerializable]
public sealed class RuneScrollCastMessage : EntityEventArgs
{
    public readonly NetEntity Scroll;
    public readonly NetCoordinates Target;

    public RuneScrollCastMessage(NetEntity scroll, NetCoordinates target)
    {
        Scroll = scroll;
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
    public readonly NetEntity Paper;

    public RuneQuillOpenEvent(NetEntity paper)
    {
        Paper = paper;
    }
}

[Serializable, NetSerializable]
public sealed partial class RuneInscribeDoAfterEvent : DoAfterEvent
{
    [DataField]
    public List<string> Runes = new();

    private RuneInscribeDoAfterEvent()
    {
    }

    public RuneInscribeDoAfterEvent(List<string> runes)
    {
        Runes = runes;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class RuneLearnProductEvent : EntityEventArgs
{
    [DataField(required: true)]
    public ProtoId<RunePrototype> Rune;
}

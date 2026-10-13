// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.SlimeForm;

[RegisterComponent]
public sealed partial class SlimeFormComponent : Component
{
    [DataField]
    public EntProtoId FormAction = "ActionSlimeForm";
    [DataField]
    public bool IsSlime;

    [DataField]
    public float FormCooldown = 300f;

    [DataField]
    public float FormDuration = 4f;

    public bool Busy;

    public EntityUid? Action;

    public TimeSpan NextLimbDrop;

    public TimeSpan LimbInterval;

    public List<SlimeFormLimb> Limbs = new();

    public List<SlimeFormLimb> Fallen = new();
}

public readonly record struct SlimeFormLimb(EntityUid Part, EntityUid Parent, string Slot);

public sealed partial class SlimeFormActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class SlimeFormEnterDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class SlimeFormExitDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum SlimeFormVisuals : byte
{
    BodyColor,
    FaceColor,
}

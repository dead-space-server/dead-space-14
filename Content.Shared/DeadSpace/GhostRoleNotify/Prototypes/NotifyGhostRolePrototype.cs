using Robust.Shared.Prototypes;
namespace Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;

[Prototype("ghostNotifyGroup")]
public sealed partial class GhostRoleGroupNotify : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField]
    public string Name { get; private set; } = default!;
}
[Prototype("soundForPing")]
public sealed partial class SoundForPing : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)]
    public string Name { get; private set; } = default!;
    [DataField(required: true)]
    public string Path { get; private set; } = default!;
}

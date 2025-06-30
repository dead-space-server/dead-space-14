using Robust.Shared.Prototypes;
namespace Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;

[Prototype("ghostNotifyGroup")]
public sealed partial class GhostRoleGroupNotify : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField]
    public string Name { get; private set; } = default!;
    public string MainGroup { get; private set; } = default!;
    public string SecondGroup { get; private set; } = default!;
}

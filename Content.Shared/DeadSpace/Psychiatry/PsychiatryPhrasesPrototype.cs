// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Psychiatry;

[Prototype]
public sealed partial class PsychiatryPhrasesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<string> Crime = new();

    [DataField]
    public List<string> Mockery = new();

    [DataField]
    public List<string> Neutral = new();

    [DataField]
    public List<string> Addressed = new();

    [DataField]
    public List<string> Radio = new();

    [DataField]
    public List<string> FakeNames = new();

    [DataField]
    public List<string> RadioNames = new();

    [DataField]
    public List<ProtoId<JobPrototype>> RadioJobs = new();
}

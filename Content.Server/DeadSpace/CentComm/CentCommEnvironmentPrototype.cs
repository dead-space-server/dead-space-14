// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.CentComm;

[Prototype]
public sealed partial class CentCommEnvironmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Parallax prototypes are client-only.
    [DataField(required: true)]
    public string Parallax = default!;

    [DataField]
    public GasMixture? Atmosphere;
}

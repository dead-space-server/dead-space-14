// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.ERT.Prototypes;

namespace Content.Shared.DeadSpace.ERT;

public abstract partial class SharedErtResponseSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public int GetErtPrice(ProtoId<ErtTeamPrototype> protoId)
    {
        if (!_prototype.TryIndex(protoId, out var proto))
            return 0;

        return proto.Price;
    }
}

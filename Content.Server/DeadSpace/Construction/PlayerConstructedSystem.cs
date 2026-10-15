// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Construction;

namespace Content.Server.DeadSpace.Construction;

public sealed class PlayerConstructedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnChangeEntity);
    }

    private void OnChangeEntity(ConstructionChangeEntityEvent args)
    {
        if (HasComp<PlayerConstructedComponent>(args.Old))
            EnsureComp<PlayerConstructedComponent>(args.New);
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Autopsy;
using Content.Shared.DeadSpace.Surgery;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Surgery;

public sealed partial class SurgeryXrayTableVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryXrayTableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurgeryXrayTableComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<SurgeryXrayTableComponent> ent, ref ComponentStartup args)
    {
        Paint(ent);
    }

    private void OnState(Entity<SurgeryXrayTableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Paint(ent);
    }

    private void Paint(Entity<SurgeryXrayTableComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprites.LayerMapTryGet((ent.Owner, sprite), AutopsyTableLayers.WithDevice, out var layer, false))
            return;

        _sprites.LayerSetVisible((ent.Owner, sprite), layer, ent.Comp.Lit);
    }
}

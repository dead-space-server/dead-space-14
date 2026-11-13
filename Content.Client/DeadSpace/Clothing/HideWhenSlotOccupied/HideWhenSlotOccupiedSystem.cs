// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DeadSpace.Clothing.HideWhenSlotOccupied;
using Content.Shared.Item;

namespace Content.Client.DeadSpace.Clothing.HideWhenSlotOccupied;

public sealed partial class HideWhenSlotOccupiedSystem : SharedHideWhenSlotOccupiedSystem
{
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HideWhenSlotOccupiedComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<HideWhenSlotOccupiedComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<HideWhenSlotOccupiedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnGetEquipmentVisuals(Entity<HideWhenSlotOccupiedComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.IsHidden)
            return;

        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, layer) = args.Layers[i];
            layer.Visible = false;
            args.Layers[i] = (key, layer);
        }
    }
}

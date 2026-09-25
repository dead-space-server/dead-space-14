// TEMP FOR EVENT DONT FCKNG TOUCH
using Content.Shared.DeadSpace.ItemSwitch;
using Content.Shared.DeadSpace.ItemSwitch.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.DeadSpace.ItemSwitch;

public sealed partial class ItemSwitchSystem : Shared.DeadSpace.ItemSwitch.SharedItemSwitchSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSwitchComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<ItemSwitchComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.State != null)
            UpdateVisuals((ent, ent.Comp), ent.Comp.State);
    }

    public override void VisualsChanged(Entity<ItemSwitchComponent> ent, string key)
    {
        base.VisualsChanged(ent, key);

        if (ent.Comp.States.TryGetValue(key, out var state)
            && state.Sprite is not null
            && TryComp(ent, out SpriteComponent? sprite))
        {
            sprite.LayerSetSprite(0, state.Sprite);
        }
    }
}
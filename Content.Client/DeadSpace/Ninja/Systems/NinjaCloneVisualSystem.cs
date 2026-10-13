// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaCloneVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaCloneVisualComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(Entity<NinjaCloneVisualComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var source = ent.Comp.Source;
        if (source == EntityUid.Invalid || source == ent.Owner || !Exists(source))
            return;

        if (!TryComp<SpriteComponent>(source, out var sourceSprite) ||
            !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.CopySprite((source, sourceSprite), (ent, sprite));
        _sprite.SetVisible((ent, sprite), true);
    }
}
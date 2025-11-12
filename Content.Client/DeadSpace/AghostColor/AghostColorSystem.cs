// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.AghostColor;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.DeadSpace.AghostColor;

public sealed class AghostColorSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    public event Action<AghostColorComponent>? PlayerUpdated;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AghostColorComponent, AfterAutoHandleStateEvent>(OnGhostState);
    }

    private void OnGhostState(EntityUid uid, AghostColorComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.LayerSetColor((uid, sprite), 0, component.Color);

        if (uid != _playerManager.LocalEntity)
            return;

        PlayerUpdated?.Invoke(component);
    }
}

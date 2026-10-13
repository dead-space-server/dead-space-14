// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.DeadSpace.CameraArchives;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed class CameraArchiveSealSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CameraArchiveSealedComponent, ComponentStartup>(OnSeal);
        SubscribeLocalEvent<CameraArchiveSealedComponent, ComponentShutdown>(OnPeel);
        SubscribeLocalEvent<CameraArchiveBurnedComponent, ComponentStartup>(OnBurn);
        SubscribeLocalEvent<CameraArchiveBurnedComponent, ComponentShutdown>(OnMend);
    }

    private void OnBurn(Entity<CameraArchiveBurnedComponent> ent, ref ComponentStartup args)
    {
        Paint(ent.Owner, Color.FromHex("#3a241c"));
    }

    private void OnMend(Entity<CameraArchiveBurnedComponent> ent, ref ComponentShutdown args)
    {
        if (!HasComp<CameraArchiveSealedComponent>(ent))
            Paint(ent.Owner, Color.White);
    }

    private void OnSeal(Entity<CameraArchiveSealedComponent> ent, ref ComponentStartup args)
    {
        Paint(ent.Owner, Color.Black);
    }

    private void OnPeel(Entity<CameraArchiveSealedComponent> ent, ref ComponentShutdown args)
    {
        if (!HasComp<CameraArchiveBurnedComponent>(ent))
            Paint(ent.Owner, Color.White);
    }

    private void Paint(EntityUid uid, Color color)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Color = color;
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            _sprite.LayerSetColor((uid, sprite), i, color);
    }
}

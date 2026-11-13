// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared.DeadSpace.ERT.Components;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.ERT;

public sealed partial class ErtTrackingSystem : EntitySystem
{
    private const string TrackingAlert = "ErtTracking";

    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtTrackingComponent, UpdateAlertSpriteEvent>(OnUpdateAlertSprite);
    }

    private void OnUpdateAlertSprite(Entity<ErtTrackingComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != TrackingAlert)
            return;

        var rotation = ent.Comp.DistanceToTarget switch
        {
            Distance.Reached or Distance.Close or Distance.Medium or Distance.Far => ent.Comp.ArrowAngle + _eyeManager.CurrentEye.Rotation,
            _ => Angle.Zero
        };

        _sprite.LayerSetRotation((args.SpriteViewEnt.Owner, (SpriteComponent?) args.SpriteViewEnt.Comp), AlertVisualLayers.Base, rotation);
    }
}

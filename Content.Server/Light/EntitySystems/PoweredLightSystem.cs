using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.Light.EntitySystems;

/// <inheritdoc/>
public sealed partial class PoweredLightSystem : SharedPoweredLightSystem
{
<<<<<<< ours
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo)
            return;

        // check cooldown first to prevent abuse
        var time = GameTiming.CurTime;
        if (light.LastGhostBlink != null)
        {
            if (time <= light.LastGhostBlink + light.GhostBlinkingCooldown)
                return;
        }

        light.LastGhostBlink = time;

        ToggleBlinkingLight(uid, light, true);
        Timer.Spawn(light.GhostBlinkingTime, () =>
        {
            ToggleBlinkingLight(uid, light, false);
        });

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
||||||| base
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo)
            return;

        // check cooldown first to prevent abuse
        var time = GameTiming.CurTime;
        if (light.LastGhostBlink != null)
        {
            if (time <= light.LastGhostBlink + light.GhostBlinkingCooldown)
                return;
        }

        light.LastGhostBlink = time;

        ToggleBlinkingLight(uid, light, true);
        uid.SpawnTimer(light.GhostBlinkingTime, () =>
        {
            ToggleBlinkingLight(uid, light, false);
        });

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
=======
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<PoweredLightComponent> ent, ref MapInitEvent args)
>>>>>>> theirs
    {
        // TODO: Use ContainerFill dog
        if (ent.Comp.HasLampOnSpawn != null)
        {
            var entity = Spawn(ent.Comp.HasLampOnSpawn, Transform(ent).Coordinates);
            ContainerSystem.Insert(entity, ent.Comp.LightBulbContainer);
        }
        // need this to update visualizers
        UpdateLight(ent, ent.Comp);
    }
}

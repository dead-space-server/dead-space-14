using Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Armutant.Abilities.Blade;

public sealed class TalonArmsBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public EntProtoId TalonClawsPrototype = "WeaponTalonClaws";
    public override void Initialize()
    {
        SubscribeLocalEvent<BladeTalonSpawnComponent, CreateTalonBladeEvent>(OnCreateTalonClaws);
        SubscribeLocalEvent<BladeTalonSpawnComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BladeTalonSpawnComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentInit(EntityUid uid, BladeTalonSpawnComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionTalonEntity, component.ActionToggleTalonSpawn, uid);
    }

    private void OnComponentShutdown(EntityUid uid, BladeTalonSpawnComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionTalonEntity);
    }
    private void OnCreateTalonClaws(EntityUid uid, BladeTalonSpawnComponent component, ref CreateTalonBladeEvent args)
    {
        var shard = Spawn(TalonClawsPrototype, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, shard);

        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }

        args.Handled = true;
    }
}

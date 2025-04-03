using Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Weapons.Reflect;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Armutant.Abilities.Shield;
public sealed partial class VoidShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<VoidShieldComponent, VoidShieldToggleEvent>(OnToggleShield);
        SubscribeLocalEvent<VoidShieldComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VoidShieldComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentInit(EntityUid uid, VoidShieldComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionVoidShieldEntity, component.ActionToggleVoidShield, uid);
    }

    private void OnComponentShutdown(EntityUid uid, VoidShieldComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionVoidShieldEntity);
    }
    private void OnToggleShield(EntityUid uid, VoidShieldComponent component, VoidShieldToggleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var reflectComponent = EnsureComp<ReflectComponent>(uid);
        reflectComponent.ReflectProb = 0.99f;
        reflectComponent.Spread = 180f;
        reflectComponent.SoundOnReflect = component.ReflectSound;

        if (_net.IsServer && component.SelfEffect is not null)
        {
            var effect = Spawn(component.SelfEffect, Transform(uid).Coordinates);
            _transformSystem.SetParent(effect, uid);
        }

        Timer.Spawn(TimeSpan.FromSeconds(15), () =>
        {
            RemComp<ReflectComponent>(uid);
        });
    }
}

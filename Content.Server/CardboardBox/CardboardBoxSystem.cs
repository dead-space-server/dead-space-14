using System.Numerics;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CardboardBox;
using Content.Shared.CardboardBox.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Slippery;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CardboardBox;

public sealed partial class CardboardBoxSystem : SharedCardboardBoxSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private EntityStorageSystem _storage = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private EntityManager _entity = default!;
    // DS14-start - #43532 is adapted to the current server-side cardboard-box system.
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CardboardBoxComponent, StorageAfterOpenEvent>(AfterStorageOpen);
        SubscribeLocalEvent<CardboardBoxComponent, StorageBeforeOpenEvent>(BeforeStorageOpen);
        SubscribeLocalEvent<CardboardBoxComponent, StorageAfterCloseEvent>(AfterStorageClosed);
        SubscribeLocalEvent<CardboardBoxComponent, ActivateInWorldEvent>(OnInteracted);
        SubscribeLocalEvent<CardboardBoxComponent, VehicleOperatorSetEvent>(OnOperatorSet);

        SubscribeLocalEvent<CardboardBoxComponent, SlipEvent>(OnSlip);
    }

    private void OnInteracted(EntityUid uid, CardboardBoxComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EntityStorageComponent>(uid, out var box))
            return;

        if (!args.Complex)
        {
            if (box.Open || !box.Contents.Contains(args.User))
                return;
        }

        args.Handled = true;
        _storage.ToggleOpen(args.User, uid, box);
    }

    private void BeforeStorageOpen(EntityUid uid, CardboardBoxComponent component, ref StorageBeforeOpenEvent args)
    {
        if (component.Quiet)
            return;

        if (!_vehicle.TryGetOperator(uid, out var operatorEnt))
            return;

        if (_timing.CurTime <= component.EffectCooldown)
            return;

        RaiseNetworkEvent(new PlayBoxEffectMessage(GetNetEntity(uid), GetNetEntity(operatorEnt.Value.Owner)));
        _audio.PlayPvs(component.EffectSound, uid);
        component.EffectCooldown = _timing.CurTime + component.CooldownDuration;
    }

    private void AfterStorageOpen(EntityUid uid, CardboardBoxComponent component, ref StorageAfterOpenEvent args)
    {
        // If this box has a stealth/chameleon effect, disable the stealth effect while the box is open.
        _stealth.SetEnabled(uid, false);
        if (HasComp<StealthComponent>(uid))
        {
            RemComp<SlipperyComponent>(uid);
        }
    }

    private void AfterStorageClosed(EntityUid uid, CardboardBoxComponent component, ref StorageAfterCloseEvent args)
    {
        // If this box has a stealth/chameleon effect, enable the stealth effect.
        if (TryComp(uid, out StealthComponent? stealth))
        {
            _stealth.SetVisibility(uid, stealth.MaxVisibility, stealth);
            _stealth.SetEnabled(uid, true, stealth);
            EnsureComp<SlipperyComponent>(uid, out var slippery);
        }
    }

    private void OnOperatorSet(Entity<CardboardBoxComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.NewOperator != null || args.OldOperator == null)
            return;

        _physics.SetLinearVelocity(ent, Vector2.Zero);
    }

    private void OnSlip(EntityUid uid, CardboardBoxComponent component, ref SlipEvent args)
    {
        if (_vehicle.TryGetOperator(uid, out var operatorEnt))
            _stun.TryUpdateParalyzeDuration(operatorEnt.Value.Owner, TimeSpan.FromSeconds(2));

        if (TryComp<EntityStorageComponent>(uid, out var box))
        {
            for (var i = 0; i < box.Contents.Count; i++)
            {
                var ent = _entity.GetNetEntity(box.Contents.ContainedEntities[i]);

                if (_entity.HasComponent<HumanoidAppearanceComponent>(_entity.GetEntity(ent)))
                    _stun.TryUpdateParalyzeDuration(_entity.GetEntity(ent), TimeSpan.FromSeconds(2));
            }
        }

        _audio.PlayPvs(component.EffectSound, uid);
        _storage.OpenStorage(uid);
    }
}

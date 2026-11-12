using Content.Shared.CardboardBox;
// DS14-start - upstream moved the cardboard box logic into SharedCardboardBoxSystem;
// this server system only keeps our slippery cardboard box behaviour.
using Content.Shared.CardboardBox.Components;
using Content.Shared.Humanoid;
using Content.Shared.Slippery;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Audio.Systems;
// DS14-end

namespace Content.Server.CardboardBox;

public sealed partial class CardboardBoxSystem : SharedCardboardBoxSystem
{
    // DS14-start
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        // CardboardBoxComponent storage open/close events are already subscribed by the shared system,
        // so the stealth box slipperiness hooks onto StealthComponent instead.
        SubscribeLocalEvent<StealthComponent, StorageAfterOpenEvent>(OnStealthBoxOpened);
        SubscribeLocalEvent<StealthComponent, StorageAfterCloseEvent>(OnStealthBoxClosed);
        SubscribeLocalEvent<CardboardBoxComponent, SlipEvent>(OnSlip);
    }

    private void OnStealthBoxOpened(Entity<StealthComponent> ent, ref StorageAfterOpenEvent args)
    {
        if (!HasComp<CardboardBoxComponent>(ent))
            return;

        RemComp<SlipperyComponent>(ent);
    }

    private void OnStealthBoxClosed(Entity<StealthComponent> ent, ref StorageAfterCloseEvent args)
    {
        if (!HasComp<CardboardBoxComponent>(ent))
            return;

        EnsureComp<SlipperyComponent>(ent);
    }

    private void OnSlip(Entity<CardboardBoxComponent> ent, ref SlipEvent args)
    {
        if (_vehicle.TryGetOperator(ent.Owner, out var operatorEnt))
            _stun.TryUpdateParalyzeDuration(operatorEnt.Value.Owner, TimeSpan.FromSeconds(2));

        if (TryComp<EntityStorageComponent>(ent, out var box))
        {
            foreach (var contained in box.Contents.ContainedEntities)
            {
                if (HasComp<HumanoidAppearanceComponent>(contained))
                    _stun.TryUpdateParalyzeDuration(contained, TimeSpan.FromSeconds(2));
            }
        }

        _audio.PlayPvs(ent.Comp.EffectSound, ent);
        _storage.OpenStorage(ent.Owner);
    }
    // DS14-end
}

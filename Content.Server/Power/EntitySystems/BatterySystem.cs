using Content.Server.Power.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Utility;

namespace Content.Server.Power.EntitySystems;

public sealed partial class BatterySystem : SharedBatterySystem
{
    // DS14-start
    private readonly HashSet<EntityUid> _networkBatteryPreSyncQueue = new();
    private readonly HashSet<EntityUid> _rateNetworkBatteries = new();
    private readonly List<EntityUid> _networkBatteryPreSyncBuffer = new();
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        // DS14-start
        SubscribeLocalEvent<PowerNetworkBatteryComponent, ComponentStartup>(OnNetBatteryStartup);
        SubscribeLocalEvent<PowerNetworkBatteryComponent, ComponentRemove>(OnNetBatteryRemove);
        SubscribeLocalEvent<PowerNetworkBatteryComponent, ChargeChangedEvent>(OnNetBatteryChargeChanged);
        // DS14-end
        SubscribeLocalEvent<PowerNetworkBatteryComponent, RejuvenateEvent>(OnNetBatteryRejuvenate);
        SubscribeLocalEvent<NetworkBatteryPreSync>(PreSync);
        SubscribeLocalEvent<NetworkBatteryPostSync>(PostSync);
    }

    protected override void OnStartup(Entity<BatteryComponent> ent, ref ComponentStartup args)
    {
        // DS14-start
        if (HasComp<PowerNetworkBatteryComponent>(ent))
            QueueNetworkBatteryPreSync(ent.Owner, ent.Comp);
        // DS14-end

        // Debug assert to prevent anyone from killing their networking performance by dirtying a battery's charge every single tick.
        // This checks for components that interact with the power network, have a charge rate that ramps up over time and therefore
        // have to set the charge in an update loop instead of using a <see cref="RefreshChargeRateEvent"/> subscription.
        // This is usually the case for APCs, SMES, battery powered turrets or similar.
        // For those entities you should disable net sync for the battery in your prototype, using
        /// <code>
        /// - type: Battery
        ///   netSync: false
        /// </code>
        /// This disables networking and prediction for this battery.
        if (!ent.Comp.NetSyncEnabled)
            return;

        DebugTools.Assert(!HasComp<ApcPowerReceiverBatteryComponent>(ent), $"{ToPrettyString(ent.Owner)} has a predicted battery connected to the power net. Disable net sync!");
        DebugTools.Assert(!HasComp<PowerNetworkBatteryComponent>(ent), $"{ToPrettyString(ent.Owner)} has a predicted battery connected to the power net. Disable net sync!");
        DebugTools.Assert(!HasComp<PowerConsumerComponent>(ent), $"{ToPrettyString(ent.Owner)} has a predicted battery connected to the power net. Disable net sync!");
    }

    // DS14-start
    private void OnNetBatteryStartup(Entity<PowerNetworkBatteryComponent> ent, ref ComponentStartup args)
    {
        QueueNetworkBatteryPreSync(ent.Owner);
    }

    private void OnNetBatteryRemove(Entity<PowerNetworkBatteryComponent> ent, ref ComponentRemove args)
    {
        _networkBatteryPreSyncQueue.Remove(ent.Owner);
        _rateNetworkBatteries.Remove(ent.Owner);
    }

    private void OnNetBatteryChargeChanged(Entity<PowerNetworkBatteryComponent> ent, ref ChargeChangedEvent args)
    {
        QueueNetworkBatteryPreSync(ent.Owner);

        if (args.CurrentChargeRate == 0f)
            _rateNetworkBatteries.Remove(ent.Owner);
        else
            _rateNetworkBatteries.Add(ent.Owner);
    }
    // DS14-end

    private void OnNetBatteryRejuvenate(Entity<PowerNetworkBatteryComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.NetworkBattery.SetCurrentStorage(ent.Comp.NetworkBattery.Capacity); // DS14
    }

    private void PreSync(NetworkBatteryPreSync ev)
    {
        // DS14-start
        _networkBatteryPreSyncBuffer.Clear();
        _networkBatteryPreSyncBuffer.AddRange(_networkBatteryPreSyncQueue);

        foreach (var uid in _rateNetworkBatteries)
        {
            if (_networkBatteryPreSyncQueue.Add(uid))
                _networkBatteryPreSyncBuffer.Add(uid);
        }

        _networkBatteryPreSyncQueue.Clear();

        foreach (var uid in _networkBatteryPreSyncBuffer)
            PreSyncNetworkBattery(uid);
        // DS14-end
    }

    private void PostSync(NetworkBatteryPostSync ev)
    {
        // Ignoring entity pausing. If the entity was paused, neither component's data should have been changed.
        if (ev.ChangedBatteries == null) // DS14
            return;

        foreach (var uid in ev.ChangedBatteries) // DS14
        {
            // DS14-start
            if (!TryComp<PowerNetworkBatteryComponent>(uid, out var netBat) ||
                !TryComp<BatteryComponent>(uid, out var bat))
            // DS14-end
            {
                continue;
            }

            var currentStorage = netBat.NetworkBattery.CurrentStorage;
            if (bat.ChargeRate == 0f && bat.LastCharge == currentStorage)
                continue;

            SetCharge((uid, bat), currentStorage);
        }
    }

    // DS14-start
    private void QueueNetworkBatteryPreSync(EntityUid uid, BatteryComponent? battery = null)
    {
        _networkBatteryPreSyncQueue.Add(uid);

        if (battery == null && !TryComp(uid, out battery))
            return;

        if (battery.ChargeRate == 0f)
            _rateNetworkBatteries.Remove(uid);
        else
            _rateNetworkBatteries.Add(uid);
    }

    private void PreSyncNetworkBattery(EntityUid uid)
    {
        // Ignoring entity pausing. If the entity was paused, neither component's data should have been changed.
        if (!TryComp<PowerNetworkBatteryComponent>(uid, out var netBat) ||
            !TryComp<BatteryComponent>(uid, out var bat))
        {
            _networkBatteryPreSyncQueue.Remove(uid);
            _rateNetworkBatteries.Remove(uid);
            return;
        }

        var currentCharge = bat.ChargeRate == 0f
            ? Math.Clamp(bat.LastCharge, 0f, bat.MaxCharge)
            : GetCharge((uid, bat));

        DebugTools.Assert(currentCharge <= bat.MaxCharge && currentCharge >= 0);

        if (bat.ChargeRate == 0f)
            _rateNetworkBatteries.Remove(uid);
        else
            _rateNetworkBatteries.Add(uid);

        if (netBat.NetworkBattery.Capacity != bat.MaxCharge)
            netBat.NetworkBattery.Capacity = bat.MaxCharge;

        if (netBat.NetworkBattery.CurrentStorage != currentCharge)
            netBat.NetworkBattery.SetCurrentStorage(currentCharge, trackChange: false);
    }
    // DS14-end
}

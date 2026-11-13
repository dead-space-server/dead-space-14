// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.EnergySeller;
using Robust.Shared.Prototypes;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Power.EntitySystems;
using Content.Server.Power.Components;
using Robust.Server.GameObjects;

namespace Content.Server.DeadSpace.EnergySeller;

public sealed partial class EnergySellerSystem : EntitySystem
{
    private const int MinPowerSetting = 5000;

    [Dependency] private StationSystem _station = default!;
    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryComponent, BatteryStateChangedEvent>(CheckBatteryCharges);
        SubscribeLocalEvent<EnergySellerComponent, ChangesForSellingEnergy>(ChoseVoid);
        SubscribeLocalEvent<EnergySellerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EnergySellerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnStartup(EntityUid uid, EnergySellerComponent comp, ComponentStartup args)
    {
        comp.Distribution = new Dictionary<ProtoId<CargoAccountPrototype>, double>
        {
            { comp.Account, 1.0 },
        };

        UpdateUI(uid, comp);
    }

    private void CheckBatteryCharges(EntityUid uid, BatteryComponent comp, BatteryStateChangedEvent args)
    {
        if (args.NewState != BatteryState.Full)
            return;
        if (!TryComp<EnergySellerComponent>(uid, out var compSell))
            return;
        if (!TryComp<PowerNetworkBatteryComponent>(uid, out var compNetBatt))
            return;

        if (!TryGetStationBank(uid, out var stationBank))
            return;
        _cargo.UpdateBankAccount(stationBank, GetEnergyPrice(comp, compSell, compNetBatt), compSell.Distribution, false);
        compSell.LastSell = DateTime.Now;
        _battery.SetCharge((uid, comp), 0);
        Dirty(stationBank);
    }

    private void ChoseVoid(EntityUid uid, EnergySellerComponent comp, ChangesForSellingEnergy message)
    {
        if (message.SpeedOrLimit)
        {
            SetSpeed(uid, comp, message);
        }
        else
        {
            SetMaxLimit(uid, comp, message);
        }
    }

    private void SetSpeed(EntityUid uid, EnergySellerComponent comp, ChangesForSellingEnergy message)
    {
        if (message.Now is >= MinPowerSetting && TryComp<PowerNetworkBatteryComponent>(uid, out var compSell))
        {
            compSell.MaxChargeRate = ClampPowerSetting(message.Now.Value, comp.MaxChargeRate);
        }
        if (message.Max is >= MinPowerSetting)
        {
            comp.MaxChargeRate = (int)message.Max;
        }
        if (TryComp<PowerNetworkBatteryComponent>(uid, out var compBatt) && compBatt.MaxChargeRate > comp.MaxChargeRate)
        {
            compBatt.MaxChargeRate = comp.MaxChargeRate;
        }
        UpdateUI(uid, comp);
    }

    private void SetMaxLimit(EntityUid uid, EnergySellerComponent comp, ChangesForSellingEnergy message)
    {
        if (message.Now is >= MinPowerSetting && TryComp<BatteryComponent>(uid, out var compSell))
        {
            _battery.SetMaxCharge((uid, compSell), ClampPowerSetting(message.Now.Value, comp.MaxLimit));
        }
        if (message.Max is >= MinPowerSetting)
        {
            comp.MaxLimit = (int)message.Max;
        }
        if (TryComp<BatteryComponent>(uid, out var compBatt) && compBatt.MaxCharge > comp.MaxLimit)
        {
            _battery.SetMaxCharge((uid, compBatt), (float)comp.MaxLimit);
        }
        UpdateUI(uid, comp);
    }

    private void UpdateUI(EntityUid uid, EnergySellerComponent comp)
    {
        if (!_userInterfaceSystem.HasUi(uid, ESBControllerUiKey.Key))
            return;
        if (!TryComp<BatteryComponent>(uid, out var compBat))
            return;
        if (!TryComp<PowerNetworkBatteryComponent>(uid, out var compSell))
            return;
        _userInterfaceSystem.SetUiState(uid, ESBControllerUiKey.Key, new EnergySellerBoundUserInterfaceState(comp.MaxChargeRate, comp.MaxLimit, (int)compSell.MaxChargeRate, (int)compBat.MaxCharge));
    }

    private void OnPowerChanged(EntityUid uid, EnergySellerComponent comp, ref PowerChangedEvent args)
    {
        UpdateUI(uid, comp);
    }

    private bool TryGetStationBank(EntityUid uid, out Entity<StationBankAccountComponent?> stationBank)
    {
        stationBank = default;

        var station = _station.GetOwningStation(uid);
        if (station == null || !TryComp<StationBankAccountComponent>(station.Value, out var bankAccount))
            return false;

        stationBank = (station.Value, bankAccount);
        return true;
    }

    private static int GetEnergyPrice(BatteryComponent battery, EnergySellerComponent seller, PowerNetworkBatteryComponent netBatt)
    {
        var timeMulty = (seller.LastSell - DateTime.Now).TotalMinutes > 2f ? (seller.LastSell - DateTime.Now).Minutes * 1.5 : 1;
        var multi = ((battery.MaxCharge / seller.AdditionalCoefficient + 1) * (netBatt.MaxChargeRate >= battery.MaxCharge ? 1 : (netBatt.MaxChargeRate / battery.MaxCharge)) / timeMulty);
        return (int)Math.Round(battery.PricePerJoule * battery.MaxCharge * (multi > 10 ? 10 : multi));
    }

    private static int ClampPowerSetting(int value, int max)
    {
        return Math.Clamp(value, MinPowerSetting, Math.Max(max, MinPowerSetting));
    }
}

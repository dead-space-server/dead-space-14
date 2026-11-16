namespace Content.Server.Silicons.Borgs;

public sealed partial class BorgSystem
{
    public float GetBorgHp(EntityUid borg) => CalcHP(borg);

    public float GetBorgCharge(EntityUid borg)
    {
        return _powerCell.TryGetBatteryFromSlot(borg, out var battery)
            ? _battery.GetChargeLevel(battery.Value.AsNullable())
            : 0f;
    }
}

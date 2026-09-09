// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;
using Content.Shared.Examine;

namespace Content.Shared.DeadSpace.Atmos.Piping;

public abstract class SharedGasTemperatureValveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTemperatureValveComponent, GasTemperatureValveChangeThresholdMessage>(OnChangeThreshold);
        SubscribeLocalEvent<GasTemperatureValveComponent, GasTemperatureValveToggleModeMessage>(OnToggleMode);
        SubscribeLocalEvent<GasTemperatureValveComponent, ExaminedEvent>(OnExamined);
    }

    private void OnChangeThreshold(Entity<GasTemperatureValveComponent> ent, ref GasTemperatureValveChangeThresholdMessage args)
    {
        if (!float.IsFinite(args.Threshold))
            return;

        ent.Comp.Threshold = Math.Clamp(args.Threshold, Atmospherics.TCMB, GasTemperatureValveComponent.MaxThreshold);
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnToggleMode(Entity<GasTemperatureValveComponent> ent, ref GasTemperatureValveToggleModeMessage args)
    {
        ent.Comp.PassWhenBelow = args.PassWhenBelow;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnExamined(Entity<GasTemperatureValveComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gas-temperature-valve-examined",
            ("mode", Loc.GetString(ent.Comp.PassWhenBelow ? "gas-temperature-valve-below" : "gas-temperature-valve-above")),
            ("threshold", MathF.Min(ent.Comp.Threshold, GasTemperatureValveComponent.MaxThreshold))));
    }

    protected virtual void UpdateUi(Entity<GasTemperatureValveComponent> ent)
    {
    }
}

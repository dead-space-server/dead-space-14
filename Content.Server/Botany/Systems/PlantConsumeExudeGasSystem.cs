using Content.Server.Atmos.EntitySystems;
using Content.Shared.Administration.Logs; // DS14
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Database; // DS14
using Robust.Shared.Timing; // DS14

namespace Content.Server.Botany.Systems;

public sealed partial class PlantConsumeExudeGasSystem : SharedPlantConsumeExudeGasSystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantConsumeExudeGasComponent, PlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<PlantConsumeExudeGasComponent, ComponentShutdown>(OnGasLogShutdown);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextGasLog = new();
    private static readonly TimeSpan GasLogCooldown = TimeSpan.FromSeconds(60);
    // DS14-end

    private void OnPlantGrow(Entity<PlantConsumeExudeGasComponent> ent, ref PlantGrowEvent args)
    {
        if (!TryComp<PlantComponent>(ent.Owner, out var plant)
            || !TryComp<PlantHolderComponent>(ent.Owner, out var plantHolder))
            return;

        var environment = _atmosphere.GetContainingMixture(ent.Owner, true, true) ?? GasMixture.SpaceGas;

        // Consume Gasses.
        plantHolder.MissingGas = false;
        var missingGas = 0;
        List<(Gas Gas, float Moles)>? consumed = null; // DS14
        if (ent.Comp.ConsumeGasses.Count > 0)
        {
            foreach (var (gas, amount) in ent.Comp.ConsumeGasses)
            {
                if (environment.GetMoles(gas) < amount)
                {
                    missingGas++;
                    continue;
                }

                environment.AdjustMoles(gas, -amount);
                if (IsLoggedGas(gas)) // DS14
                {
                    consumed ??= new(); // DS14
                    consumed.Add((gas, amount)); // DS14
                }
            }

            if (missingGas > 0)
            {
                _plantHolder.AdjustsHealth(ent.Owner, -missingGas);
                plantHolder.MissingGas = true;
            }
        }

        // Exude Gasses.
        var exudeCount = ent.Comp.ExudeGasses.Count;
        List<(Gas Gas, float Moles)>? exuded = null; // DS14
        if (exudeCount > 0)
        {
            foreach (var (gas, amount) in ent.Comp.ExudeGasses)
            {
                // DS14-start
                var released = MathF.Max(1f, MathF.Round(amount * MathF.Round(plant.Potency) / exudeCount));
                environment.AdjustMoles(gas, released);
                if (IsLoggedGas(gas))
                {
                    exuded ??= new();
                    exuded.Add((gas, released));
                }
                // DS14-end
            }
        }

        if (exuded != null || consumed != null) // DS14
            LogGasExchange(ent.Owner, exuded, consumed); // DS14

        Dirty(ent);
    }

    // DS14-start
    private void OnGasLogShutdown(Entity<PlantConsumeExudeGasComponent> ent, ref ComponentShutdown args)
    {
        _nextGasLog.Remove(ent.Owner);
    }

    private void LogGasExchange(EntityUid plant, List<(Gas Gas, float Moles)>? exuded, List<(Gas Gas, float Moles)>? consumed)
    {
        if (!_adminLog.Enabled)
            return;

        var exudeText = FormatMoles(exuded);
        var consumeText = FormatMoles(consumed);
        if (exudeText == null && consumeText == null)
            return;

        var now = _timing.CurTime;
        if (_nextGasLog.TryGetValue(plant, out var next) && now < next)
            return;

        _nextGasLog[plant] = now + GasLogCooldown;
        var name = TryComp<PlantDataComponent>(plant, out var data)
            ? Loc.GetString(data.Name)
            : MetaData(plant).EntityName;
        _adminLog.Add(LogType.Botany, exudeText != null ? LogImpact.Medium : LogImpact.Low,
            $"{plant:plant} ({name:name}) gas exchange at {Transform(plant).Coordinates:pos}: exuded {exudeText ?? "none":exuded}; consumed {consumeText ?? "none":consumed}.");
    }

    private static string? FormatMoles(List<(Gas Gas, float Moles)>? gases)
    {
        if (gases == null || gases.Count == 0)
            return null;

        var parts = new List<string>(gases.Count);
        foreach (var (gas, moles) in gases)
            parts.Add($"{gas} {moles:0.##} mol");

        return string.Join(", ", parts);
    }

    private static bool IsLoggedGas(Gas gas)
    {
        return gas is not (Gas.Oxygen or Gas.Nitrogen or Gas.CarbonDioxide or Gas.WaterVapor);
    }
    // DS14-end
}

using Content.Server.Atmos.EntitySystems;
using Content.Server.Materials;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.BluespaceMiner;
using Content.Shared.Examine;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.BluespaceMiner;

/// <summary>
/// Логика блюспейс-майнера: добывает материалы при соблюдении условий
/// среды (20-80 K, 100-150 кПа) и выделяет горячий углекислый газ.
/// Добытые ресурсы материализуются рядом с машиной с блюспейс-эффектом.
/// </summary>
public sealed class BluespaceMinerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityManager _spawn = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceMinerComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<BluespaceMinerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAtmosUpdate(Entity<BluespaceMinerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;

        if (!_power.IsPowered(ent))
        {
            SetStatus(ent, BluespaceMinerStatus.Unpowered);
            return;
        }

        var environment = _atmosphere.GetContainingMixture((ent, Transform(ent)), true, true);
        if (environment == null || !ConditionsMet(comp, environment.Temperature, environment.Pressure))
        {
            SetStatus(ent, BluespaceMinerStatus.BadConditions);
            return;
        }

        SetStatus(ent, BluespaceMinerStatus.Ok);

        // выделяем горячий газ пропорционально времени
        var moles = comp.GasMolesPerSecond * args.dt;
        ReleaseGas(comp, environment, moles);

        // добыча материала — пачками раз в секунду
        comp.Accumulator += args.dt;
        while (comp.Accumulator >= 1f)
        {
            comp.Accumulator -= 1f;
            MineOnce(ent, comp);
        }
    }

    private void MineOnce(Entity<BluespaceMinerComponent> ent, BluespaceMinerComponent comp)
    {
        if (comp.MaterialWeights.Count == 0)
            return;

        var material = PickMaterial(comp);
        var amount = comp.SheetsPerSecond * comp.MaterialPerSheet;

        // копим добычу во внутреннем хранилище, а когда наберётся пачка —
        // материализуем её рядом с машиной с блюспейс-эффектом
        if (!_materialStorage.TryChangeMaterialAmount(ent, material, amount))
            return;

        var batchAmount = comp.SheetsPerBatch * comp.MaterialPerSheet;
        if (comp.SheetsPerBatch <= 0 || _materialStorage.GetMaterialAmount(ent, material) < batchAmount)
            return;

        _materialStorage.TryChangeMaterialAmount(ent, material, -batchAmount);
        SpawnBatch(ent, comp, material, comp.SheetsPerBatch);
    }

    private void SpawnBatch(Entity<BluespaceMinerComponent> ent, BluespaceMinerComponent comp,
        ProtoId<MaterialPrototype> material, int sheets)
    {
        var materialProto = _proto.Index<MaterialPrototype>(material);
        if (materialProto.StackEntity is not { } stackProto)
            return;

        var xform = Transform(ent);
        var coords = xform.Coordinates.Offset(_random.NextVector2(-0.4f, 0.4f));

        var stack = _spawn.SpawnEntity(stackProto, coords);
        _stack.SetCount(stack, sheets);
        _spawn.SpawnEntity("EffectBluespaceMinerTeleport", coords);
    }

    private ProtoId<MaterialPrototype> PickMaterial(BluespaceMinerComponent comp)
    {
        var total = 0f;
        foreach (var (_, weight) in comp.MaterialWeights)
            total += weight;

        var roll = _random.NextFloat() * total;
        foreach (var (material, weight) in comp.MaterialWeights)
        {
            roll -= weight;
            if (roll <= 0f)
                return material;
        }

        // страховка от ошибок округления
        foreach (var (material, _) in comp.MaterialWeights)
            return material;

        throw new InvalidOperationException("BluespaceMiner has no materials configured");
    }

    private void ReleaseGas(BluespaceMinerComponent comp, GasMixture environment, float moles)
    {
        if (moles <= 0f)
            return;

        // добавляем газ с температурой comp.GasTemperature:
        // пересчитываем температуру смеси как средневзвешенную по молям
        var totalMoles = environment.TotalMoles;
        var newTemp = totalMoles > 0f
            ? (environment.Temperature * totalMoles + comp.GasTemperature * moles) / (totalMoles + moles)
            : comp.GasTemperature;

        environment.AdjustMoles(comp.ReleasedGas, moles);
        environment.Temperature = newTemp;
    }

    private bool ConditionsMet(BluespaceMinerComponent comp, float temperature, float pressure)
    {
        return temperature >= comp.MinTemperature
            && temperature <= comp.MaxTemperature
            && pressure >= comp.MinPressure
            && pressure <= comp.MaxPressure;
    }

    private void SetStatus(Entity<BluespaceMinerComponent> ent, BluespaceMinerStatus status)
    {
        _appearance.SetData(ent, BluespaceMinerVisuals.Status, status);
    }

    private void OnExamined(Entity<BluespaceMinerComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;

        if (!_power.IsPowered(ent))
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-unpowered"));
            return;
        }

        var environment = _atmosphere.GetContainingMixture((ent, Transform(ent)), true, true);
        if (environment == null)
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-pressure-low"));
            return;
        }

        var pushed = false;
        if (environment.Temperature < comp.MinTemperature)
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-temp-low"));
            pushed = true;
        }
        else if (environment.Temperature > comp.MaxTemperature)
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-temp-high"));
            pushed = true;
        }

        if (environment.Pressure < comp.MinPressure)
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-pressure-low"));
            pushed = true;
        }
        else if (environment.Pressure > comp.MaxPressure)
        {
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-pressure-high"));
            pushed = true;
        }

        if (!pushed)
            args.PushMarkup(Loc.GetString("bluespace-miner-examine-ok"));
    }
}

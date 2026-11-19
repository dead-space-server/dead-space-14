using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.BluespaceMiner;
using Content.Shared.Examine;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.BluespaceMiner;

public sealed partial class BluespaceMinerSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceMinerComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<BluespaceMinerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAtmosUpdate(Entity<BluespaceMinerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;

        if (!IsPowered(ent))
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

        var interval = MathF.Max(comp.CycleInterval, 0.1f);
        comp.Accumulator += args.dt;
        while (comp.Accumulator >= interval)
        {
            comp.Accumulator -= interval;
            ReleaseGas(comp, environment, comp.GasMolesPerSecond * interval);
            MineOnce(ent, comp);
        }
    }

    private void MineOnce(Entity<BluespaceMinerComponent> ent, BluespaceMinerComponent comp)
    {
        if (comp.MaterialWeights.Count == 0)
            return;

        var material = PickMaterial(comp);
        SpawnBatch(ent, material, comp.SheetsPerBatch);
    }

    private void SpawnBatch(Entity<BluespaceMinerComponent> ent, ProtoId<MaterialPrototype> material, int sheets)
    {
        var materialProto = _proto.Index(material);
        if (materialProto.StackEntity is not { } stackProto)
            return;

        var xform = Transform(ent);
        var coords = xform.Coordinates.Offset(_random.NextVector2(-ent.Comp.TeleportRange, ent.Comp.TeleportRange));

        var stack = Spawn(stackProto, coords);
        _stack.SetCount(stack, sheets);
        Spawn("EffectBluespaceMinerTeleport", coords);
    }

    private ProtoId<MaterialPrototype> PickMaterial(BluespaceMinerComponent comp)
    {
        var total = 0f;
        foreach (var weight in comp.MaterialWeights.Values)
            total += weight;

        var roll = _random.NextFloat() * total;
        ProtoId<MaterialPrototype> last = default;
        foreach (var (material, weight) in comp.MaterialWeights)
        {
            last = material;
            roll -= weight;
            if (roll <= 0f)
                return material;
        }

        return last;
    }

    private void ReleaseGas(BluespaceMinerComponent comp, GasMixture environment, float moles)
    {
        if (moles <= 0f)
            return;

        var giver = new GasMixture { Temperature = comp.GasTemperature };
        giver.AdjustMoles(comp.ReleasedGas, moles);
        _atmosphere.Merge(environment, giver);
    }

    private bool IsPowered(EntityUid uid)
    {
        return TryComp<PowerConsumerComponent>(uid, out var consumer)
            && consumer.DrawRate > 0f
            && consumer.ReceivedPower >= consumer.DrawRate * 0.9f;
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

        if (!IsPowered(ent))
            return;

        var environment = _atmosphere.GetContainingMixture((ent, Transform(ent)), true);
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
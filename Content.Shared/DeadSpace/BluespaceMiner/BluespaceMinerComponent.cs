using Content.Shared.Atmos;
using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.BluespaceMiner;

/// <summary>
/// Блюспейс-майнер: добывает материалы из блюспейса, пока соблюдаются
/// атмосферные условия окружающей среды, и выделяет горячий углекислый газ.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BluespaceMinerComponent : Component
{
    /// <summary>Минимальная температура среды, K.</summary>
    [DataField]
    public float MinTemperature = 20f;

    /// <summary>Максимальная температура среды, K.</summary>
    [DataField]
    public float MaxTemperature = 80f;

    /// <summary>Минимальное давление среды, кПа.</summary>
    [DataField]
    public float MinPressure = 100f;

    /// <summary>Максимальное давление среды, кПа.</summary>
    [DataField]
    public float MaxPressure = 150f;

    /// <summary>Сколько листов материала добывается за секунду работы.</summary>
    [DataField]
    public int SheetsPerSecond = 4;

    /// <summary>Сколько предметов с материалами телепортируется за секунду (зона 3х3 тайла вокруг машины).</summary>
    [DataField]
    public int MaxTeleportsPerSecond = 5;

    /// <summary>Полуширина зоны телепорта, метры (3 тайла).</summary>
    [DataField]
    public float TeleportRange = 1.5f;

    /// <summary>Материалов в одном листе (1 лист = 100 единиц).</summary>
    [DataField]
    public int MaterialPerSheet = 100;

    /// <summary>Шансы выпадения материалов (относительные веса).</summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, float> MaterialWeights = new()
    {
        { "Glass", 23f },
        { "Steel", 23f },
        { "Plastic", 23f },
        { "Silver", 13.5f },
        { "Gold", 7f },
        { "Diamond", 4f },
        { "Uranium", 5.5f },
    };

    /// <summary>Какой газ выделяется при работе.</summary>
    [DataField]
    public Gas ReleasedGas = Gas.CarbonDioxide;

    /// <summary>Молей газа в секунду.</summary>
    [DataField]
    public float GasMolesPerSecond = 2f;

    /// <summary>Температура выделяемого газа, K.</summary>
    [DataField]
    public float GasTemperature = 200f;

    /// <summary>Накопитель секунд для добычи материала.</summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>Сколько листов материализуется одной пачкой с блюспейс-эффектом.</summary>
    [DataField]
    public int SheetsPerBatch = 20;
}

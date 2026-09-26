// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Components;
using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Content.Shared.Body.Prototypes;
using Content.Shared.DeadSpace.Virus.Symptoms;
using System.Linq;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Zombies;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Interaction.Events;

namespace Content.Shared.DeadSpace.Virus;

public enum BedRegenerationType
{
    None = 0,      // Не влияет
    Normal,        // Обычная кровать
    Stasis         // Стазис-кровать
}

public struct BaseVirusSettings
{
    /// <summary>
    ///     Стандартная цена для удаления тела.
    /// </summary>
    public const int StaticPriceDeleteBody = 250;

    /// <summary>
    ///     Стандартная цена для удаления симптома.
    /// </summary>
    public const int StaticPriceDeleteSymptom = 1000;

    /// <summary>
    ///     Стандартная цена для мутации всех тел.
    /// </summary>
    public const int StaticBodyPrice = 250;

    /// <summary>
    ///     Стандартный для всех вирусов белый список компонентов.
    /// </summary>
    public static readonly string[] DefaultWhitelistComponents =
    {
        "MobState",
        "Bloodstream"
    };

    /// <summary>
    ///     Список не доступных для покупки тел.
    /// </summary>
    public static readonly List<ProtoId<BodyPrototype>> BodyBlackList = new List<ProtoId<BodyPrototype>>
    {
        "AnimalNymphBrain",
        "AnimalNymphLungs",
        "AnimalNymphStomach",
        "Skeleton",
        "Gingerbread",
        "Bot",
        "HugBot",
        "MobDeadSquadNecro",
        "EggSpider",
        "XenoMaid",
        "AnimalRuminant",
        "SmartCorgi",
        "SuperSoldier",
        "IPC"
    };

    /// <summary>
    ///     Модификаторы ослабления вируса в зависимости от состояния.
    /// </summary>
    public static readonly Dictionary<BedRegenerationType, float> DebuffVirusMultipliers =
        new()
        {
            { BedRegenerationType.None, 1.0f },
            { BedRegenerationType.Normal, 0.5f },
            { BedRegenerationType.Stasis, 0.1f },
        };
}

public abstract partial class SharedVirusSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    private ISawmill _sawmill = default!;

    /// <summary>
    ///     Стандартное окно времени проявления симптом.
    /// </summary>
    protected TimedWindow DefaultSymptomWindow = new TimedWindow(TimeSpan.FromSeconds(15f), TimeSpan.FromSeconds(60f));

    /// <summary>
    ///     Метка для сущностей, которые не могут проявить симпптомы.
    /// </summary>
    public readonly ProtoId<TagPrototype> VirusIgnorSymptomsTag = "VirusIgnorSymptoms";
    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("SharedVirusSystem");

        SubscribeLocalEvent<SentientVirusComponent, InteractionAttemptEvent>(OnSentientVirusInteract);
        SubscribeLocalEvent<SentientVirusComponent, UseAttemptEvent>(OnSentientVirusUse);
    }

    private void OnSentientVirusInteract(Entity<SentientVirusComponent> ent, ref InteractionAttemptEvent args)
    {
        // Its own mutation UI and instant actions remain available, but it has no physical body to use objects.
        if (args.Target != null && args.Target != ent.Owner)
            args.Cancelled = true;
    }

    private void OnSentientVirusUse(Entity<SentientVirusComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    public int GetSymptomPrice(VirusData data, ProtoId<VirusSymptomPrototype> symptomId)
    {
        if (!_prototype.TryIndex(symptomId, out var proto))
            return 0;

        return Math.Max(1, data.ActiveSymptom.Count) * proto.Price;
    }

    public int GetSymptomPrice(List<ProtoId<VirusSymptomPrototype>> symptoms, ProtoId<VirusSymptomPrototype> symptomId)
    {
        if (!_prototype.TryIndex(symptomId, out var proto))
            return 0;

        return Math.Max(1, symptoms.Count) * proto.Price;
    }

    public int GetSymptomPrice(List<ProtoId<VirusSymptomPrototype>> symptoms, VirusSymptomPrototype proto)
    {
        return Math.Max(1, symptoms.Count) * proto.Price;
    }

    public int GetSymptomPrice(VirusData data, VirusSymptomPrototype proto)
    {
        return Math.Max(1, data.ActiveSymptom.Count) * proto.Price;
    }

    public int GetBodyPrice(VirusData data)
    {
        return Math.Max(1, data.BodyWhitelist.Count) * BaseVirusSettings.StaticBodyPrice;
    }

    public int GetBodyPrice(List<ProtoId<BodyPrototype>> bodyWhitelist)
    {
        return Math.Max(1, bodyWhitelist.Count) * BaseVirusSettings.StaticBodyPrice;
    }

    public int GetBodyDeletePrice()
    {
        return BaseVirusSettings.StaticPriceDeleteBody;
    }

    public int GetSymptomDeletePrice(int multiPriceDeleteSymptom)
    {
        return Math.Max(1, multiPriceDeleteSymptom) * BaseVirusSettings.StaticPriceDeleteSymptom;
    }

    public int GetQuantityInfected(string strainId)
    {
        int quantity = 0;

        var query = EntityQueryEnumerator<VirusComponent>();
        while (query.MoveNext(out _, out var component))
        {
            if (component.Data.StrainId == strainId)
                quantity++;
        }

        return quantity;
    }

    /// <summary>
    ///     Могут ли симптомы проявиться.
    /// </summary>
    public bool CanManifestInHost(Entity<VirusComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        if (_tag.HasTag(entity, VirusIgnorSymptomsTag))
            return false;

        if (_mobState.IsDead(entity))
            return false;

        if (HasComp<PrimaryPacientComponent>(entity)
            || HasComp<ZombieComponent>(entity)
            || HasComp<NecromorfComponent>(entity))
            return false;

        return true;
    }

    public bool HasSymptom<T>(Entity<VirusComponent?> entity)
    where T : IVirusSymptom
    {
        if (!Resolve(entity, ref entity.Comp, false))
        {
            _sawmill.Warning($"Entity {entity.Owner} не имеет компонента VirusComponent, невозможно проверить наличие симптома {typeof(T).Name}.");
            return default!;
        }

        return entity.Comp.ActiveSymptomInstances.Any(s => s is T);
    }
}

using Content.Shared.DeadSpace.Celestial;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.Celestial;

/// <summary>
/// Компонент босса Селестиала. Разрушает всё вокруг себя, медленно движется,
/// изредка "говорит" (субтитры + звук) и проводит атаки лучами и сферами.
/// </summary>
[RegisterComponent]
public sealed partial class CelestialComponent : Component
{
    /// <summary>Звуки "речи" Селестиала (проигрываются со случайным субтитром).</summary>
    [DataField]
    public List<SoundSpecifier> TalkSounds = new();

    /// <summary>Звук одиночного луча (вариации атаки "ПАДИ").</summary>
    [DataField]
    public SoundSpecifier? VariationSound;

    /// <summary>Звук луча (второй удар).</summary>
    [DataField]
    public SoundSpecifier? BeamSound;

    /// <summary>Звук финального залпа из трёх лучей.</summary>
    [DataField]
    public SoundSpecifier? VolleySound;

    /// <summary>Звук призыва сфер.</summary>
    [DataField]
    public SoundSpecifier? TeleportSound;

    /// <summary>Крик перед залпом лучей — единственная реплика Селестиала.</summary>
    [DataField]
    public string AttackLine = "ПАДИ.";

    /// <summary>Минимальная пауза между атаками, сек.</summary>
    [DataField]
    public float AttackMinDelay = 4f;

    /// <summary>Максимальная пауза между атаками, сек.</summary>
    [DataField]
    public float AttackMaxDelay = 8f;

    /// <summary>Громкость звуков атаки (меньше — тише).</summary>
    [DataField]
    public float AttackVolume = -8f;

    /// <summary>Насколько дальше игрока тянется розовый шнур, метры.</summary>
    [DataField]
    public float BeamOvershoot = 24f;

    /// <summary>Сколько держится чёрный луч, сек.</summary>
    [DataField]
    public float DarkBeamDuration = 1f;

    /// <summary>Минимальная дистанция точки запуска луча от игрока, метры.</summary>
    [DataField]
    public float BeamStartMinDist = 12f;

    /// <summary>Максимальная дистанция точки запуска луча от игрока, метры.</summary>
    [DataField]
    public float BeamStartMaxDist = 18f;

    /// <summary>Радиус урона удара луча, метры.</summary>
    [DataField]
    public float BeamDamageRadius = 1.2f;

    /// <summary>Количество одиночных лучей перед финальным залпом.</summary>
    [DataField]
    public int BeamCount = 3;

    /// <summary>Пауза между одиночными лучами, сек.</summary>
    [DataField]
    public float BeamDelay = 1f;

    /// <summary>Задержка между телеграфом луча и ударом, сек.</summary>
    [DataField]
    public float BeamChargeTime = 1f;

    /// <summary>Сколько лучей в финальном залпе.</summary>
    [DataField]
    public int FinalBeamCount = 3;

    /// <summary>Сколько игроков становится целью сфер.</summary>
    [DataField]
    public int SphereTargets = 3;

    /// <summary>Сколько дополнительных игроков получают копию каждой атаки.</summary>
    [DataField]
    public int ExtraTargets = 10;

    // ----- вторая атака: ТЩЕТНО -----

    /// <summary>Сколько целей получает трещины и шары.</summary>
    [DataField]
    public int FutilityTargets = 5;

    /// <summary>Сколько раз повторяется последовательность трещина-шар.</summary>
    [DataField]
    public int FutilityRepeats = 4;

    /// <summary>Пауза между повторами, сек.</summary>
    [DataField]
    public float FutilityRepeatDelay = 1.2f;

    /// <summary>Скорость шара, м/с.</summary>
    [DataField]
    public float OrbSpeed = 9f;

    /// <summary>Максимальное время жизни шара, сек.</summary>
    [DataField]
    public float OrbLifetime = 12f;

    /// <summary>Сколько шаров выходит из одной трещины.</summary>
    [DataField]
    public int OrbsPerCrack = 3;

    /// <summary>Звук открытия рифта.</summary>
    [DataField]
    public SoundSpecifier? FutilityCrackSound;

    /// <summary>Звуки выстрела/полёта шара.</summary>
    [DataField]
    public List<SoundSpecifier> FutilityOrbSounds = new();

    /// <summary>Как часто шар стреляет мелкими лучами, сек.</summary>
    [DataField]
    public float OrbFireInterval = 0.18f;

    /// <summary>Минимальный урон мелкого луча.</summary>
    [DataField]
    public float SmallBeamMinDamage = 10f;

    /// <summary>Максимальный урон мелкого луча.</summary>
    [DataField]
    public float SmallBeamMaxDamage = 20f;

    /// <summary>Сколько раз повторяется Cutter.</summary>
    [DataField]
    public int CutterRepeats = 4;

    /// <summary>Пауза между повторами Cutter, сек.</summary>
    [DataField]
    public float CutterRepeatDelay = 2.5f;

    /// <summary>Длина лучей Cutter, метры.</summary>
    [DataField]
    public float CutterLength = 120f;

    /// <summary>Время вращения лучей Cutter, сек.</summary>
    [DataField]
    public float CutterRotateTime = 2f;

    /// <summary>Звук вращения Cutter.</summary>
    [DataField]
    public SoundSpecifier? CutterChargeSound;

    /// <summary>Звук активации Cutter.</summary>
    [DataField]
    public SoundSpecifier? CutterImpactSound;

    /// <summary>Финальный звук последнего Cutter.</summary>
    [DataField]
    public SoundSpecifier? CutterFinalSound;

    /// <summary>Стартовая катсцена уже сыграна.</summary>
    [ViewVariables]
    public bool CutscenePlayed;

    /// <summary>Субтитры и катсцены видят только игроки на карте босса.</summary>
    [DataField]
    public bool LocalizedEvents;

    /// <summary>Сколько держится удар Cutter, сек.</summary>
    [DataField]
    public float CutterFireTime = 1f;

    /// <summary>Урон луча Cutter.</summary>
    [DataField]
    public float CutterDamage = 60f;

    // ----- третья атака: РАЗНЕСУ -----

    /// <summary>Сколько кружков раскидывается по станции.</summary>
    [DataField]
    public int CircleCount = 100;

    /// <summary>Минимальная дистанция кружков от босса, метры.</summary>
    [DataField]
    public float CircleMinDist = 6f;

    /// <summary>Максимальная дистанция кружков от босса, метры.</summary>
    [DataField]
    public float CircleMaxDist = 38f;

    /// <summary>Счётчик цикла атак.</summary>
    [DataField]
    public int AttackCycle;

    /// <summary>Время дрейфа кружков по умолчанию, сек.</summary>
    [DataField]
    public float CircleWanderTime = 5f;

    // ----- пятая атака: ВСПЫХНИ ВО ТЬМУ -----

    /// <summary>Крик перед волней сфер.</summary>
    [DataField]
    public string FlashLine = "ВСПЫХНИ ВО ТЬМУ.";

    /// <summary>Множитель количества сфер.</summary>
    [DataField]
    public float FlashSphereMultiplier = 2.5f;

    // ----- шестая атака: УВЯНЬ В ЦВЕТУ -----

    /// <summary>Крик перед гигантскими лучами.</summary>
    [DataField]
    public string BloomLine = "УВЯНЬ В ЦВЕТУ.";

    /// <summary>Время зарядки гигантских лучей, сек.</summary>
    [DataField]
    public float BloomChargeTime = 5f;

    /// <summary>Время обхода карты лучами, сек.</summary>
    [DataField]
    public float BloomFireTime = 15f;

    /// <summary>Урон за тик гигантского луча.</summary>
    [DataField]
    public float BloomBeamDamage = 20f;

    /// <summary>Интервал урона гигантского луча, сек.</summary>
    [DataField]
    public float BloomDamageInterval = 0.5f;

    /// <summary>Звук зарядки.</summary>
    [DataField]
    public SoundSpecifier? BloomChargeSound;

    /// <summary>Звук выстрела.</summary>
    [DataField]
    public SoundSpecifier? BloomFireSound;

    /// <summary>Крик "ЗАМРИ." перед морем лучей.</summary>
    [DataField]
    public string FreezeLine = "ЗАМРИ.";

    /// <summary>Сколько замерших лучей в атаке ЗАМРИ.</summary>
    [DataField]
    public int FreezeBeamCount = 100;

    /// <summary>Сколько кружков в синхронном взрыве ЗАМРИ.</summary>
    [DataField]
    public int FreezeCircleCount = 45;

    /// <summary>Урон замершего луча при почернении.</summary>
    [DataField]
    public float FreezeBeamDamage = 40f;

    // ----- вторая фаза -----

    /// <summary>Фаза 2: босс бессмертен, урон сбрасывается.</summary>
    [ViewVariables]
    public bool Phase2;

    /// <summary>Минимальная пауза между атаками во второй фазе, сек.</summary>
    [DataField]
    public float Phase2AttackMinDelay = 3f;

    /// <summary>Максимальная пауза между атаками во второй фазе, сек.</summary>
    [DataField]
    public float Phase2AttackMaxDelay = 6f;

    /// <summary>Сколько дополнительных игроков атакуется во второй фазе.</summary>
    [DataField]
    public int Phase2ExtraTargets = 20;

    /// <summary>Минимум сфер вокруг каждого игрока.</summary>
    [DataField]
    public int SpheresPerTargetMin = 6;

    /// <summary>Максимум сфер вокруг каждого игрока.</summary>
    [DataField]
    public int SpheresPerTargetMax = 9;

    /// <summary>Минимальная дистанция спавна сфер, метры.</summary>
    [DataField]
    public float SphereSpawnMinDist = 3.2f;

    /// <summary>Максимальная дистанция спавна сфер, метры.</summary>
    [DataField]
    public float SphereSpawnMaxDist = 4.6f;

    /// <summary>Минимальное время жизни сферы, сек.</summary>
    [DataField]
    public float SphereLifetimeMin = 2.0f;

    /// <summary>Максимальное время жизни сферы, сек.</summary>
    [DataField]
    public float SphereLifetimeMax = 3.2f;

    /// <summary>Задержка после залпа перед призывом сфер, сек.</summary>
    [DataField]
    public float SpheresAfterAttackDelay = 1.5f;

    /// <summary>Накопитель для разрушения тайлов.</summary>
    [ViewVariables]
    public float DestroyAccumulator;

    /// <summary>Таймер следующей атаки.</summary>
    [ViewVariables]
    public float AttackTimer = 30f;

    /// <summary>Идёт ли сейчас атака (не начинать новую).</summary>
    [ViewVariables]
    public bool Attacking;
}

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>Начало стартовой катсцены Селестиала.</summary>
[Serializable, NetSerializable]
public sealed class CelestialCutsceneStartEvent : EntityEventArgs
{
}

/// <summary>Смена стадии спрайта Селестиала в катсцене (0=idle, 1=глаза, 2=разрыв).</summary>
[Serializable, NetSerializable]
public sealed class CelestialSpiritStageEvent(int stage) : EntityEventArgs
{
    public int Stage = stage;
}

/// <summary>Конец стартовой катсцены Селестиала.</summary>
[Serializable, NetSerializable]
public sealed class CelestialCutsceneEndEvent : EntityEventArgs
{
}

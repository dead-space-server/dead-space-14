using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>Начало катсцены смерти Селестиала (белый экран).</summary>
[Serializable, NetSerializable]
public sealed class CelestialDeathEvent : EntityEventArgs
{
}

/// <summary>Фраза на белом экране: чёрный текст, белые прямоугольники.</summary>
[Serializable, NetSerializable]
public sealed class CelestialDeathSpeakEvent(string text, float duration) : EntityEventArgs
{
    public string Text = text;
    public float Duration = duration;
}

/// <summary>Конец катсцены смерти.</summary>
[Serializable, NetSerializable]
public sealed class CelestialDeathEndEvent : EntityEventArgs
{
}

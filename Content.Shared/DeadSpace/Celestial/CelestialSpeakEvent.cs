using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Сервер сообщает клиенту, что Селестиал "говорит": показать субтитр.
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialSpeakEvent(string text, float duration) : EntityEventArgs
{
    public string Text = text;
    public float Duration = duration;
}

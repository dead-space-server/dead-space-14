using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Сервер сообщает клиенту, что Селестиал "говорит": показать субтитр.
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialSpeakEvent(string text, float duration, int style = 0) : EntityEventArgs
{
    public string Text = text;
    public float Duration = duration;

    /// <summary>Оформление субтитра: 0 - обычный розовый, 3 - красные буквы.</summary>
    public int Style = style;
}

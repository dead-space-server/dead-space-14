using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.Celestial;

/// <summary>
/// Сервер сообщает клиенту, что Селестиал "говорит": показать субтитр.
/// </summary>
[Serializable, NetSerializable]
public sealed class CelestialSpeakEvent(string text, float duration, Color? color = null, int style = 0) : EntityEventArgs
{
    public string Text = text;
    public float Duration = duration;

    // DS14: text colour override (the mad miner speaks in red). Null - the Celestial pink.
    public Color? Color = color;

    /// <summary>Оформление субтитра: 0 - обычный розовый, 3 - красные буквы.</summary>
    public int Style = style;
}

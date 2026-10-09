using Robust.Shared.Serialization;

namespace Content.Shared.Sectants;

[Serializable, NetSerializable]
public sealed class SectantAnnouncementEvent : EntityEventArgs
{
    public string Message = string.Empty;
    public Color Color = Color.Red;
    public float FlashDuration = 2.5f;
    public float ShakeIntensity = 6f;
}

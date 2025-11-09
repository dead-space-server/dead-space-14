using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AntiAlcohol;

[Serializable, NetSerializable]
public sealed partial class AlcoScanDoAfterEvent : SimpleDoAfterEvent
{
}
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[Serializable, NetSerializable]
public sealed partial class NinjaAiHackDoAfterEvent : SimpleDoAfterEvent { }
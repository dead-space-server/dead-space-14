using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AshWalkers;

[Serializable, NetSerializable]
public sealed partial class AshWalkerSacrificeDoAfterEvent : SimpleDoAfterEvent;

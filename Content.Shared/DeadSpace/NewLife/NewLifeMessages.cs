// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.NewLife;

[Serializable, NetSerializable]
public enum NewLifeAvailability : byte
{
    NoAccess,
    Unavailable,
    Waiting,
    Available,
    Used,
}

[Serializable, NetSerializable]
public readonly record struct NewLifeState(NewLifeAvailability Availability, TimeSpan? AvailableAt = null, string? Reason = null);

[Serializable, NetSerializable]
public sealed class NewLifeStateRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NewLifeOpenEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class NewLifeStateEvent(NewLifeState state) : EntityEventArgs
{
    public readonly NewLifeState State = state;
}

[Serializable, NetSerializable]
public sealed class NewLifeEuiState(NewLifeState state, TimeSpan confirmAt) : EuiStateBase
{
    public readonly NewLifeState State = state;
    public readonly TimeSpan ConfirmAt = confirmAt;
}

[Serializable, NetSerializable]
public sealed class NewLifeConfirmMessage : EuiMessageBase;

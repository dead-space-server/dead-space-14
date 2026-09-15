// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.BSAConsole;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BluespaceArtilleryComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsReady = true;

    [DataField, AutoNetworkedField]
    public bool HasPendingShot;

    [DataField, AutoNetworkedField]
    public float CooldownEnd;

    [DataField]
    public float CooldownDuration = 60f;

    [DataField]
    public float PendingShotDelay = 10f;

    public float PendingShotEnd;
    public int PendingShotMapId = -1;
    public Vector2 PendingShotWorldPosition;
    public EntityUid? PendingShotGridUid;
    public Vector2 PendingShotGridLocalPosition;
}

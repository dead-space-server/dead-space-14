using Content.Shared.DeadSpace.StationAI.UI;
using Content.Shared.Silicons.StationAi;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.StationAI.UI;

/// <summary>
///     Initializes a <see cref="AICameraList"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class AICameraListBoundUserInterface : BoundUserInterface
{
    public AICameraList? Window;
    [Dependency] private readonly IGameTiming _timing = default!; // DS14

    public AICameraListBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();
        Window?.Close();
        EntityUid? gridUid = null;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;
        }

        Window = new AICameraList(gridUid, Owner);
        Window.OpenCentered();
        Window.OnClose += Close;
        Window.WarpToCamera += WindowOnWarpToCamera;
        Window.SearchRequested += WindowOnSearchRequested;
        Window.WarpToTarget += WindowOnWarpToTarget;
    }

    private void WindowOnWarpToCamera(NetEntity obj)
    {
        SendMessage(new EyeMoveToCam { Entity = EntMan.GetNetEntity(Owner), Uid = obj });
    }

    private void WindowOnSearchRequested(string query, AiCameraSearchType type)
    {
        SendMessage(new AiCameraSearchRequestMessage
        {
            Entity = EntMan.GetNetEntity(Owner),
            Query = query,
            Type = type,
        });
    }

    private void WindowOnWarpToTarget(NetEntity obj)
    {
        SendMessage(new AiCameraJumpToTargetMessage
        {
            Entity = EntMan.GetNetEntity(Owner),
            Target = obj,
            SourceTime = _timing.CurTime
        });
    }

    public void UpdateCameras()
    {
        Window?.UpdateCameras();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AiCameraSearchResultsState searchState)
            Window?.UpdateSearchResults(searchState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (Window != null)
        {
            Window.WarpToCamera -= WindowOnWarpToCamera;
            Window.SearchRequested -= WindowOnSearchRequested;
            Window.WarpToTarget -= WindowOnWarpToTarget;
        }

        Window?.Parent?.RemoveChild(Window);
    }
}

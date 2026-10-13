using System.IO;
using Content.Client.DeadSpace.CameraArchives; //DS-14
using Content.Client.Eye;
using Content.Shared.DeadSpace.CameraArchives; //DS-14
using Content.Shared.SurveillanceCamera;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Utility; //DS-14
using SixLabors.ImageSharp; //DS-14

namespace Content.Client.SurveillanceCamera.UI;

public sealed class SurveillanceCameraMonitorBoundUserInterface : BoundUserInterface
{
    private readonly EyeLerpingSystem _eyeLerpingSystem;
    private readonly SurveillanceCameraMonitorSystem _surveillanceCameraMonitorSystem;

    [ViewVariables]
    private SurveillanceCameraMonitorWindow? _window;

    [ViewVariables]
    private EntityUid? _currentCamera;
    //DS-14 start
    private readonly CameraArchivePlaybackSystem _playback;
    private SurveillanceCameraMonitorUiState? _live;
    private SurveillanceCameraArchiveBrowseMessage? _archive;
    //DS-14 end

    public SurveillanceCameraMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerpingSystem = EntMan.System<EyeLerpingSystem>();
        _surveillanceCameraMonitorSystem = EntMan.System<SurveillanceCameraMonitorSystem>();
        _playback = EntMan.System<CameraArchivePlaybackSystem>(); //DS-14
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SurveillanceCameraMonitorWindow>();
        _window.SetArchiveEnabled(EntMan.HasComponent<CameraArchiveConsoleComponent>(Owner)); //DS-14

        _window.CameraSelected += OnCameraSelected;
        _window.SubnetOpened += OnSubnetRequest;
        _window.CameraRefresh += OnCameraRefresh;
        _window.SubnetRefresh += OnSubnetRefresh;
        _window.CameraSwitchTimer += OnCameraSwitchTimer;
        _window.CameraDisconnect += OnCameraDisconnect;
        //DS-14 start
        _window.ArchiveOpened += () => SendMessage(new SurveillanceCameraArchiveOpenMessage());
        _window.ArchiveClosed += RestoreLive;
        _window.ArchiveView.CameraPicked += OnArchiveCamera;
        _window.ArchiveView.FramePicked += OnArchiveFrame;
        _window.ArchiveView.PrintPressed += OnArchivePrint;
        _window.ArchiveView.SearchChanged += _ => ShowArchiveList();
        //DS-14 end

        var xform = EntMan.GetComponent<TransformComponent>(Owner);
        var gridUid = xform.GridUid ?? xform.MapUid;

        if (gridUid is not null)
            _window?.SetMap(gridUid.Value);
    }

    private void OnCameraSelected(string address, string? subnet)
    {
        SendMessage(new SurveillanceCameraMonitorSwitchMessage(address, subnet));
    }

    private void OnSubnetRequest(string subnet)
    {
        SendMessage(new SurveillanceCameraMonitorSubnetRequestMessage(subnet));
    }

    private void OnCameraSwitchTimer()
    {
        _surveillanceCameraMonitorSystem.AddTimer(Owner, _window!.OnSwitchTimerComplete);
    }

    private void OnCameraRefresh()
    {
        SendMessage(new SurveillanceCameraRefreshCamerasMessage());
    }

    private void OnSubnetRefresh()
    {
        SendMessage(new SurveillanceCameraRefreshSubnetsMessage());
    }

    private void OnCameraDisconnect()
    {
        SendMessage(new SurveillanceCameraDisconnectMessage());
    }

    //DS-14 start
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (_window == null || message is not SurveillanceCameraArchiveBrowseMessage browse)
            return;

        _archive = browse;
        ShowArchiveList();

        _window.ArchiveView.SetChat(browse.Frame?.Chat, browse.BodyLog);

        if (browse.Covered && browse.FrameCount > 0 && browse.FrameIndex >= browse.FrameCount - 1)
        {
            _playback.Clear();
            _window.ArchiveView.ShowCover(browse.FrameIndex, browse.FrameCount, browse.FrameTime);
            return;
        }

        if (browse.Frame == null || browse.Frame.Interference)
        {
            _playback.Clear();
            _window.ArchiveView.ShowStatic(browse.FrameIndex, browse.FrameCount, browse.FrameTime, browse.Frame != null);
            return;
        }

        var view = _playback.RequestPlay(browse.Frame);
        if (view?.Eye != null)
            _window.ArchiveView.ShowFrame(view.Eye, browse.FrameIndex, browse.FrameCount, browse.FrameTime);
    }

    private void ShowArchiveList()
    {
        if (_window == null || _archive == null)
            return;

        var query = _window.ArchiveView.Search.Text.Trim();
        var rows = new List<(string Name, bool Offline, bool Burned, int Index)>();
        for (var i = 0; i < _archive.Cameras.Length; i++)
        {
            var entry = _archive.Cameras[i];
            if (query.Length > 0 && !entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            rows.Add((entry.Name.Replace('[', '(').Replace(']', ')'), entry.Offline, entry.Burned, i));
        }

        _window.ArchiveView.ShowCameras(rows);
    }

    private void OnArchiveCamera(int row)
    {
        if (_archive == null || row < 0 || row >= _archive.Cameras.Length)
            return;

        SendMessage(new SurveillanceCameraArchivePickMessage
        {
            Camera = _archive.Cameras[row].Camera,
            FrameIndex = -1,
        });
    }

    private void OnArchiveFrame(int index)
    {
        if (_archive == null)
            return;

        SendMessage(new SurveillanceCameraArchivePickMessage
        {
            Camera = _archive.Selected,
            FrameIndex = index,
        });
    }

    private void OnArchivePrint()
    {
        if (_window == null || _archive == null || _archive.FrameCount <= 0)
            return;

        var camera = _archive.Selected;
        var index = _archive.FrameIndex;
        _window.ArchiveView.View.Screenshot(image =>
        {
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            var bytes = stream.ToArray();
            if (bytes.Length == 0 || bytes.Length > CameraArchivePrintMessage.MaxImageBytes)
                return;

            SendMessage(new CameraArchivePrintMessage
            {
                Camera = camera,
                FrameIndex = index,
                Image = bytes,
            });
        });
    }
    //DS-14 end

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SurveillanceCameraMonitorUiState cast)
            return;

        _live = cast;
        if (_window.ArchiveTabOpen) //DS-14
            return;

        ApplyLive(cast);
    }

    private void RestoreLive()
    {
        if (_live != null)
            ApplyLive(_live);
    }

    private void ApplyLive(SurveillanceCameraMonitorUiState cast)
    {
        if (_window == null)
            return;

        var active = EntMan.GetEntity(cast.ActiveCamera);

        if (active == null)
        {
            _window.UpdateState(null, cast.Subnets, cast.ActiveAddress, cast.ActiveSubnet, cast.Cameras);

            if (_currentCamera != null)
            {
                _surveillanceCameraMonitorSystem.RemoveTimer(Owner);
                _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
                _currentCamera = null;
            }
        }
        else
        {
            if (_currentCamera == null)
            {
                _eyeLerpingSystem.AddEye(active.Value);
                _currentCamera = active;
            }
            else if (_currentCamera != active)
            {
                _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
                _eyeLerpingSystem.AddEye(active.Value);
                _currentCamera = active;
            }

            if (EntMan.TryGetComponent<EyeComponent>(active, out var eye))
            {
                _window.UpdateState(eye.Eye, cast.Subnets, cast.ActiveAddress, cast.ActiveSubnet, cast.Cameras);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_currentCamera != null)
        {
            _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
            _currentCamera = null;
        }
    }
}

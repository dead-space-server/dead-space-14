// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed partial class RoundEndCreditsOverlay : LayoutContainer
{
    public const float IntroSeconds = 7f;
    private const float FadeOutSeconds = 1.5f;
    private const float PanelFadeSeconds = 0.6f;
    private const float EdgeFade = 110f;
    private const float MaxContentWidth = 1000f;
    private const float MinContentWidth = 560f;
    private const float ChatWidth = 420f;
    private const float ChatHeight = 230f;
    private const float ScreenPadding = 16f;
    private const int StarCount = 180;

    private const string RegularFont = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    private const string ItalicFont = "/Fonts/NotoSans/NotoSans-Italic.ttf";
    private const string BoldFont = "/Fonts/NotoSansDisplay/NotoSansDisplay-Bold.ttf";

    private static readonly Color Gold = Color.FromHex("#ffd166");
    private static readonly Color Blood = Color.FromHex("#e05252");
    private static readonly Color Victory = Color.FromHex("#6fcf97");
    private static readonly Color Partial = Color.FromHex("#f2994a");
    private static readonly Color TextColor = Color.FromHex("#e8e8e8");
    private static readonly Color DimColor = Color.FromHex("#8a93a0");
    private static readonly Color Cyan = Color.FromHex("#7fd3e6");
    private static readonly Color CardBackground = Color.FromHex("#141922");
    private static readonly Color CardHover = Color.FromHex("#1e2633");

    private readonly IGameTiming _timing;
    private readonly IEntityManager _entMan;
    private readonly IResourceCache _cache;
    private readonly IRobustRandom _random;
    private readonly ILocalizationManager _loc = IoCManager.Resolve<ILocalizationManager>();
    private readonly RoundEndDollPreviewSystem _dollPreviews;
    private readonly RoundEndMessageEvent _round;
    private readonly int _dollOwner;
    private readonly List<RoundEndCreditsDollView> _dolls = new();
    private readonly Star[] _stars = new Star[StarCount];

    private readonly TimeSpan _openTime;
    private readonly float _fadeInSeconds;
    private TimeSpan? _closeStart;
    private bool _closeQueued;
    private float _blackAlpha;
    private float _scrollSpeed;

    private RoundEndCreditsStartEvent? _credits;
    private TimeSpan _creditsStart;
    private ArenaManifestEvent? _arena;

    private readonly PanelContainer _chatPanel;
    private readonly ChatBox _chat;
    private ChatBox? _demotedChat;

    private readonly CreditsScrollHost _scrollHost;
    private BoxContainer? _content;

    private readonly Button _skipButton;
    private bool _skipped;

    private ArchitectMvpShowcase? _architectShowcase;
    private Control? _architectCard;
    private bool _architectShown;

    public event Action? Closed;
    public event Action<int, int>? VoteCast;
    public event Action? Skipped;

    public RoundEndCreditsOverlay(
        RoundEndMessageEvent round,
        TimeSpan fadeEnd,
        IEntityManager entMan,
        IGameTiming timing,
        IResourceCache cache,
        IRobustRandom random,
        ChatBox chat)
    {
        _round = round;
        _entMan = entMan;
        _timing = timing;
        _cache = cache;
        _random = random;
        _dollPreviews = entMan.System<RoundEndDollPreviewSystem>();
        _dollOwner = _dollPreviews.CreateOwner();

        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;

        _openTime = timing.CurTime;
        _fadeInSeconds = Math.Max(0.01f, (float) (fadeEnd - _openTime).TotalSeconds);

        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i] = new Star(
                random.NextFloat(),
                random.NextFloat(),
                random.NextFloat(0.6f, 2.4f),
                random.NextFloat(0f, MathF.Tau),
                random.NextFloat(0.2f, 1f));
        }

        _scrollHost = new CreditsScrollHost { MouseFilter = MouseFilterMode.Ignore };
        AddChild(_scrollHost);
        SetAnchorPreset(_scrollHost, LayoutPreset.Wide);

        _chatPanel = new PanelContainer
        {
            SetSize = new Vector2(ChatWidth, ChatHeight),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Black.WithAlpha(0.65f),
                BorderColor = Color.White.WithAlpha(0.1f),
                BorderThickness = new Thickness(1),
            },
            Modulate = Color.Transparent,
        };
        _chat = chat;
        _chat.Orphan();
        _chat.MinSize = Vector2.Zero;
        _chatPanel.AddChild(_chat);
        AddChild(_chatPanel);
        SetAnchorPreset(_chatPanel, LayoutPreset.BottomLeft);
        SetMarginLeft(_chatPanel, ScreenPadding);
        SetMarginBottom(_chatPanel, -ScreenPadding);
        SetGrowVertical(_chatPanel, GrowDirection.Begin);
        _chat.Repopulate();

        if (UserInterfaceManager.ActiveScreen?.GetWidget<ChatBox>() is { Main: true } mainChat)
        {
            mainChat.Main = false;
            _demotedChat = mainChat;
        }

        _chat.Main = true;

        _skipButton = new Button
        {
            Text = Loc.GetString("round-end-credits-skip"),
            Visible = false,
        };
        _skipButton.OnPressed += _ => Skip();
        AddChild(_skipButton);
        SetAnchorPreset(_skipButton, LayoutPreset.TopRight);
        SetMarginRight(_skipButton, -ScreenPadding);
        SetMarginTop(_skipButton, ScreenPadding);
        SetGrowHorizontal(_skipButton, GrowDirection.Begin);
    }

    private void Skip()
    {
        if (_skipped)
            return;

        _skipped = true;
        _skipButton.Visible = false;
        UserInterfaceManager.DeferAction(() => Skipped?.Invoke());
    }

    private float ScreenWidth => Size.X > 0f ? Size.X : UserInterfaceManager.RootControl.Size.X;

    private float GetCenterWidth(float screenWidth)
    {
        var free = screenWidth - 2f * (ChatWidth + ScreenPadding * 2f);
        return Math.Clamp(free, Math.Min(MinContentWidth, screenWidth * 0.9f), MaxContentWidth);
    }

    public int RoundId => _round.RoundId;

    public bool HasCredits => _credits != null;

    public void SetArena(ArenaManifestEvent arena)
    {
        _arena = arena;
    }

    public void SetCredits(RoundEndCreditsStartEvent credits, TimeSpan startTime)
    {
        if (_credits != null)
            return;

        _credits = credits;
        _creditsStart = startTime;
        BuildIntro();

        _content = BuildContent();
        _content.Visible = false;
        _scrollHost.AddChild(_content);
    }

    public void BeginClose()
    {
        _closeStart ??= _timing.CurTime;
    }

    private void AddLayer(Control layer)
    {
        AddChild(layer);
        layer.SetPositionInParent(_scrollHost.GetPositionInParent() + 1);
    }

    public void HostWindow(BaseWindow window)
    {
        if (window.Parent != this)
        {
            window.Orphan();
            AddChild(window);
        }

        window.MoveToFront();
    }

    public void Shutdown()
    {
        foreach (var doll in _dolls)
        {
            doll.Release();
        }

        _dolls.Clear();
        _dollPreviews.Cancel(_dollOwner);

        _chat.Main = false;
        if (_demotedChat is { Disposed: false } demoted)
            demoted.Main = true;

        foreach (var window in Children.OfType<BaseWindow>().ToArray())
        {
            window.Orphan();
            UserInterfaceManager.WindowRoot.AddChild(window);
        }

        _chat.Orphan();
        Orphan();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.CurTime;
        var time = (float) (now - _openTime).TotalSeconds;
        _blackAlpha = Math.Clamp(time / _fadeInSeconds, 0f, 1f);

        if (_closeStart is { } closeStart)
        {
            var alpha = 1f - (float) (now - closeStart).TotalSeconds / FadeOutSeconds;
            Modulate = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));
            if (alpha <= 0f)
            {
                if (!_closeQueued)
                {
                    _closeQueued = true;
                    UserInterfaceManager.DeferAction(() => Closed?.Invoke());
                }

                return;
            }
        }

        _chatPanel.Modulate = Color.White.WithAlpha(_blackAlpha);

        UpdateStars(args.DeltaSeconds);
        UpdateVote(now);
        UpdateCredits(now, args.DeltaSeconds);

        foreach (var doll in _dolls)
        {
            doll.Animate(time);
        }

        UpdateArchitectShowcase();
    }

    private void UpdateCredits(TimeSpan now, float frameTime)
    {
        if (_credits == null)
            return;

        var time = (float) (now - _creditsStart).TotalSeconds;
        var skippable = !_skipped && _closeStart == null && time >= 0f;
        if (_skipButton.Visible != skippable)
            _skipButton.Visible = skippable;

        if (time < 0f)
            return;

        UpdateIntro(time);

        var scrollTime = time - IntroSeconds;
        var scrollDuration = Math.Max(1f, _credits.ContentDuration - IntroSeconds);

        if (_content != null)
        {
            var height = Size.Y;
            var width = GetCenterWidth(Size.X);
            var contentHeight = _scrollHost.ContentHeight;
            var progress = Math.Clamp(scrollTime / scrollDuration, 0f, 1f);
            var y = height - (height + contentHeight) * progress;

            _scrollSpeed = (height + contentHeight) / scrollDuration;
            var visible = scrollTime >= 0f && progress < 1f;
            if (_content.Visible != visible)
                _content.Visible = visible;

            _scrollHost.SetLayout(width, y);

            if (_content.Visible)
                ApplyEdgeFade(_content, y, height);
        }
        else
        {
            _scrollSpeed = 0f;
        }

        if (time >= _credits.ContentDuration)
            UpdateWall(time - _credits.ContentDuration, frameTime);
    }

    private static void ApplyEdgeFade(BoxContainer content, float contentY, float height)
    {
        foreach (var child in content.Children)
        {
            var top = contentY + child.Position.Y;
            var bottom = top + child.Size.Y;
            var depth = Math.Min(bottom, height - top);
            var alpha = Math.Clamp(depth / EdgeFade, 0f, 1f);
            child.Modulate = Color.White.WithAlpha(alpha * alpha);
        }
    }

    private void UpdateStars(float frameTime)
    {
        var drift = 0.006f + (_scrollSpeed > 0f ? _scrollSpeed / Math.Max(1f, Size.Y) * 0.35f : 0f);
        for (var i = 0; i < _stars.Length; i++)
        {
            ref var star = ref _stars[i];
            star.Y -= drift * star.Depth * frameTime;
            star.Phase += frameTime * (0.8f + star.Depth);
            if (star.Y < 0f)
            {
                star.Y += 1f;
                star.X = _random.NextFloat();
            }
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Color.Black.WithAlpha(_blackAlpha));

        if (_blackAlpha <= 0.5f)
            return;

        var starAlpha = (_blackAlpha - 0.5f) * 2f;
        foreach (var star in _stars)
        {
            var twinkle = 0.55f + 0.45f * MathF.Sin(star.Phase);
            var x = star.X * size.X;
            var y = star.Y * size.Y;
            var half = star.Size * UIScale / 2f;
            var color = Color.FromHsv(new Vector4(0.58f, 0.15f * star.Depth, 1f, starAlpha * twinkle * star.Depth));
            handle.DrawRect(new UIBox2(x - half, y - half, x + half, y + half), color);
        }
    }

    private Font GetFont(int size, string path = RegularFont)
    {
        return _cache.GetFont(path, size);
    }

    private Label MakeLabel(string text, int size, Color color, string font = RegularFont, bool center = true)
    {
        return new Label
        {
            Text = text,
            FontOverride = GetFont(size, font),
            FontColorOverride = color,
            Align = center ? Label.AlignMode.Center : Label.AlignMode.Left,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
    }

    private struct Star(float x, float y, float size, float phase, float depth)
    {
        public float X = x;
        public float Y = y;
        public readonly float Size = size;
        public float Phase = phase;
        public readonly float Depth = depth;
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldSearchBox : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IClipboardManager _clipboard = default!;

    private const int BaseFontSize = 10;

    private Font _font = default!;
    private string _text = "";
    private string _placeholder = "SEARCH...";
    private int _cursorPosition;
    private int _selectionStart;
    private int _drawOffset;

    private CursorBlink _blink;
    private bool _mouseSelectingText;
    private float _lastMousePosition;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _focusBorderColor = Color.FromHex("#6d5a8a");
    private readonly Color _textColor = Color.FromHex("#c0b3da");
    private readonly Color _placeholderColor = Color.FromHex("#6d5a8a");
    private readonly Color _cursorColor = Color.FromHex("#a589c9");
    private readonly Color _selectionColor = Color.FromHex("#6d5a8a");

    public event Action<string>? OnTextChanged;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;
            _text = value;
            _cursorPosition = Math.Min(_cursorPosition, _text.Length);
            _selectionStart = Math.Min(_selectionStart, _text.Length);
        }
    }

    public string Placeholder
    {
        get => _placeholder;
        set => _placeholder = value;
    }

    public EmeraldSearchBox()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseFontSize);
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        DefaultCursorShape = CursorShape.IBeam;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var height = _font.GetLineHeight(1f) + 12f;
        return new Vector2(availableSize.X, height);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        var borderColor = HasKeyboardFocus() ? _focusBorderColor : _borderColor;
        handle.DrawLine(rect.TopLeft, rect.TopRight, borderColor);
        handle.DrawLine(rect.TopRight, rect.BottomRight, borderColor);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, borderColor);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, borderColor);

        var padding = 6f * UIScale;
        var contentRect = new UIBox2(padding, 0, PixelSize.X - padding, PixelSize.Y);
        var offsetY = (PixelSize.Y - _font.GetLineHeight(UIScale)) / 2f;

        var displayText = string.IsNullOrEmpty(_text) ? _placeholder : _text;
        var displayColor = string.IsNullOrEmpty(_text) ? _placeholderColor : _textColor;

        var posX = 0f;
        var actualCursorPosition = 0f;
        var actualSelectionStartPosition = 0f;
        var count = 0;

        foreach (var rune in displayText.EnumerateRunes())
        {
            var metrics = _font.GetCharMetrics(rune, UIScale);
            if (metrics.HasValue)
            {
                posX += metrics.Value.Advance;
            }
            count += rune.Utf16SequenceLength;

            if (count == _cursorPosition)
                actualCursorPosition = posX;
            if (count == _selectionStart)
                actualSelectionStartPosition = posX;
        }

        var totalLength = posX;
        var end = totalLength - _drawOffset;
        if (end + 1 < contentRect.Width - padding * 2)
        {
            _drawOffset = Math.Max(0, _drawOffset - (int)(contentRect.Width - padding * 2 - end));
        }

        if (actualCursorPosition < _drawOffset)
        {
            _drawOffset -= (int)(_drawOffset - actualCursorPosition);
        }
        else if (actualCursorPosition >= contentRect.Width - padding * 2 + _drawOffset)
        {
            _drawOffset += (int)(actualCursorPosition - (contentRect.Width - padding * 2 + _drawOffset - 1));
        }

        actualCursorPosition -= _drawOffset;
        actualSelectionStartPosition -= _drawOffset;

        var baseLine = new Vector2(padding - _drawOffset, offsetY);

        foreach (var rune in displayText.EnumerateRunes())
        {
            var metrics = _font.GetCharMetrics(rune, UIScale);
            if (!metrics.HasValue)
                continue;

            if (baseLine.X > contentRect.Right)
                break;

            if (baseLine.X + metrics.Value.Width >= contentRect.Left)
            {
                handle.DrawString(_font, baseLine, rune.ToString(), UIScale, displayColor);
            }

            baseLine += new Vector2(metrics.Value.Advance, 0);
        }

        if (HasKeyboardFocus() && !string.IsNullOrEmpty(_text))
        {
            var selectionLower = Math.Min(actualSelectionStartPosition, actualCursorPosition);
            var selectionUpper = Math.Max(actualSelectionStartPosition, actualCursorPosition);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (selectionLower != selectionUpper)
            {
                var selectionPadding = 2f * UIScale;
                handle.DrawRect(
                    new UIBox2(padding + selectionLower, selectionPadding, padding + selectionUpper, PixelSize.Y - selectionPadding),
                    _selectionColor.WithAlpha(0.4f));
            }

            var cursorPadding = 2f * UIScale;
            var cursorWidth = 1f * UIScale;
            var cursorColor = _cursorColor.WithAlpha(_blink.Opacity);
            handle.DrawRect(
                new UIBox2(padding + actualCursorPosition, cursorPadding, padding + actualCursorPosition + cursorWidth, PixelSize.Y - cursorPadding),
                cursorColor);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _blink.FrameUpdate(args);

        if (_mouseSelectingText)
        {
            var padding = 6f * UIScale;
            var contentBox = new UIBox2(padding, 0, PixelSize.X - padding, PixelSize.Y);
            var index = GetIndexAtPos(MathHelper.Clamp(_lastMousePosition, contentBox.Left, contentBox.Right));
            _cursorPosition = index;
        }
    }

    protected override void TextEntered(GUITextEnteredEventArgs args)
    {
        base.TextEntered(args);
        InsertAtCursor(args.Text);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseSelectingText = true;
            _lastMousePosition = args.RelativePosition.X;
            var index = GetIndexAtPos(args.RelativePosition.X);
            _cursorPosition = index;
            _selectionStart = index;
            _blink.Reset();
            args.Handle();
            return;
        }

        if (!HasKeyboardFocus())
            return;

        if (args.Function == EngineKeyFunctions.TextBackspace)
        {
            if (_selectionStart != _cursorPosition)
            {
                var lower = Math.Min(_selectionStart, _cursorPosition);
                var upper = Math.Max(_selectionStart, _cursorPosition);
                _text = _text.Remove(lower, upper - lower);
                _cursorPosition = lower;
                _selectionStart = lower;
            }
            else if (_cursorPosition > 0)
            {
                _text = _text.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                _selectionStart = _cursorPosition;
            }
            OnTextChanged?.Invoke(_text);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextDelete)
        {
            if (_selectionStart != _cursorPosition)
            {
                var lower = Math.Min(_selectionStart, _cursorPosition);
                var upper = Math.Max(_selectionStart, _cursorPosition);
                _text = _text.Remove(lower, upper - lower);
                _cursorPosition = lower;
                _selectionStart = lower;
            }
            else if (_cursorPosition < _text.Length)
            {
                _text = _text.Remove(_cursorPosition, 1);
            }
            OnTextChanged?.Invoke(_text);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCursorLeft)
        {
            if (_cursorPosition > 0)
            {
                _cursorPosition--;
                _selectionStart = _cursorPosition;
            }
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCursorRight)
        {
            if (_cursorPosition < _text.Length)
            {
                _cursorPosition++;
                _selectionStart = _cursorPosition;
            }
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCursorBegin)
        {
            _cursorPosition = 0;
            _selectionStart = 0;
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCursorEnd)
        {
            _cursorPosition = _text.Length;
            _selectionStart = _text.Length;
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextSelectAll)
        {
            _cursorPosition = _text.Length;
            _selectionStart = 0;
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextPaste)
        {
            async void DoPaste()
            {
                var clipText = await _clipboard.GetText();
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (clipText != null)
                    InsertAtCursor(clipText);
            }
            DoPaste();
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCopy)
        {
            if (_selectionStart != _cursorPosition)
            {
                var lower = Math.Min(_selectionStart, _cursorPosition);
                var upper = Math.Max(_selectionStart, _cursorPosition);
                _clipboard.SetText(_text.Substring(lower, upper - lower));
            }
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCut)
        {
            if (_selectionStart != _cursorPosition)
            {
                var lower = Math.Min(_selectionStart, _cursorPosition);
                var upper = Math.Max(_selectionStart, _cursorPosition);
                _clipboard.SetText(_text.Substring(lower, upper - lower));
                _text = _text.Remove(lower, upper - lower);
                _cursorPosition = lower;
                _selectionStart = lower;
                OnTextChanged?.Invoke(_text);
            }
            args.Handle();
        }

        _blink.Reset();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseSelectingText = false;
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _lastMousePosition = args.RelativePosition.X;
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();
        _blink.Reset();
        Root?.Window?.TextInputStart();
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        Root?.Window?.TextInputStop();
    }

    private void InsertAtCursor(string text)
    {
        var chars = new List<char>();
        foreach (var chr in text)
        {
            if (chr == '\n' || chr == '\r')
                continue;
            chars.Add(chr);
        }

        var insertText = new string(chars.ToArray());
        var lower = Math.Min(_selectionStart, _cursorPosition);
        var upper = Math.Max(_selectionStart, _cursorPosition);

        _text = _text[..lower] + insertText + _text[upper..];
        _cursorPosition = lower + insertText.Length;
        _selectionStart = _cursorPosition;

        OnTextChanged?.Invoke(_text);
    }

    private int GetIndexAtPos(float horizontalPos)
    {
        var padding = 6f * UIScale;
        var clickPosX = horizontalPos;

        var index = 0;
        var chrPosX = padding - _drawOffset;
        var lastChrPosX = padding - _drawOffset;

        foreach (var rune in _text.EnumerateRunes())
        {
            var metrics = _font.GetCharMetrics(rune, UIScale);
            if (!metrics.HasValue)
            {
                index += rune.Utf16SequenceLength;
                continue;
            }

            if (chrPosX > clickPosX)
                break;

            lastChrPosX = chrPosX;
            chrPosX += metrics.Value.Advance;
            index += rune.Utf16SequenceLength;
        }

        var distanceRight = chrPosX - clickPosX;
        var distanceLeft = clickPosX - lastChrPosX;

        if (index > 0 && distanceRight > distanceLeft)
        {
            index--;
        }

        return index;
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }

    private struct CursorBlink
    {
        private const float BlinkTime = 1.3f;
        private const float HalfBlinkTime = BlinkTime / 2f;

        public float Opacity;
        public float Timer;

        public void Reset()
        {
            Timer = 0;
            UpdateOpacity();
        }

        public void FrameUpdate(FrameEventArgs args)
        {
            Timer += args.DeltaSeconds;
            UpdateOpacity();
        }

        private void UpdateOpacity()
        {
            if (Timer >= BlinkTime)
                Timer %= BlinkTime;

            if (Timer < HalfBlinkTime)
            {
                Opacity = 1f;
            }
            else
            {
                Opacity = 1f - (Timer - HalfBlinkTime) / HalfBlinkTime;
            }
        }
    }
}

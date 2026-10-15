using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;
using System.Linq;
using System.Numerics;

namespace Content.Client.DeadSpace.Celestial;

/// <summary>
/// Субтитры Селестиала: розовый текст шрифтом OMORI под центром экрана,
/// позади — мерцающие чёрные прямоугольники с рандомной альфой (глитч).
/// В конце блоки белеют, текст мигает белым и исчезает.
///
/// Рисуется вручную поверх UI-корня (<see cref="Robust.Client.UserInterface.IUserInterfaceManager.OnPostDrawUIRoot"/>),
/// а не оверлеем: оверлеи рисуются только внутри вьюпорта игрового экрана, поэтому в лобби
/// (до раунда, у убитых и наблюдателей) их не видно, а UI-корень рисуется всегда.
/// </summary>
public sealed class CelestialSubtitleRenderer
{
    private const string FontPath = "/Fonts/OMORI/OMORI_GAME_RUS.ttf";
    private const int FontSize = 104;
    private const float FlickerInterval = 0.09f;   // частота "сломанного стробоскопа"
    private const float AppearStagger = 0.5f;      // разброс времени появления блоков
    private const float EndDuration = 0.55f;        // финальная последовательность
    private const float BlinkPeriod = 0.45f;       // период мигания (два раза)

    private static readonly Color TextColor = new(0xFF / 255f, 0x52 / 255f, 0x98 / 255f);
    private static readonly Color White = new(1f, 1f, 1f, 1f);
    private static readonly Color Black = new(0f, 0f, 0f, 1f);

    private readonly Font _font;

    private string _text = string.Empty;
    private float _timeLeft;
    private float _elapsedTotal;
    private float _flickerTimer;
    private float _wobbleTime;

    // режим смерти: чёрный текст, чисто белые прямоугольники, зона НАД глазом
    private bool _deathMode;

    // блок: (нормированное смещение, размер, задержка появления, альфа)
    private readonly List<(Vector2 offset, Vector2 size, float delay, float alpha, bool whitens)> _blocks = new();

    // фазы покачивания букв
    private readonly List<(float anglePhase, float driftPhase, float maxAngle)> _charWobble = new();

    private readonly Random _rand = new();

    public bool Active => _timeLeft > 0f;

    public CelestialSubtitleRenderer(IResourceCache resourceCache)
    {
        _font = resourceCache.GetFont(FontPath, FontSize);
    }

    public void ShowDeath(string text, float duration)
    {
        _deathMode = true; // ВАЖНО: после Show, чтобы не сбросился
        ShowInternal(text, duration);
    }

    public void Show(string text, float duration)
    {
        _deathMode = false;
        ShowInternal(text, duration);
    }

    private void ShowInternal(string text, float duration)
    {
        _text = text;
        _timeLeft = duration;
        _elapsedTotal = 0f;
        _flickerTimer = 0f;
        _wobbleTime = 0f;
        _charWobble.Clear();
        foreach (var _ in text)
        {
            _charWobble.Add((
                (float) (_rand.NextDouble() * Math.PI * 2),
                (float) (_rand.NextDouble() * Math.PI * 2),
                5f + (float) _rand.NextDouble() * 10f));
        }
    }

    public void FrameUpdate(float frameTime)
    {
        if (_timeLeft <= 0f)
            return;

        _timeLeft -= frameTime;
        _elapsedTotal += frameTime;
        _wobbleTime += frameTime;

        // стробоскоп: блоки меняются не каждый кадр
        _flickerTimer -= frameTime;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = FlickerInterval * (0.7f + (float) _rand.NextDouble() * 0.6f);
            RegenerateBlocks();
        }
    }

    private sealed record Line(string text, float width, int index);

    private List<Line> SplitLines(string text, float scale, float maxWidth)
    {
        var width = MeasureWidth(text, scale);
        if (width <= maxWidth || !text.Contains(' '))
            return new List<Line> { new(text, width, 0) };

        // ищем пробел, делящий фразу на две строки как можно ровнее по ширине
        var half = width / 2f;
        var acc = 0f;
        var bestDiff = float.MaxValue;
        var bestIndex = -1;

        var runes = text.EnumerateRunes().ToList();
        for (var i = 0; i < runes.Count; i++)
        {
            if (runes[i].Value == ' ')
            {
                var diff = MathF.Abs(acc - half);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }

            if (_font.TryGetCharMetrics(runes[i], scale, out var m))
                acc += m.Advance * scale;
            else
                acc += FontSize * 0.5f;
        }

        if (bestIndex < 0)
            return new List<Line> { new(text, width, 0) };

        var line1 = string.Concat(runes.Take(bestIndex).Select(r => r.ToString()));
        var line2 = string.Concat(runes.Skip(bestIndex + 1).Select(r => r.ToString()));
        var w1 = MeasureWidth(line1, scale);
        var w2 = MeasureWidth(line2, scale);
        return new List<Line> { new(line1, w1, 0), new(line2, w2, 1) };
    }

    private float MeasureWidth(string text, float scale)
    {
        var w = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, scale, out var m))
                w += m.Advance * scale;
            else
                w += FontSize * 0.5f;
        }
        return w;
    }

    private void RegenerateBlocks()
    {
        _blocks.Clear();

        // пока текст только появился — блоки стартуют с задержкой до полусекунды
        var delayMax = MathF.Max(0f, AppearStagger - _elapsedTotal);

        var count = _rand.Next(7, 12);
        for (var i = 0; i < count; i++)
        {
            var w = _rand.Next(80, 321);
            var h = _rand.Next(26, 101);
            var alpha = 0.15f + (float) _rand.NextDouble() * 0.8f;
            var delay = (float) _rand.NextDouble() * delayMax;
            var whitens = _rand.NextDouble() < 0.45;

            // кучнее: большинство блоков жмётся к тексту, часть — по зоне
            int ox, oy;
            if (_rand.NextDouble() < 0.7)
            {
                ox = _rand.Next(-45, 46);
                oy = _rand.Next(-45, 46);
            }
            else
            {
                ox = _rand.Next(-100, 101);
                oy = _rand.Next(-100, 101);
            }

            _blocks.Add((new Vector2(ox, oy), new Vector2(w, h), delay, alpha, whitens));
        }
    }

    /// <summary>
    /// Рисует субтитр в экранных координатах. <paramref name="bounds"/> - полный экран
    /// (оверлей вьюпорта либо UI-корень).
    /// </summary>
    public void Draw(DrawingHandleScreen handle, UIBox2i bounds)
    {
        if (_timeLeft <= 0f || string.IsNullOrEmpty(_text))
            return;

        var width = bounds.Width;
        var height = bounds.Height;

        // центр вьюпорта с учётом его смещения на экране
        var center = new Vector2(bounds.Left + width / 2f, bounds.Top + height / 2f);
        var zoneCenterY = center.Y + 170f;

        // фаза финала (в смерти - просто затухание)
        var ending = !_deathMode && _timeLeft <= EndDuration;
        var endPhase = ending ? 1f - _timeLeft / EndDuration : 0f;
        var fadeOut = ending ? Math.Clamp(1f - (endPhase - 0.5f) / 0.5f, 0f, 1f) : 1f;

        var scale = 1f;

        // разбиваем слишком длинную фразу на две строки
        var lines = SplitLines(_text, scale, MathF.Min(width * 0.82f, 1150f));

        var ascent = _font.GetAscent(scale);
        var lineStep = FontSize * 1.15f;
        var totalH = lineStep * lines.Count;

        var halfW = 0f;
        var halfH = totalH / 2f + 16f;
        foreach (var line in lines)
            halfW = MathF.Max(halfW, line.width / 2f + 22f);

        // цвет текста: обычный розовый; в смерти - чёрный
        var textColor = _deathMode ? Black : TextColor;
        if (ending)
        {
            var blinkBlack = (_timeLeft % BlinkPeriod) < BlinkPeriod / 2f;
            textColor = _deathMode ? Black : (blinkBlack ? Black : White);
        }

        // блоки фона
        foreach (var (offset, size, delay, alpha, whitens) in _blocks)
        {
            if (_elapsedTotal < delay)
                continue;

            var blockAlpha = alpha;
            Color color;
            if (_deathMode)
            {
                // ЧИСТО белые прямоугольники
                color = new Color(1f, 1f, 1f, blockAlpha);
            }
            else if (ending && whitens)
            {
                // только часть блоков белеет
                var whiteMix = Math.Clamp(endPhase / 0.4f, 0f, 1f);
                color = new Color(whiteMix, whiteMix, whiteMix, blockAlpha * fadeOut);
            }
            else if (ending)
            {
                // остальные остаются чёрными и просто тают
                color = new Color(0f, 0f, 0f, blockAlpha * fadeOut);
            }
            else
            {
                color = new Color(0f, 0f, 0f, blockAlpha);
            }

            // только редкие блоки чуть-чуть выходят за границу зоны
            var escaper = Math.Abs(offset.X) > 90 || Math.Abs(offset.Y) > 90;
            var drift = escaper ? 1f + 0.07f * MathF.Sin(_wobbleTime * 0.6f) : 1f;
            var pos = new Vector2(
                center.X + (offset.X / 100f) * halfW * drift - size.X / 2f,
                zoneCenterY + (offset.Y / 100f) * halfH * drift - size.Y / 2f);

            handle.DrawRect(new UIBox2(pos, pos + size), color);
        }

        // буквы ПОВЕРХ фона: лёгкое медленное вращение (5-15°) и дрейф влево-вправо
        var charIndex = 0;
        var firstBaseline = zoneCenterY - totalH / 2f + ascent;
        foreach (var line in lines)
        {
            var baselineY = firstBaseline + line.index * lineStep;
            var x = center.X - line.width / 2f;

            foreach (var rune in line.text.EnumerateRunes())
            {
                var advance = FontSize * 0.5f;
                if (_font.TryGetCharMetrics(rune, scale, out var m))
                    advance = m.Advance * scale;

                var (anglePhase, driftPhase, maxAngle) = _charWobble.Count > charIndex
                    ? _charWobble[charIndex]
                    : (0f, 0f, 8f);

                var angle = maxAngle * MathF.Sin(_wobbleTime * 1.3f + anglePhase);
                var drift = 6f * MathF.Sin(_wobbleTime * 0.9f + driftPhase);

                var charCenter = new Vector2(x + advance / 2f + drift, baselineY);
                handle.SetTransform(charCenter, Angle.FromDegrees(angle));

                if (_font.TryGetCharMetrics(rune, scale, out _))
                    _font.DrawChar(handle, rune, new Vector2(-advance / 2f, 0f), scale, textColor);
                else
                    handle.DrawString(_font, new Vector2(-advance / 2f, 0f), rune.ToString(), scale, textColor);

                handle.SetTransform(Vector2.Zero, Angle.Zero);

                x += advance;
                charIndex++;
            }
        }
    }
}

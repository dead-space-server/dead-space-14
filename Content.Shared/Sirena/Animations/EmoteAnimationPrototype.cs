// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//DS-14 start
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.Sirena.Animations;

public static class EmoteAnimation
{
    public const float ShiftLimit = 2f;
    public const float TiltLimit = 720f;
    public const float SizeMin = 0.2f;
    public const float SizeMax = 3f;
    public const float FadeMin = 0.15f;
    public const float FadeMax = 1f;
    public const float ReturnGap = 0.15f;

    public static bool TryResolve(IReadOnlyList<EmoteAnimationStep>? steps, out EmoteAnimationPlan plan, out string error)
    {
        plan = new EmoteAnimationPlan();
        error = "";

        if (steps == null || steps.Count == 0)
        {
            error = "нет шагов";
            return false;
        }

        var shift = Vector2.Zero;
        var tilt = 0f;
        var face = 0f;
        var size = Vector2.One;
        var hasColor = false;
        var color = default(Color);
        var hasFade = false;
        var fade = 1f;
        string? frame = null;
        object layer = 0;
        var layerChosen = false;
        var previousAt = float.NegativeInfinity;

        foreach (var step in steps)
        {
            if (step == null)
            {
                error = "пустой шаг";
                return false;
            }

            if (step.At < -0.0001f)
            {
                error = "время меньше нуля";
                return false;
            }

            if (step.At <= previousAt)
            {
                error = "время должно расти";
                return false;
            }

            previousAt = step.At;

            if (step.Shift != null)
            {
                var value = step.Shift.Value;
                if (MathF.Abs(value.X) > ShiftLimit || MathF.Abs(value.Y) > ShiftLimit)
                {
                    error = "сдвиг дальше 2";
                    return false;
                }

                shift = value;
                plan.Shift = true;
            }

            if (step.Tilt != null)
            {
                if (MathF.Abs(step.Tilt.Value) > TiltLimit)
                {
                    error = "наклон дальше 720";
                    return false;
                }

                tilt = step.Tilt.Value;
                plan.Tilt = true;
            }

            if (step.Face != null)
            {
                if (!IsMultiple(step.Face.Value, 90f))
                {
                    error = "поворот не кратен 90";
                    return false;
                }

                face = step.Face.Value;
                plan.Face = true;
            }

            if (step.Size != null)
            {
                var value = step.Size.Value;
                if (value.X < SizeMin || value.X > SizeMax || value.Y < SizeMin || value.Y > SizeMax)
                {
                    error = "размер вне 0.2 и 3";
                    return false;
                }

                size = value;
                plan.Size = true;
            }

            if (step.Fade != null)
            {
                if (step.Fade.Value < FadeMin || step.Fade.Value > FadeMax)
                {
                    error = "прозрачность вне 0.15 и 1";
                    return false;
                }

                fade = step.Fade.Value;
                hasFade = true;
                plan.Tint = true;
            }

            if (!string.IsNullOrEmpty(step.Color))
            {
                if (!TryColor(step.Color, out color))
                {
                    error = "непонятный цвет";
                    return false;
                }

                hasColor = true;
                plan.Tint = true;
            }
            else if (step.Color != null)
            {
                error = "непонятный цвет";
                return false;
            }

            if (step.Frame != null)
            {
                if (string.IsNullOrWhiteSpace(step.Frame))
                {
                    error = "пустой кадр";
                    return false;
                }

                frame = step.Frame;
                plan.Frame = true;
            }

            if (!string.IsNullOrWhiteSpace(step.Layer))
            {
                if (!TryLayer(step.Layer, out var key))
                {
                    error = "слой меньше нуля";
                    return false;
                }

                if (layerChosen && !LayerEquals(layer, key))
                {
                    error = "слой меняется посреди анимации";
                    return false;
                }

                layer = key;
                layerChosen = true;
            }

            plan.Poses.Add(new EmoteAnimationPose
            {
                At = step.At,
                Shift = shift,
                Tilt = tilt,
                Face = face,
                Size = size,
                HasColor = hasColor,
                Color = color,
                HasFade = hasFade,
                Fade = fade,
                Frame = frame,
            });
        }

        if (plan.Face && !IsMultiple(plan.Poses[^1].Face, 360f))
        {
            error = "поворот в конце не кратен 360";
            return false;
        }

        if (plan.Poses[0].At > 0.0001f)
        {
            plan.Poses.Insert(0, new EmoteAnimationPose
            {
                At = 0f,
                Size = Vector2.One,
                Fade = 1f,
            });
        }

        var last = plan.Poses[^1];
        var shiftHome = !plan.Shift || Near(last.Shift, Vector2.Zero);
        var tiltHome = !plan.Tilt || IsMultiple(last.Tilt, 360f);
        var sizeHome = !plan.Size || Near(last.Size, Vector2.One);
        var fadeHome = !last.HasFade || MathF.Abs(last.Fade - 1f) <= 0.001f;

        if (!shiftHome || !tiltHome || !sizeHome || !fadeHome || plan.Tint)
        {
            var fix = last;
            fix.At = last.At + ReturnGap;
            if (!shiftHome)
                fix.Shift = Vector2.Zero;
            if (!tiltHome)
                fix.Tilt = NearestFullTurn(last.Tilt);
            if (!sizeHome)
                fix.Size = Vector2.One;
            if (!fadeHome)
            {
                fix.HasFade = true;
                fix.Fade = 1f;
            }

            fix.RestoreTint = plan.Tint;
            plan.Poses.Add(fix);
        }

        if (plan.Poses[^1].At <= 0.0001f)
        {
            error = "нулевая длина";
            return false;
        }

        plan.Layer = layer;
        return true;
    }

    public static bool IsMultiple(float value, float step)
    {
        var count = value / step;
        return MathF.Abs(count - MathF.Round(count)) <= 0.001f;
    }

    public static float NearestFullTurn(float degrees)
    {
        return MathF.Round(degrees / 360f, MidpointRounding.AwayFromZero) * 360f;
    }

    private static bool Near(Vector2 value, Vector2 target)
    {
        return MathF.Abs(value.X - target.X) <= 0.001f && MathF.Abs(value.Y - target.Y) <= 0.001f;
    }

    private static bool TryColor(string text, out Color color)
    {
        var hex = Color.TryFromHex(text);
        if (hex != null)
        {
            color = hex.Value;
            return true;
        }

        return Color.TryFromName(text, out color);
    }

    private static bool TryLayer(string text, out object key)
    {
        if (int.TryParse(text, out var index))
        {
            key = index;
            return index >= 0;
        }

        key = text;
        return true;
    }

    private static bool LayerEquals(object left, object right)
    {
        if (left is int leftIndex && right is int rightIndex)
            return leftIndex == rightIndex;

        return left is string leftName && right is string rightName && leftName == rightName;
    }
}

[DataDefinition]
public sealed partial class EmoteAnimationStep
{
    [DataField(required: true)]
    public float At;

    [DataField]
    public Vector2? Shift;

    [DataField]
    public float? Tilt;

    [DataField]
    public float? Face;

    [DataField]
    public Vector2? Size;

    [DataField]
    public string? Color;

    [DataField]
    public float? Fade;

    [DataField]
    public string? Frame;

    [DataField]
    public string? Layer;
}

public sealed class EmoteAnimationPlan
{
    public List<EmoteAnimationPose> Poses = new();
    public bool Shift;
    public bool Tilt;
    public bool Face;
    public bool Size;
    public bool Tint;
    public bool Frame;
    public object Layer = 0;
}

public struct EmoteAnimationPose
{
    public float At;
    public Vector2 Shift;
    public float Tilt;
    public float Face;
    public Vector2 Size;
    public bool HasColor;
    public Color Color;
    public bool HasFade;
    public float Fade;
    public string? Frame;
    public bool RestoreTint;
}
//DS-14 end

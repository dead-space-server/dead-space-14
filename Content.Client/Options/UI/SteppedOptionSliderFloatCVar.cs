using Robust.Shared.Configuration;

namespace Content.Client.Options.UI;

// Это код МК
public sealed class SteppedOptionSliderFloatCVar : BaseOptionCVar<float>
{
    public float Scale { get; }
    public float Step { get; set; }

    private readonly OptionSlider _slider;
    private readonly Func<SteppedOptionSliderFloatCVar, float, string> _format;

    protected override float Value
    {
        get => ApplyStep(_slider.Slider.Value) * Scale;
        set
        {
            _slider.Slider.Value = ApplyStep(value / Scale);
            UpdateLabelValue();
        }
    }

    public SteppedOptionSliderFloatCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<float> cVar,
        OptionSlider slider,
        float minValue,
        float maxValue,
        float scale,
        float step,
        Func<SteppedOptionSliderFloatCVar, float, string> format) : base(controller, cfg, cVar)
    {
        Scale = scale;
        Step = step;
        _slider = slider;
        _format = format;

        slider.Slider.MinValue = minValue;
        slider.Slider.MaxValue = maxValue;

        slider.Slider.OnValueChanged += args =>
        {
            var steppedValue = ApplyStep(args.Value);
            if (Math.Abs(steppedValue - args.Value) > 0.001f)
            {
                slider.Slider.Value = steppedValue;
                return;
            }

            ValueChanged();
            UpdateLabelValue();
        };
    }

    private float ApplyStep(float value)
    {
        if (Step <= 0) return value;
        return MathF.Round(value / Step) * Step;
    }

    private void UpdateLabelValue()
    {
        _slider.ValueLabel.Text = _format(this, _slider.Slider.Value);
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CentComm;

[Serializable, NetSerializable]
public enum CentCommTemperature
{
    Space,
    Cold,
    Normal,
    Hot,
    Custom,
}

public static class CentCommTransferSettings
{
    public const float ColdCelsius = -20f;
    public const float NormalCelsius = 20f;
    public const float HotCelsius = 60f;
    public const float MinCelsius = Atmospherics.TCMB - Atmospherics.T0C;
    public const float MaxCelsius = Atmospherics.Tmax - Atmospherics.T0C;

    public static bool TryGetTemperature(CentCommTemperature preset, float celsius, out float? kelvin)
    {
        kelvin = null;
        if (preset == CentCommTemperature.Space)
            return true;

        var value = preset switch
        {
            CentCommTemperature.Cold => ColdCelsius,
            CentCommTemperature.Normal => NormalCelsius,
            CentCommTemperature.Hot => HotCelsius,
            CentCommTemperature.Custom => celsius,
            _ => float.NaN,
        };

        if (!float.IsFinite(value) || value < MinCelsius || value > MaxCelsius)
            return false;

        kelvin = value + Atmospherics.T0C;
        return true;
    }
}

[Serializable, NetSerializable]
public sealed class CentCommTransferState(string[] parallaxes, string[] weather, bool canStart, string status) : EuiStateBase
{
    public readonly string[] Parallaxes = parallaxes;
    public readonly string[] Weather = weather;
    public readonly bool CanStart = canStart;
    public readonly string Status = status;
}

[Serializable, NetSerializable]
public sealed class CentCommTransferRequest(string parallax, CentCommTemperature temperature, float celsius, string? weather) : EuiMessageBase
{
    public readonly string Parallax = parallax;
    public readonly CentCommTemperature Temperature = temperature;
    public readonly float Celsius = celsius;
    public readonly string? Weather = weather;
}

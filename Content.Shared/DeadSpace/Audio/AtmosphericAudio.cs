// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;

namespace Content.Shared.DeadSpace.Audio;

public static class AtmosphericAudio
{
    public const float VacuumOcclusion = 6f;

    // Zero means unknown/air-blocked; 1 is vacuum and 255 is one atmosphere or higher.
    public static byte EncodePressure(float pressure)
    {
        if (!float.IsFinite(pressure))
            return 0;

        return (byte) (1 + MathF.Round(Math.Clamp(pressure / Atmospherics.OneAtmosphere, 0f, 1f) * 254f));
    }

    public static float GetOcclusion(byte pressure)
    {
        return pressure == 0 ? 0f : VacuumOcclusion * (1f - (pressure - 1) / 254f);
    }
}

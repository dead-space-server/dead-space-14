// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Audio;

public static class SpatialAudio
{
    public const float WhisperRange = 4f;

    public static float GetDistanceGain(float distance, float range, float referenceDistance = 1f)
    {
        if (range <= referenceDistance)
            return distance < range ? 1f : 0f;
        var remaining = 1f - Math.Clamp((distance - referenceDistance) / (range - referenceDistance), 0f, 1f);
        return remaining * remaining;
    }
}

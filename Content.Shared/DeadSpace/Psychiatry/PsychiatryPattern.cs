// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;

namespace Content.Shared.DeadSpace.Psychiatry;

public enum PsychiatryRemapPool : byte
{
    Animal,
    Monster,
    Item,
}

public static class PsychiatryPattern
{
    public static bool IsMeatWall(Vector2i idx, int seed, SchizophreniaStage stage)
    {
        if (stage < SchizophreniaStage.Acute)
            return false;
        var n = Fbm(idx.X, idx.Y, seed + 17, 0.09f);
        var intensity = 1f;
        var threshold = MathHelper.Lerp(0.58f, 0.36f, intensity);
        return n > threshold;
    }

    public static bool ShouldRemapMob(int entityHash, int seed, SchizophreniaStage stage, float latent = 0.45f, float simple = 0.65f, float acute = 0.9f)
    {
        if (stage < SchizophreniaStage.Simple)
            return false;

        var chance = stage switch
        {
            SchizophreniaStage.Simple => simple,
            SchizophreniaStage.Acute => acute,
            _ => 0f,
        };
        var v = (HashCode.Combine(entityHash, seed) & 255) / 255f;
        return v <= chance;
    }

    public static bool ShouldRemapItem(int entityHash, int seed, SchizophreniaStage stage, float latent = 0.35f, float simple = 0.45f, float acute = 0.55f)
    {
        if (stage < SchizophreniaStage.Acute)
            return false;
        var chance = stage switch
        {
            SchizophreniaStage.Latent => latent,
            SchizophreniaStage.Simple => simple,
            SchizophreniaStage.Acute => acute,
            _ => 0f,
        };
        var v = (HashCode.Combine(entityHash, seed, 23) & 255) / 255f;
        return v <= chance;
    }

    public static PsychiatryRemapPool PickPool(SchizophreniaStage stage, int entityHash, int seed, float monsterChance = 200f / 255f)
    {
        if (stage >= SchizophreniaStage.Acute)
        {
            return (HashCode.Combine(entityHash, seed, 9) & 255) / 255f < monsterChance
                ? PsychiatryRemapPool.Monster
                : PsychiatryRemapPool.Animal;
        }

        return PsychiatryRemapPool.Animal;
    }

    public static bool PreferCow(int entityHash, int seed) =>
        (HashCode.Combine(entityHash, seed, 3) & 1) == 0;

    private static float Fbm(int x, int y, int seed, float scale)
    {
        var n = 0f;
        var amp = 1f;
        var freq = scale;
        var norm = 0f;
        for (var o = 0; o < 4; o++)
        {
            n += ValueNoise(x * freq, y * freq, seed + o * 131) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }

        return n / norm;
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        var x0 = (int) MathF.Floor(x);
        var y0 = (int) MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        var a = Hash01(x0, y0, seed);
        var b = Hash01(x0 + 1, y0, seed);
        var c = Hash01(x0, y0 + 1, seed);
        var d = Hash01(x0 + 1, y0 + 1, seed);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float) 0xFFFFFF;
        }
    }
}

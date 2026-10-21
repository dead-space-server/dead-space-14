using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.Bosses;

[Serializable, NetSerializable]
public sealed class LavalandBossHudUpdateEvent : EntityEventArgs
{
    public int ArenaId;
    public string BossName = string.Empty;
    public float CurrentHealth;
    public float MaxHealth;
    public int Participants;

    public LavalandBossHudUpdateEvent()
    {
    }

    public LavalandBossHudUpdateEvent(int arenaId, string bossName, float currentHealth, float maxHealth, int participants)
    {
        ArenaId = arenaId;
        BossName = bossName;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        Participants = participants;
    }
}

[Serializable, NetSerializable]
public sealed class LavalandBossHudHideEvent : EntityEventArgs
{
    public int ArenaId;

    public LavalandBossHudHideEvent()
    {
    }

    public LavalandBossHudHideEvent(int arenaId)
    {
        ArenaId = arenaId;
    }
}

[Serializable, NetSerializable]
public sealed class LavalandBossMusicStartEvent : EntityEventArgs
{
    /// <summary>The fauna raid's music goes through the boss music too (its volume and switch); a boss fight wins over it.</summary>
    public const int RaidMusicId = int.MinValue;

    public int ArenaId;
    public ResolvedSoundSpecifier Specifier = default!;
    public AudioParams? AudioParams;

    public LavalandBossMusicStartEvent()
    {
    }

    public LavalandBossMusicStartEvent(int arenaId, ResolvedSoundSpecifier specifier, AudioParams? audioParams)
    {
        ArenaId = arenaId;
        Specifier = specifier;
        AudioParams = audioParams;
    }
}

[Serializable, NetSerializable]
public sealed class LavalandBossMusicStopEvent : EntityEventArgs
{
    public int ArenaId;

    public LavalandBossMusicStopEvent()
    {
    }

    public LavalandBossMusicStopEvent(int arenaId)
    {
        ArenaId = arenaId;
    }
}

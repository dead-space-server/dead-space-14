// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public enum LimbHearingState : byte
{
    Healthy,
    Damaged,
    Deaf,
}

public static class LimbHearing
{
    public const float Muffle = 15f;
    public const float Lost = 40f;
    public const float Heal = 0.05f;
    public const float ConcussionLight = 6f;
    public const float ConcussionHeavy = 14f;

    public static LimbHearingState State(LimbBodyComponent limbs)
    {
        if (!limbs.HasEars)
            return LimbHearingState.Healthy;

        if (limbs.EarsGone || limbs.Ears >= Lost)
            return LimbHearingState.Deaf;

        return limbs.Ears >= Muffle ? LimbHearingState.Damaged : LimbHearingState.Healthy;
    }

    public static LimbHearingState State(IEntityManager entities, EntityUid uid)
    {
        return entities.TryGetComponent<LimbBodyComponent>(uid, out var limbs) ? State(limbs) : LimbHearingState.Healthy;
    }
}

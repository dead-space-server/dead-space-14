namespace Content.Shared.Humanoid
{
    // You need to update profile, profile editor, maybe voices and names if you want to expand this further.
    public enum Sex : byte
    {
        Male,
        Female,
        Unsexed,
    }
<<<<<<< ours

    /// <summary>
    ///     Raised when entity has changed their sex.
    ///     This doesn't handle gender changes.
    /// </summary>
    public record struct SexChangedEvent(Sex OldSex, Sex NewSex);
||||||| base

    /// <summary>
    ///     Raised when entity has changed their sex.
    ///     This doesn't handle gender changes.
    /// </summary>
    [ByRefEvent]
    public record struct SexChangedEvent(Sex OldSex, Sex NewSex);
=======
>>>>>>> theirs
}

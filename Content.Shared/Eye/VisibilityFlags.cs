using Robust.Shared.Serialization;

namespace Content.Shared.Eye
{
    [Flags]
    [FlagsFor(typeof(VisibilityMaskLayer))]
    public enum VisibilityFlags : int
    {
        None = 0,
        Normal = 1 << 0,
        Ghost = 1 << 1, // Observers and revenants.
        Subfloor = 1 << 2, // Pipes, disposal chutes, cables etc. while hidden under tiles. Can be revealed with a t-ray.
        Admin = 1 << 3, // Reserved for admins in stealth mode and admin tools.
        Astral = 1 << 4, // DS14  Astral projection, can see other astral projections.
        //DS-14 start
        SubfloorCable = 1 << 5,
        SubfloorAtmos = 1 << 6,
        SubfloorDisposal = 1 << 7,
        //DS-14 end
    }

    //DS-14 start
    public static class SubfloorVisibility
    {
        public const VisibilityFlags All =
            VisibilityFlags.Subfloor
            | VisibilityFlags.SubfloorCable
            | VisibilityFlags.SubfloorAtmos
            | VisibilityFlags.SubfloorDisposal;
    }
    //DS-14 end
}

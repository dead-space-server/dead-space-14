using Content.Shared.Eye; //DS-14
using Robust.Shared.GameStates;

namespace Content.Shared.SubFloor
{
    /// <summary>
    /// Simple component that automatically hides the sibling
    /// <see cref="SpriteComponent" /> when the tile it's on is not a sub floor
    /// (plating).
    /// </summary>
    /// <seealso cref="P:Content.Shared.Maps.ContentTileDefinition.IsSubFloor" />
    [NetworkedComponent]
    [RegisterComponent]
    [Access(typeof(SharedSubFloorHideSystem))]
    public sealed partial class SubFloorHideComponent : Component
    {
        /// <summary>
        ///     Whether the entity's current position has a "Floor-type" tile above its current position.
        /// </summary>
        [ViewVariables]
        public bool IsUnderCover { get; set; } = false;

        /// <summary>
        ///     Whether interactions with this entity should be blocked while it is under floor tiles.
        /// </summary>
        /// <remarks>
        ///     Useful for entities like vents, which are only partially hidden. Anchor attempts will still be blocked.
        /// </remarks>
        [DataField]
        public bool BlockInteractions { get; set; } = true;

        /// <summary>
        /// Whether this entity's ambience should be disabled when underneath the floor.
        /// </summary>
        /// <remarks>
        /// Useful for cables and piping, gives maint it's distinct noise.
        /// </remarks>
        [DataField]
        public bool BlockAmbience { get; set; } = true;

        /// <summary>
        ///     Sprite layer keys for the layers that are always visible, even if the entity is below a floor tile. E.g.,
        ///     the vent part of a vent is always visible, even though the piping is hidden.
        /// </summary>
        [DataField]
        public HashSet<Enum> VisibleLayers = new();

        /// <summary>
        /// This is used for storing the original draw depth of a t-ray revealed entity.
        /// e.g. when a t-ray revealed cable is drawn above a carpet.
        /// </summary>
        [DataField]
        public int? OriginalDrawDepth;

        //DS-14 start
        [DataField]
        public SubfloorViewLayer ViewLayer = SubfloorViewLayer.None;
        //DS-14 end
    }

    //DS-14 start
    public enum SubfloorViewLayer : byte
    {
        None,
        Cable,
        Atmos,
        Disposal,
    }

    public static class SubfloorViewLayers
    {
        public static VisibilityFlags ToFlag(SubfloorViewLayer layer)
        {
            switch (layer)
            {
                case SubfloorViewLayer.Cable:
                    return VisibilityFlags.SubfloorCable;
                case SubfloorViewLayer.Atmos:
                    return VisibilityFlags.SubfloorAtmos;
                case SubfloorViewLayer.Disposal:
                    return VisibilityFlags.SubfloorDisposal;
                default:
                    return VisibilityFlags.Subfloor;
            }
        }
    }
    //DS-14 end
}

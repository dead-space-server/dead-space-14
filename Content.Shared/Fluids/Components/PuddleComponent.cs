using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Fluids.Components
{
    /// <summary>
    /// Puddle on a floor
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedPuddleSystem))] //DS14
    public sealed partial class PuddleComponent : Component
    {
        [DataField, AutoNetworkedField] //DS14
        public SoundSpecifier SpillSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

        [DataField, AutoNetworkedField] //DS14
        public FixedPoint2 OverflowVolume = FixedPoint2.New(50);

        // DS14-start: reduce physics load from decorative puddles.
        [DataField, AutoNetworkedField]
        public FixedPoint2 VisualOverflowVolume = FixedPoint2.New(50);

        [DataField, AutoNetworkedField]
        public FixedPoint2 DecorativePuddleOverflowVolume = FixedPoint2.New(120);

        [DataField, AutoNetworkedField]
        public FixedPoint2 DecorativePuddleMaxVolume = FixedPoint2.New(120);
        // DS14-end

        [DataField("solution")] public string SolutionName = "puddle";

        /// <summary>
        /// Default minimum speed someone must be moving to slip for all reagents.
        /// </summary>
        [DataField, AutoNetworkedField] //DS14
        public float DefaultSlippery = 5.5f;

        // Shitcode I wrote to make this worth with new IconSmooth.
        // Both need generic visualizers support.
        [ViewVariables]
        public string PuddleKey = "direction";

        // Shitcode I wrote to make this worth with new IconSmooth.
        // Both need generic visualizers support.
        [DataField, ViewVariables]
        public string PuddleState = "splat";

        [ViewVariables]
        public Entity<SolutionComponent>? Solution;

        // Backmen-footsteps-start
        [DataField, AutoNetworkedField]
        public bool ViscosityAffectsMovement = true;
        // Backmen-footsteps-end
    }
}

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.NormalMap;

/// <summary>
///     Tracks which sprite layers currently have a normal map shader applied to them.
/// </summary>
[RegisterComponent]
public sealed partial class NormalMapVisualsComponent : Component
{
    /// <summary>
    ///     Snapshot of the sprite layer setup used to detect when normal maps have to be rebuilt.
    /// </summary>
    [ViewVariables] public string? Signature;

    public readonly List<AppliedEntry> Applied = new();

    public sealed class AppliedEntry
    {
        /// <summary>
        ///     The visual layer the normal map shader is applied to.
        /// </summary>
        public Layer TargetLayer = default!;

        /// <summary>
        ///     Shader instance owned by us, with the <c>normalMap</c>/<c>normalUV</c> parameters.
        /// </summary>
        public ShaderInstance? Shader;

        /// <summary>
        ///     Shader state that was on the layer before we touched it.
        /// </summary>
        public ShaderInstance? OriginalShader;
        public ProtoId<ShaderPrototype>? OriginalShaderPrototype;

        public RSI.State? State;
        public Texture? NormalSheet;
        public Vector2i FrameSize;

        /// <summary>
        ///     Whether the normal image is a sheet that has to be cropped per direction/frame.
        /// </summary>
        public bool IsSheet;

        public string TargetRsi = string.Empty;
        public string TargetState = string.Empty;

        /// <summary>
        ///     Flat index (direction * frame count + frame) of the normal sheet cell currently bound.
        /// </summary>
        public int LastCell = -2;
    }
}

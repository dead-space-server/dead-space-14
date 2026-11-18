using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaperComponent : Component
{
    [DataField("content"), AutoNetworkedField]
    public string Content { get; set; } = "";

    [DataField, AutoNetworkedField]
    public List<string> Signatures = new List<string>(); // DS14-signatures

    [DataField("contentSize")]
    public int ContentSize { get; set; } = 10000;

    [DataField("stampedBy"), AutoNetworkedField]
    public List<StampDisplayInfo> StampedBy { get; set; } = new();

    /// <summary>
    ///     Stamp to be displayed on the paper, state from bureaucracy.rsi
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string? StampState { get; set; }

    [DataField, AutoNetworkedField]
    public bool EditingDisabled;

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly List<string> Signatures; // DS14-signatures
        public readonly PaperAction Mode;
        public readonly bool EditingDisabled; // DS14

        public PaperBoundUserInterfaceState(string text, List<StampDisplayInfo> stampedBy, List<string> signatures, PaperAction mode = PaperAction.Read, bool editingDisabled = false) // DS14-signatures
        {
            Text = text;
            StampedBy = stampedBy;
            Signatures = signatures; // DS14-signatures
            Mode = mode;
            EditingDisabled = editingDisabled; // DS14
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputTextMessage : BoundUserInterfaceMessage
    {
        public readonly string Text;

        public PaperInputTextMessage(string text)
        {
            Text = text;
        }
    }

    // DS14-start
    [Serializable, NetSerializable]
    public sealed class PaperCheckMessage : BoundUserInterfaceMessage
    {
        public readonly int Index;
        public readonly PaperCheckState State;

        public PaperCheckMessage(int index, PaperCheckState state)
        {
            Index = index;
            State = state;
        }
    }

    [Serializable, NetSerializable]
    public enum PaperCheckState : byte
    {
        Blank,
        Check,
        Cross
    }
    // DS14-end

    [Serializable, NetSerializable]
    public enum PaperUiKey
    {
        Key,
        Write // DS14
    }

    [Serializable, NetSerializable]
    public enum PaperAction
    {
        Read,
        Write,
    }

    [Serializable, NetSerializable]
    public enum PaperVisuals : byte
    {
        Status,
        Signature, // DS14-signatures
        Stamp
    }

    [Serializable, NetSerializable]
    public enum PaperStatus : byte
    {
        Blank,
        Written
    }
}

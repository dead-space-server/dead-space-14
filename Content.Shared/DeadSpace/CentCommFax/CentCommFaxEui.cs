// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Eui;
using Content.Shared.Paper;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CentCommFax;

[Serializable, NetSerializable]
public sealed class CentCommFaxEuiState : EuiStateBase
{
    public List<CentCommFaxEntry> Inbox = new();
    public List<CentCommFaxTarget> Faxes = new();
}

[Serializable, NetSerializable]
public sealed class CentCommFaxEntry
{
    public int Id;
    public string Time = string.Empty;
    public string Sender = string.Empty;
    public string Station = string.Empty;
    public NetEntity? SenderFax;
    public string Title = string.Empty;
    public string Content = string.Empty;
    public List<StampDisplayInfo> Stamps = new();
    public List<string> Signatures = new();
    public int Replies;

    // What it takes to send this very paper back.
    public string PaperPrototype = string.Empty;
    public string? Label;
    public string? StampState;
}

/// <summary>One stamp put on a reply: the stamp prototype and the ink colour (null - the stamp's own).</summary>
[Serializable, NetSerializable]
public record struct CentCommFaxStamp(string Prototype, Color? Color);

[Serializable, NetSerializable]
public record struct CentCommFaxTarget(NetEntity Fax, string Name, string Station);

public static class CentCommFaxEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Reply : EuiMessageBase
    {
        public int ReplyTo = -1;
        public NetEntity Target;
        public string Title = string.Empty;
        public string From = string.Empty;
        public string Content = string.Empty;
        public List<CentCommFaxStamp> Stamps = new();
        public bool Locked;
        public string? PaperPrototype;

        /// <summary>
        /// Send back the received paper <see cref="ReplyTo"/> itself, with its text, stamps and signatures, plus the new
        /// stamps; Title and Content are ignored.
        /// </summary>
        public bool ReturnOriginal;
    }

    [Serializable, NetSerializable]
    public sealed class RequestTemplate : EuiMessageBase
    {
        public string Template = string.Empty;
        public NetEntity? Target;
    }

    [Serializable, NetSerializable]
    public sealed class TemplateLoaded : EuiMessageBase
    {
        public string Template = string.Empty;
        public string Title = string.Empty;
        public string Content = string.Empty;
        public string PaperPrototype = string.Empty;
    }

    [Serializable, NetSerializable]
    public sealed class Follow : EuiMessageBase
    {
        public NetEntity Target;
    }
}

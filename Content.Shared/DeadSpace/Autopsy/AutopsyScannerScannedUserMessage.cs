// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Autopsy;

[Serializable, NetSerializable]
public sealed class AutopsyScannerScannedUserMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public string? EntityName;
    public string? IdentityText;
    public List<OrganDamageDetail> Details = new();
    public List<string> Traces = new();

    public AutopsyScannerScannedUserMessage(NetEntity? targetEntity)
    {
        TargetEntity = targetEntity;
    }
}

[Serializable, NetSerializable]
public sealed class OrganDamageDetail
{
    public string Type = string.Empty;
    public string Amount = string.Empty;
    public string Source = string.Empty;
    public TimeSpan Time;
}

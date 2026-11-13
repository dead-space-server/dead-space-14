// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.DebugAnalyzer;

[Serializable, NetSerializable]
public enum DebugAnalyzerSeverity : byte
{
    Normal,
    Warning,
    Critical,
}

/// <summary>
/// One line of a report.
/// </summary>
[Serializable, NetSerializable]
public sealed class DebugAnalyzerRow
{
    /// <summary>
    /// A localization id. Anything that is not one is shown as it is.
    /// </summary>
    public readonly string Label;

    public readonly string Value;

    /// <summary>
    /// Fills a bar under the line, from 0 to 1. No bar when null.
    /// </summary>
    public readonly float? Fraction;

    public readonly DebugAnalyzerSeverity Severity;

    public DebugAnalyzerRow(string label, string value, float? fraction = null,
        DebugAnalyzerSeverity severity = DebugAnalyzerSeverity.Normal)
    {
        Label = label;
        Value = value;
        Fraction = fraction;
        Severity = severity;
    }
}

[Serializable, NetSerializable]
public sealed class DebugAnalyzerSection
{
    /// <summary>
    /// A localization id.
    /// </summary>
    public readonly string Title;

    public readonly List<DebugAnalyzerRow> Rows;

    public DebugAnalyzerSection(string title, List<DebugAnalyzerRow> rows)
    {
        Title = title;
        Rows = rows;
    }
}

/// <summary>
/// Everything the scanner knows about the being, built on the server every time it refreshes.
/// </summary>
[Serializable, NetSerializable]
public sealed class DebugAnalyzerScannedMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;
    public readonly string TargetName;
    public readonly List<DebugAnalyzerSection> Sections;

    public DebugAnalyzerScannedMessage(NetEntity target, string targetName, List<DebugAnalyzerSection> sections)
    {
        Target = target;
        TargetName = targetName;
        Sections = sections;
    }
}

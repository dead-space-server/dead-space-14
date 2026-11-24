// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PlanetSurvey;

[Serializable, NetSerializable]
public sealed class SurveyDebugMessage : EntityEventArgs
{
    public readonly List<SurveyDebugEntry> Entries;

    public SurveyDebugMessage(List<SurveyDebugEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public readonly record struct SurveyDebugEntry(NetEntity Entity, string Text, bool Alert);

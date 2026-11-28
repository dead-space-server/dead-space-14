// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PlanetSurvey;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurveyObjectivesComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public string Title = string.Empty;

    [AutoNetworkedField]
    public string Threat = string.Empty;

    [AutoNetworkedField]
    public List<SurveyObjectiveLine> Lines = new();
}

[Serializable, NetSerializable]
public sealed class SurveyObjectiveLine
{
    public string Text = string.Empty;
    public bool Done;

    public SurveyObjectiveLine()
    {
    }

    public SurveyObjectiveLine(string text, bool done)
    {
        Text = text;
        Done = done;
    }
}

[Serializable, NetSerializable]
public sealed partial class SurveyStudyDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class SurveySabotageDoAfterEvent : SimpleDoAfterEvent;

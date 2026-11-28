// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.PlanetSurvey;

public enum SurveyKind : byte
{
    Beasts,
    Raiders,
    Pirates,
    Factory,
    DeadElement,
}

public enum SurveyMotive : byte
{
    Retrieve,
    Windfall,
    Curse,
}

public enum SurveyRole : byte
{
    None,
    Hold,
    Ambush,
    Flank,
    Suppress,
    Rush,
    Errand,
}

public enum SurveyPhase : byte
{
    Quiet,
    Contact,
    Deal,
    Betrayal,
    Withdraw,
}

public static class SurveyKeys
{
    public const string Role = "SurveyRole";
    public const string Retreat = "SurveyRetreat";
    public const string HoldFire = "SurveyHoldFire";
    public const string Confident = "SurveyConfident";
    public const string Target = "SurveyTarget";
    public const string TargetCoords = "SurveyTargetCoords";
    public const string Post = "SurveyPost";
    public const string Flank = "SurveyFlank";
    public const string Cover = "SurveyCover";
    public const string Rush = "SurveyRush";
    public const string AimZone = "SurveyAimZone";
    public const string MeleeZone = "SurveyMeleeZone";
    public const string Bark = "SurveyBark";
    public const string Voice = "SurveyVoice";
    public const string Guard = "SurveyGuard";
    public const string Call = "SurveyCall";
    public const string Called = "SurveyCalled";
    public const string Threat = "SurveyThreat";
    public const string Flee = "SurveyFlee";
    public const string Den = "SurveyDen";
    public const string Errand = "SurveyErrand";
}

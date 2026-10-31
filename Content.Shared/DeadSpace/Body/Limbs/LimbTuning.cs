// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbTuning
{
    public static bool Enabled = true;
    public static float ExtremityHealth = LimbWoundRules.ExtremityHealth;
    public static float LimbHealth = LimbWoundRules.LimbHealth;
    public static float HeadHealth = LimbWoundRules.HeadHealth;
    public static float TorsoHealth = LimbWoundRules.TorsoHealth;
    public static float GroinHealth = LimbWoundRules.GroinHealth;
    public static float BurnFirst = LimbWoundRules.BurnFirst;
    public static float BurnSecond = LimbWoundRules.BurnSecond;
    public static float BurnThird = LimbWoundRules.RawSkin;
    public static float BurnFourth = LimbWoundRules.BurnFourth;
    public static float SeverHand = LimbWoundRules.HandFootSever;
    public static float SeverLimb = LimbWoundRules.LimbSever;
    public static float SeverHead = LimbWoundRules.HeadSever;
    public static float CrawlSpeed = LimbWoundRules.CrawlSpeed;
    public static float CrawlAcceleration = LimbWoundRules.CrawlAcceleration;
    public static float FireSpread = LimbWoundRules.FireSpreadAmount;
    public static float LimpSpeed = LimbWoundRules.LimpSpeed;
    public static float MissingLegSpeed = LimbWoundRules.MissingLegSpeed;
}

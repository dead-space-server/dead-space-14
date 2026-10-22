// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> LimbEnabled =
        CVarDef.Create("limb.enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbHealthExtremity =
        CVarDef.Create("limb.health_extremity", LimbWoundRules.ExtremityHealth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbHealthLimb =
        CVarDef.Create("limb.health_limb", LimbWoundRules.LimbHealth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbHealthHead =
        CVarDef.Create("limb.health_head", LimbWoundRules.HeadHealth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbHealthTorso =
        CVarDef.Create("limb.health_torso", LimbWoundRules.TorsoHealth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbHealthGroin =
        CVarDef.Create("limb.health_groin", LimbWoundRules.GroinHealth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbBurnFirst =
        CVarDef.Create("limb.burn_first", LimbWoundRules.BurnFirst, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbBurnSecond =
        CVarDef.Create("limb.burn_second", LimbWoundRules.BurnSecond, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbBurnThird =
        CVarDef.Create("limb.burn_third", LimbWoundRules.RawSkin, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbBurnFourth =
        CVarDef.Create("limb.burn_fourth", LimbWoundRules.BurnFourth, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbSeverHand =
        CVarDef.Create("limb.sever_hand", LimbWoundRules.HandFootSever, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbSeverLimb =
        CVarDef.Create("limb.sever_limb", LimbWoundRules.LimbSever, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbSeverHead =
        CVarDef.Create("limb.sever_head", LimbWoundRules.HeadSever, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbCrawlSpeed =
        CVarDef.Create("limb.crawl_speed", LimbWoundRules.CrawlSpeed, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbCrawlAcceleration =
        CVarDef.Create("limb.crawl_acceleration", LimbWoundRules.CrawlAcceleration, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbFireSpread =
        CVarDef.Create("limb.fire_spread", LimbWoundRules.FireSpreadAmount, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbLimpSpeed =
        CVarDef.Create("limb.limp_speed", LimbWoundRules.LimpSpeed, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> LimbMissingLegSpeed =
        CVarDef.Create("limb.missing_leg_speed", LimbWoundRules.MissingLegSpeed, CVar.SERVER | CVar.REPLICATED);
}

using Robust.Shared.Configuration;

namespace Content.Shared.DeadSpace.CCCCVars;

/// <summary>
///     DeadSpace modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class CCCCVars
{
    /*
	* GCF
	*/

    /// <summary>
    ///     Whether GCF being shown is enabled at all.
    /// </summary>

    public static readonly CVarDef<bool> GCFEnabled =
        CVarDef.Create("gcf_auto.enabled", false);

    /// <summary>
    ///     Notify for admin about GCF Clean.
    /// </summary>
    public static readonly CVarDef<bool> GCFNotify =
        CVarDef.Create("gcf_auto.notify", false);

    /// <summary>
    ///     The number of seconds between each GCF
    /// </summary>
    public static readonly CVarDef<float> GCFFrequency =
        CVarDef.Create("gcf_auto.frequency", 300f);

    /*
	* InfoLinks
	*/

    /// <summary>
    /// IPs address for reconnect.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksIPs =
        CVarDef.Create("infolinks.ips", string.Empty, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Multiplier for playtime.
    /// </summary>
    public static readonly CVarDef<float> PlayTimeMultiplier =
        CVarDef.Create("playtime.multiplier", 1f, CVar.SERVER | CVar.REPLICATED);

    /*
	* TTS
	*/

    public static readonly CVarDef<float> TTSVolumeRadio =
        CVarDef.Create("tts.volume_radio", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RadioTTSSoundsEnabled =
        CVarDef.Create("audio.radio_tts_sounds_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
	* Jukebox
	*/

    public static readonly CVarDef<float> JukeboxMusicVolume =
        CVarDef.Create("jukebox.volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> JukeboxAutoVolume =
        CVarDef.Create("jukebox.auto_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Alert Level
     */

    public static readonly CVarDef<float> AlertLevelVolume =
        CVarDef.Create("audio.alert_level_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Annocment
     */
    public static readonly CVarDef<float> AnnonceVolume =
        CVarDef.Create("audio.annonce_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Annocment
     */
    public static readonly CVarDef<float> AdminVolume =
        CVarDef.Create("audio.admin_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Item sounds
     */

    public static readonly CVarDef<float> ItemSoundsVolume =
        CVarDef.Create("audio.item_sounds_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Boss music
     */

    public static readonly CVarDef<bool> BossMusicEnabled =
        CVarDef.Create("audio.boss_music_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> BossMusicVolume =
        CVarDef.Create("audio.boss_music_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
    * Taipan
    */

    /// <summary>
    /// Should Taipan spawn or not.
    /// </summary>
    public static readonly CVarDef<bool> TaipanEnabled =
        CVarDef.Create("taipan.enabled", false, CVar.SERVERONLY);

    /*
    * Lavaland
    */

    /// <summary>
    /// Should stations auto-generate Lavaland on round start.
    /// </summary>
    public static readonly CVarDef<bool> LavalandAutoGenerate =
        CVarDef.Create("lavaland.auto_generate", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> AshWalkersEnabled =
        CVarDef.Create("lavaland.ash_walkers_enabled", true, CVar.SERVER | CVar.REPLICATED);

    /*
    * Prison
    */

    public static readonly CVarDef<bool> PrisonEnabled =
        CVarDef.Create("prison.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<int> PrisonMurderPenaltyMinutes =
        CVarDef.Create("prison.murder_penalty_minutes", 60, CVar.SERVERONLY);

    public static readonly CVarDef<float> PrisonSentenceTimeMultiplier =
        CVarDef.Create("prison.sentence_time_multiplier", 1.1f, CVar.SERVERONLY);

    public static readonly CVarDef<int> PrisonCrossFactionKillRewardMinutes =
        CVarDef.Create("prison.cross_faction_kill_reward_minutes", 2, CVar.SERVERONLY);

    public static readonly CVarDef<int> PrisonFactionSelectionSeconds =
        CVarDef.Create("prison.faction_selection_seconds", 20, CVar.SERVERONLY);

    /// <summary>
    /// Moves long-stuck dynamic physics bodies out of static hard overlaps.
    /// </summary>
    public static readonly CVarDef<bool> PhysicsSanityEnabled =
        CVarDef.Create("physics.sanity_enabled", true, CVar.SERVERONLY);

    /*
    * Lobby ui
    */

    /// <summary>
    /// Lobby default background. Can be Parallax or Image.
    /// </summary>
    public static readonly CVarDef<string> Background =
        CVarDef.Create("ui.background", "Image", CVar.CLIENTONLY | CVar.ARCHIVE);

    public const string InterfaceStyleDark = "Dark";
    public const string InterfaceStyleLight = "Light";
    public const string InterfaceStyleClassic = "Classic";

    /// <summary>
    /// Visual style used by ordinary game windows and menus. This is separate from the HUD resource theme.
    /// </summary>
    public static readonly CVarDef<string> InterfaceStyle =
        CVarDef.Create("ui.style_theme", InterfaceStyleDark, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
    * Player Count Mode
    */

    /// <summary>
    /// Whether to use total players or ready players for game mode selection.
    /// </summary>
    public static readonly CVarDef<bool> GameModesUseTotalPlayers =
        CVarDef.Create("game.modes_use_total_players", true, CVar.SERVERONLY | CVar.ARCHIVE);
    /*
    * SysNotify
    */

    /// <summary>
    /// Dictionary for ping
    /// </summary>
    public static readonly CVarDef<string> SysNotifyCvar =
        CVarDef.Create("sysnotify.Dict", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How much we were wating to next ping
    /// </summary>
    public static readonly CVarDef<int> SysNotifyCoolDown =
        CVarDef.Create("sysnotify.cooldown", 1, CVar.CLIENTONLY | CVar.ARCHIVE);
    /// <summary>
    /// Get ping or no
    /// </summary>
    public static readonly CVarDef<bool> SysNotifyPerm =
        CVarDef.Create("sysnotify.permission", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SysNotifySoundPath =
        CVarDef.Create("sysnotifys.soundpath", "/Audio/Effects/balloon-pop.ogg", CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
    * Storage
    */

    /// <summary>
    ///     Allows opening multiple inventory/storage windows simultaneously.
    /// </summary>
    public static readonly CVarDef<bool> MultipleInventoryWindows =
        CVarDef.Create("storage.multiple_inventory_windows", false, CVar.CLIENTONLY | CVar.ARCHIVE);
    // DS14-start - scrolling screens accept two 32-character lines plus their separator.
    public static readonly CVarDef<int> MaxBroadcastLength =
        CVarDef.Create("chat.max_broadcast_length", 65, CVar.SERVER | CVar.REPLICATED);
    // DS14-end

    /*
    * Попауты
    */
    public static readonly CVarDef<bool> PopOutChat =
    CVarDef.Create("Chat.PopOut", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PsychiatryEnabled =
        CVarDef.Create("deadspace.psychiatry_enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> PsychiatryClientFx =
        CVarDef.Create("deadspace.psychiatry_client_fx", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PsychiatryOnsetCooldownSec =
        CVarDef.Create("deadspace.psychiatry_onset_cooldown_sec", 120f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAutoEscalateMinSec =
        CVarDef.Create("deadspace.psychiatry_auto_escalate_min_sec", 600f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAutoEscalateMaxSec =
        CVarDef.Create("deadspace.psychiatry_auto_escalate_max_sec", 600f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryScareMinSec =
        CVarDef.Create("deadspace.psychiatry_scare_min_sec", 30f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PsychiatryScareMaxSec =
        CVarDef.Create("deadspace.psychiatry_scare_max_sec", 120f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> PsychiatryWhisperMinSec =
        CVarDef.Create("deadspace.psychiatry_whisper_min_sec", 40f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryWhisperMaxSec =
        CVarDef.Create("deadspace.psychiatry_whisper_max_sec", 100f, CVar.SERVER | CVar.REPLICATED);

    // На острой стадии шёпот чаще. Интервал умножается на эти числа.
    public static readonly CVarDef<float> PsychiatryWhisperAcuteMinScale =
        CVarDef.Create("deadspace.psychiatry_whisper_acute_min_scale", 0.35f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryWhisperAcuteMaxScale =
        CVarDef.Create("deadspace.psychiatry_whisper_acute_max_scale", 0.45f, CVar.SERVER | CVar.REPLICATED);

    // Шанс, что шёпот будет про преступление, а не про насмешку.
    public static readonly CVarDef<float> PsychiatryWhisperCrimeChance =
        CVarDef.Create("deadspace.psychiatry_whisper_crime_chance", 0.5f, CVar.SERVER | CVar.REPLICATED);

    // Как часто шёпот прикидывается радио.
    public static readonly CVarDef<float> PsychiatryWhisperRadioSimple =
        CVarDef.Create("deadspace.psychiatry_whisper_radio_simple", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryWhisperRadioAcute =
        CVarDef.Create("deadspace.psychiatry_whisper_radio_acute", 0.7f, CVar.SERVER | CVar.REPLICATED);

    // Пауза после вдоха газа, чтобы одна лужа не накидала стадий пачкой.
    public static readonly CVarDef<float> PsychiatryGasOnsetSec =
        CVarDef.Create("deadspace.psychiatry_gas_onset_sec", 30f, CVar.SERVER | CVar.REPLICATED);

    // Шанс от падения. Луж и бананов много, поэтому он маленький.
    public static readonly CVarDef<float> PsychiatrySlipChance =
        CVarDef.Create("deadspace.psychiatry_slip_chance", 0.01f, CVar.SERVER | CVar.REPLICATED);

    // Если асфиксия выше этого числа, дефиб сажает болезнь сильнее.
    public static readonly CVarDef<float> PsychiatryDefibAsphyxiation =
        CVarDef.Create("deadspace.psychiatry_defib_asphyxiation", 60f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryDefibHighChance =
        CVarDef.Create("deadspace.psychiatry_defib_high_chance", 0.01f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryDefibLowChance =
        CVarDef.Create("deadspace.psychiatry_defib_low_chance", 0.01f, CVar.SERVER | CVar.REPLICATED);

    // 0.5 значит две стадии поровну.
    public static readonly CVarDef<float> PsychiatryDefibStageSplit =
        CVarDef.Create("deadspace.psychiatry_defib_stage_split", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAlcoholChance =
        CVarDef.Create("deadspace.psychiatry_alcohol_chance", 0.0005f, CVar.SERVER | CVar.REPLICATED);

    // ЭМИ для позитроника опаснее обычного удара током.
    public static readonly CVarDef<float> PsychiatryEmpChance =
        CVarDef.Create("deadspace.psychiatry_emp_chance", 0.20f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryIonShockMin =
        CVarDef.Create("deadspace.psychiatry_ion_shock_min", 50f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryIonChance =
        CVarDef.Create("deadspace.psychiatry_ion_chance", 0.02f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRadiationMin =
        CVarDef.Create("deadspace.psychiatry_radiation_min", 50f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRadiationChance =
        CVarDef.Create("deadspace.psychiatry_radiation_chance", 0.02f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRadiationStageSplit =
        CVarDef.Create("deadspace.psychiatry_radiation_stage_split", 0.5f, CVar.SERVER | CVar.REPLICATED);

    // Яды и топливо проверяются часто, поэтому шанс на одну проверку маленький.
    public static readonly CVarDef<float> PsychiatryHarmfulReagentChance =
        CVarDef.Create("deadspace.psychiatry_harmful_reagent_chance", 0.005f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAsphyxiationMin =
        CVarDef.Create("deadspace.psychiatry_asphyxiation_min", 40f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAsphyxiationSevere =
        CVarDef.Create("deadspace.psychiatry_asphyxiation_severe", 80f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryAsphyxiationChance =
        CVarDef.Create("deadspace.psychiatry_asphyxiation_chance", 0.005f, CVar.SERVER | CVar.REPLICATED);

    // Пауза между бросками удушья, радиации, шока и алкоголя. Яды и лекарства проверяются с той же паузой.
    public static readonly CVarDef<float> PsychiatryDamageRollGapSec =
        CVarDef.Create("deadspace.psychiatry_damage_roll_gap_sec", 10f, CVar.SERVER | CVar.REPLICATED);

    // Пауза между сменами стадии от урона и отравления. Обычный кулдаун болезни при этом тоже стоит.
    public static readonly CVarDef<float> PsychiatryHarmStageCooldownSec =
        CVarDef.Create("deadspace.psychiatry_harm_stage_cooldown_sec", 300f, CVar.SERVER | CVar.REPLICATED);

    // 0 полный иммунитет, 1 только случайные ситуации, 2 иммунитета нет.
    public static readonly CVarDef<int> PsychiatryAntagImmunityMode =
        CVarDef.Create("deadspace.psychiatry_antag_immunity", (int) global::Content.Shared.DeadSpace.Psychiatry.PsychiatryAntagImmunity.Partial, CVar.SERVER | CVar.REPLICATED);

    // Множитель к шансам лекарств из прототипов. 0 выключает весь список.
    public static readonly CVarDef<float> PsychiatryMedicineOnsetScale =
        CVarDef.Create("deadspace.psychiatry_medicine_onset_scale", 1f, CVar.SERVER | CVar.REPLICATED);

    // Сколько единиц в одной таблетке курса. Горит по 0.05 в секунду, около пяти минут.
    public static readonly CVarDef<float> PsychiatryCoursePillUnits =
        CVarDef.Create("deadspace.psychiatry_course_pill_units", 15f, CVar.SERVER | CVar.REPLICATED);

    // Шанс слепоты, глухоты или немоты у шока и зонда.
    public static readonly CVarDef<float> PsychiatryShockFaultChance =
        CVarDef.Create("deadspace.psychiatry_shock_fault_chance", 0.25f, CVar.SERVER | CVar.REPLICATED);

    // Сколько луж максимум зацепит один удар, чтобы не залить всю палубу.
    public static readonly CVarDef<int> PsychiatryPuddleChainCap =
        CVarDef.Create("deadspace.psychiatry_puddle_chain_cap", 48, CVar.SERVER | CVar.REPLICATED);

    // Как часто энцефалограф принимает пугалку. Не чаще самого короткого интервала между ними.
    public static readonly CVarDef<float> PsychiatryUnrealSoundCooldownSec =
        CVarDef.Create("deadspace.psychiatry_unreal_sound_cooldown_sec", 30f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryUnrealHearing =
        CVarDef.Create("deadspace.psychiatry_unreal_hearing", 0.9f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryUnrealFear =
        CVarDef.Create("deadspace.psychiatry_unreal_fear", 0.9f, CVar.SERVER | CVar.REPLICATED);

    // Чужие звуки. На латентной реже, на острой чаще. Дистанция в тайлах.
    public static readonly CVarDef<float> PsychiatryParacusiaLatentMinSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_latent_min_sec", 35f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaLatentMaxSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_latent_max_sec", 70f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaSimpleMinSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_simple_min_sec", 18f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaSimpleMaxSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_simple_max_sec", 40f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaAcuteMinSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_acute_min_sec", 8f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaAcuteMaxSec =
        CVarDef.Create("deadspace.psychiatry_paracusia_acute_max_sec", 18f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryParacusiaDistance =
        CVarDef.Create("deadspace.psychiatry_paracusia_distance", 7f, CVar.SERVER | CVar.REPLICATED);

    // Какая доля людей и предметов рядом выглядит не собой.
    public static readonly CVarDef<float> PsychiatryRemapMobLatent =
        CVarDef.Create("deadspace.psychiatry_remap_mob_latent", 0.45f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRemapMobSimple =
        CVarDef.Create("deadspace.psychiatry_remap_mob_simple", 0.65f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRemapMobAcute =
        CVarDef.Create("deadspace.psychiatry_remap_mob_acute", 0.9f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRemapItemLatent =
        CVarDef.Create("deadspace.psychiatry_remap_item_latent", 0.35f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRemapItemSimple =
        CVarDef.Create("deadspace.psychiatry_remap_item_simple", 0.45f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> PsychiatryRemapItemAcute =
        CVarDef.Create("deadspace.psychiatry_remap_item_acute", 0.55f, CVar.SERVER | CVar.REPLICATED);

    // На острой стадии подмена чаще монстр, чем животное.
    public static readonly CVarDef<float> PsychiatryRemapMonsterChance =
        CVarDef.Create("deadspace.psychiatry_remap_monster_chance", 200f / 255f, CVar.SERVER | CVar.REPLICATED);

    // На сколько клеток вокруг видны подмены и мясные стены.
    public static readonly CVarDef<float> PsychiatryRemapRadius =
        CVarDef.Create("deadspace.psychiatry_remap_radius", 12f, CVar.SERVER | CVar.REPLICATED);
}

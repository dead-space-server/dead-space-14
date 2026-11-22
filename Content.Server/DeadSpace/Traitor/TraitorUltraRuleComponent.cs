// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Random;
using Content.Shared.Roles.Components;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Traitor;

[RegisterComponent, Access(typeof(TraitorUltraRuleSystem))]
public sealed partial class TraitorUltraRuleComponent : Component
{
    public readonly Dictionary<EntityUid, TraitorUltraMindState> Minds = new();
    public readonly Dictionary<EntityUid, string?> PendingRecruitOffers = new();
    public readonly Dictionary<EntityUid, TimeSpan> ObjectiveTransitSince = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextCheck;

    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan UpgradeOfferDelay = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan UpgradeOfferTimeout = TimeSpan.FromMinutes(2);

    [DataField]
    public EntProtoId UpgradeOfferAction = "ActionTraitorUltraOpenContract";

    [DataField]
    public EntProtoId ExtraObjectiveOfferAction = "ActionTraitorUltraOpenExtraObjectiveOffer";

    [DataField]
    public TimeSpan BountyPreparationTime = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan TransplantGrace = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan RewardDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public FixedPoint2 UpgradeTelecrystals = 20;

    [DataField]
    public FixedPoint2 ExtraObjectiveTelecrystals = 5;

    [DataField]
    public EntProtoId UltraUplinkImplant = "TraitorUltraUplinkImplant";

    [DataField]
    public EntProtoId DeathAcidifierImplant = "DeathAcidifierImplant";

    [DataField]
    public FixedPoint2 TraitorKillRewardTelecrystals = 8;

    [DataField]
    public int SecurityKillRewardCredits = 10000;

    [DataField]
    public int CaptainKillRewardCredits = 10000;

    [DataField]
    public ProtoId<CargoAccountPrototype> SecurityRewardAccount = "Security";

    [DataField]
    public FixedPoint2 RecruitTelecrystals = 10;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> CorporationDataset = "TraitorCorporations";

    [DataField]
    public EntProtoId<MindRoleComponent> TraitorMindRole = "MindRoleTraitor";

    [DataField]
    public EntProtoId UltraMindRole = "MindRoleTraitorUltra";

    [DataField]
    public EntProtoId RecruitMindRole = "MindRoleTraitor";

    [DataField]
    public EntProtoId CommandKillObjective = "TraitorUltraKillRandomHeadObjective";

    [DataField]
    public EntProtoId BountyKillObjective = "TraitorUltraKillBountyObjective";

    [DataField]
    public ProtoId<WeightedRandomPrototype> BaseObjectiveGroups = "TraitorUltraObjectiveGroups";

    [DataField]
    public float BaseObjectiveMaxDifficulty = 5f;

    [DataField]
    public int BaseObjectiveMaxPicks = 20;

    [DataField]
    public ProtoId<WeightedRandomPrototype> RecruitObjectiveGroups = "TraitorObjectiveGroups";

    [DataField]
    public float RecruitObjectiveMaxDifficulty = 5f;

    [DataField]
    public int RecruitObjectiveMaxPicks = 20;

    [DataField]
    public List<EntProtoId> HighRiskStealObjectives = new()
    {
        "CaptainIDStealObjective",
        "CaptainGunStealObjective",
        "CaptainJetpackStealObjective",
        "HandTeleporterStealObjective",
        "RDHardsuitStealObjective",
        "WeaponX01StealObjective",
        "PistolBlueShieldStealObjective",
        "TabletRDStealObjective",
        "PinpointerNuclearStealObjective",
    };

    [DataField]
    public List<EntProtoId> PostUpgradeObjectives = new()
    {
        "TraitorUltraDestroyAtmosGasMinersObjective",
        "TraitorUltraDestroyAmeControllerObjective",
        "TraitorUltraHijackShuttleObjective",
        "TraitorUltraHijackTradeObjective",
        "TraitorUltraDestroyServersObjective",
        "NukeDiskStealObjective",
    };

    [DataField]
    public List<EntProtoId> RarePostUpgradeObjectives = new()
    {
        "TraitorUltraKillHalfSecurityObjective",
        "TraitorUltraDestroyStationAiCoreObjective",
    };

    [DataField]
    public HashSet<EntProtoId> ExtraObjectiveEligibleFirstObjectives = new()
    {
        "TraitorUltraHijackShuttleObjective",
        "TraitorUltraKillHalfSecurityObjective",
    };

    [DataField]
    public EntProtoId PostUpgradeSurviveObjective = "TraitorUltraSurviveObjective";

    [DataField]
    public float RarePostUpgradeObjectiveProbability = 0.15f;

    [DataField]
    public HashSet<EntProtoId> UpgradeCompletionIgnoredObjectives = new()
    {
        "EscapeShuttleObjective",
        "DieObjective",
    };

    [DataField]
    public HashSet<EntProtoId> UpgradeCompletionOptionalObjectives = new()
    {
        "TraitorUltraKillBountyObjective",
    };

    [DataField]
    public SoundSpecifier UpgradeSound = new SoundPathSpecifier("/Audio/_DeadSpace/TraitorUltra/ultra_role_assigned.ogg");

    [DataField]
    public SoundSpecifier BountyAnnouncementSound = new SoundPathSpecifier("/Audio/_DeadSpace/TraitorUltra/contract_transfer_announcement.ogg");

    [DataField]
    public ProtoId<CurrencyPrototype> TelecrystalCurrency = "Telecrystal";

    [DataField]
    public string CorporateDiscountModifierSourceId = "traitor-ultra-corporate-discount";

    [DataField]
    public List<TraitorUltraCorporateDiscountSet> CorporateDiscounts = new()
    {
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-1",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraStorageImplanter", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraEmpImplanter", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSyndicateWeaponModule", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraEnergyShield", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraEsword", 2),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-2",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraC20R", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraBulldog", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraGrenadeLauncher", 3),
                new TraitorUltraListingDiscount("UplinkTraitorUltraRPG70", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraGorlexHypospray", 3),
                new TraitorUltraListingDiscount("UplinkTraitorUltraClothingOuterVestWebElite", 1),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-3",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraStimpack2", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraCombatMedipen2", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraChemistryKitBundle", 4),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSyndicateChemBundle", 5),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-4",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraC4", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraC4Bundle", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSyndieMiniBomb", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSyndicateJawsOfLife", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraToolbox", 1),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-5",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraChameleonProjector", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraStealthBox", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraAgentIDCard", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraVoiceMask", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSlipocalypseClusterSoap", 1),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-6",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraPistolCobra", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraC20R", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraBulldog", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraPistolDesertEagle", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraMagazineEagleAP2", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraWeaponSniperR17", 3),
                new TraitorUltraListingDiscount("UplinkTraitorUltraXC67Bundle", 3),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-7",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraStimpack2", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraStimkit", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraCombatBakery", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSurplusBundle", 2),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-8",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraEmag", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraAccessBreaker", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSyndicateJawsOfLife", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraRadioJammer", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraCameraBug", 1),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-9",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraAgentIDCard", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraChameleon", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraVoiceMask", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraHypopen", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraNocturineChemistryBottle", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraSmugglerSatchel", 1),
            },
        },
        new TraitorUltraCorporateDiscountSet
        {
            Corporation = "traitor-corporations-dataset-10",
            Listings =
            {
                new TraitorUltraListingDiscount("UplinkTraitorUltraNukeHardsuit", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraCommanderHardsuit", 3),
                new TraitorUltraListingDiscount("UplinkTraitorUltraJuggernaut", 6),
                new TraitorUltraListingDiscount("UplinkTraitorUltraEnergyShield", 2),
                new TraitorUltraListingDiscount("UplinkTraitorUltraClothingOuterVestWebElite", 1),
                new TraitorUltraListingDiscount("UplinkTraitorUltraEswordDoubleAgents", 2),
            },
        },
    };
}

[DataDefinition]
public sealed partial class TraitorUltraCorporateDiscountSet
{
    [DataField(required: true)]
    public string Corporation = string.Empty;

    [DataField]
    public List<TraitorUltraListingDiscount> Listings = new();
}

[DataDefinition]
public sealed partial class TraitorUltraListingDiscount
{
    public TraitorUltraListingDiscount()
    {
    }

    public TraitorUltraListingDiscount(ProtoId<ListingPrototype> listing, FixedPoint2 telecrystalDiscount)
    {
        Listing = listing;
        TelecrystalDiscount = telecrystalDiscount;
    }

    [DataField(required: true)]
    public ProtoId<ListingPrototype> Listing;

    [DataField]
    public FixedPoint2 TelecrystalDiscount;
}

public sealed class TraitorUltraMindState
{
    public TraitorUltraStage Stage = TraitorUltraStage.Initial;
    public List<EntityUid> InitialObjectives = new();
    public EntityUid? EligibleBody;
    public string? OriginalCorporation;
    public string? NewCorporation;
    public string? AgentName;
    public FixedPoint2 BountyReward;
    public TimeSpan NextEventTime;
    public bool BountyAnnounced;
    public bool BountyResolved;
    public bool BaseObjectivesAssigned;
    public bool InitialObjectivePackageAssigned;
    public bool UltraUplinkInitialized;
    public bool BountyAnnouncementSuppressed;
    public EntityUid? BountyBody;
    public TimeSpan? BountyMovedAt;
    public EntityUid? UltraUplinkEntity;
    public EntityUid? UpgradeOfferActionEntity;
    public EntityUid? FirstPostUpgradeObjective;
    public string? FirstPostUpgradeObjectivePrototype;
    public EntityUid? PendingExtraObjective;
    public string? PendingExtraObjectivePrototype;
    public EntityUid? ExtraObjectiveOfferActionEntity;
    public TraitorUltraExtraObjectiveOfferStatus ExtraObjectiveOfferStatus = TraitorUltraExtraObjectiveOfferStatus.None;
}

public enum TraitorUltraStage : byte
{
    Initial,
    CompletionPopupSent,
    OfferOpen,
    Declined,
    Upgraded,
    BountyAnnounced,
    Resolved,
}

public enum TraitorUltraExtraObjectiveOfferStatus : byte
{
    None,
    WaitingForPrimaryCompletion,
    Open,
    Accepted,
    Declined,
}

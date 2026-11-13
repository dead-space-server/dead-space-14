// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Antag;

namespace Content.Client.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingSlaveClientSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const string SlaveFactionId = "ShadowlingSlaveFaction";
    private const string SlaveFactionAllyId = "ShadowlingSlaveFactionAlly";
    private const string SlaveFactionEnemyId = "ShadowlingSlaveFactionEnemy";
    private const string MasterFactionId = "ShadowlingMasterFaction";
    private const string MasterFactionAllyId = "ShadowlingMasterFactionAlly";
    private const string MasterFactionEnemyId = "ShadowlingMasterFactionEnemy";
    private const string LayerKey = "ShadowlingSlaveEyes";
    private const string DefaultEyesState = "shadowling_slave-eyes";

    private readonly ResPath _rsiPath = new("/Textures/_DeadSpace/Demons/shadowling.rsi");

    private readonly string[] _customEyesRaces = { "MobArachnid", "MobMoth", "MobVox" };
    private readonly string[] _hideEyesRaces = { "MobXenomorph", "MobIPC", "MobDiona" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingSlaveComponent, GetStatusIconsEvent>(OnGetSlaveIcon);
        SubscribeLocalEvent<ShadowlingRecruitComponent, GetStatusIconsEvent>(OnGetMasterIcon);
        SubscribeLocalEvent<ShadowlingRevealComponent, GetStatusIconsEvent>(OnGetMasterIcon);
        SubscribeLocalEvent<ShadowlingSlaveComponent, ComponentStartup>(OnSlaveStartup);
        SubscribeLocalEvent<ShadowlingSlaveComponent, ComponentShutdown>(OnSlaveShutdown);
    }

    private void OnSlaveStartup(EntityUid uid, ShadowlingSlaveComponent component, ComponentStartup args)
    {
        UpdateSlaveAppearance(uid, true);
    }

    private void OnSlaveShutdown(EntityUid uid, ShadowlingSlaveComponent component, ComponentShutdown args)
    {
        UpdateSlaveAppearance(uid, false);
    }

    private void UpdateSlaveAppearance(EntityUid uid, bool isSlave)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!isSlave)
        {
            if (sprite.LayerMapTryGet(LayerKey, out var layer))
                sprite.LayerSetVisible(layer, false);
            return;
        }

        var protoId = MetaData(uid).EntityPrototype?.ID;
        if (protoId == null)
            return;

        if (_hideEyesRaces.Contains(protoId))
            return;

        string state;
        if (_customEyesRaces.Contains(protoId))
            state = $"shadowling_slave-eyes_{protoId}";
        else
            state = DefaultEyesState;

        if (!sprite.LayerMapTryGet(LayerKey, out var eyesLayer))
        {
            var targetIndex = 0;
            if (sprite.LayerMapTryGet(HumanoidVisualLayers.Eyes, out var humanoidEyes))
                targetIndex = humanoidEyes + 1;

            eyesLayer = sprite.AddLayer(new SpriteSpecifier.Rsi(_rsiPath, state), targetIndex);
            sprite.LayerMapSet(LayerKey, eyesLayer);
        }
        else
        {
            sprite.LayerSetRSI(eyesLayer, _rsiPath);
            sprite.LayerSetState(eyesLayer, state);
        }

        sprite.LayerSetVisible(eyesLayer, true);
    }

    private bool IsMaster(EntityUid uid)
    {
        return HasComp<ShadowlingRecruitComponent>(uid) ||
               HasComp<ShadowlingRevealComponent>(uid);
    }

    private void OnGetSlaveIcon(EntityUid uid, ShadowlingSlaveComponent component, ref GetStatusIconsEvent args)
    {
        if (!IsShadowlingFaction(out var localPlayer))
            return;

        string factionId;

        if (HasComp<ShowAntagIconsComponent>(localPlayer))
        {
            factionId = SlaveFactionId;
        }
        else if (uid == localPlayer)
        {
            factionId = SlaveFactionAllyId;
        }
        else if (TryComp<ShadowlingSlaveComponent>(localPlayer, out var mySlave) && mySlave.Master != null)
        {
            factionId = component.Master == mySlave.Master ? SlaveFactionAllyId : SlaveFactionEnemyId;
        }
        else if (IsMaster(localPlayer))
        {
            factionId = component.Master == localPlayer ? SlaveFactionAllyId : SlaveFactionEnemyId;
        }
        else
        {
            factionId = SlaveFactionEnemyId;
        }

        if (_prototype.TryIndex<FactionIconPrototype>(factionId, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnGetMasterIcon(EntityUid uid, IComponent component, ref GetStatusIconsEvent args)
    {
        if (!IsShadowlingFaction(out var localPlayer))
            return;

        string factionId;

        if (HasComp<ShowAntagIconsComponent>(localPlayer))
        {
            factionId = MasterFactionId;
        }
        else if (uid == localPlayer)
        {
            factionId = MasterFactionAllyId;
        }
        else if (TryComp<ShadowlingSlaveComponent>(localPlayer, out var mySlave) && mySlave.Master != null)
        {
            factionId = mySlave.Master == uid ? MasterFactionAllyId : MasterFactionEnemyId;
        }
        else
        {
            factionId = MasterFactionEnemyId;
        }

        if (_prototype.TryIndex<FactionIconPrototype>(factionId, out var icon))
            args.StatusIcons.Add(icon);
    }

    private bool IsShadowlingFaction(out EntityUid localPlayer)
    {
        localPlayer = EntityUid.Invalid;
        var player = _player.LocalEntity;
        if (player == null)
            return false;

        localPlayer = player.Value;
        return HasComp<ShadowlingRecruitComponent>(localPlayer) ||
               HasComp<ShadowlingSlaveComponent>(localPlayer) ||
               HasComp<ShadowlingRevealComponent>(localPlayer) ||
               HasComp<ShowAntagIconsComponent>(localPlayer);
    }
}
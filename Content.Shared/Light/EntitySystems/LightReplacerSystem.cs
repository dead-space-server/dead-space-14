using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Light.Components;
using Content.Shared.Light.Events;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Light.EntitySystems;

/// <summary>
/// System that handles light replacer tool (which picks proper light entity for socket
///  and replaces it with other one, hopefully working one!).
/// </summary>
public sealed partial class LightReplacerSystem : EntitySystem
{
    // DS14-start: current engine baseline uses readonly IoC fields and explicit event subscriptions.
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityProviderSystem _provider = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private EntityQuery<LightBulbComponent> _lightBulbQuery;

    public override void Initialize()
    {
        base.Initialize();

        _lightBulbQuery = GetEntityQuery<LightBulbComponent>();

        SubscribeLocalEvent<LightReplacerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LightReplacerComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<LightReplacerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LightReplacerComponent, EjectLightTypeMessage>(OnEjectMessage);
        SubscribeLocalEvent<LightReplacerComponent, SwitchLightTypeMessage>(OnSwitchMessage);
        SubscribeLocalEvent<LightBulbComponent, EntityProviderInsertCheckEvent>(OnLightProviderInsertedCheck);
    }
    // DS14-end

    /// <summary> Adds contents info into examine. </summary>
    private void OnExamined(Entity<LightReplacerComponent> replacer, ref ExaminedEvent args)
    {
        if (!_provider.TryGetEntityCounter(replacer.Owner, out var entities))
            return;

        using (args.PushGroup(nameof(LightReplacerComponent)))
        {
            if (entities.Count == 0)
            {
                args.PushMarkup(Loc.GetString("comp-light-replacer-no-lights"));
                return;
            }

            args.PushMarkup(Loc.GetString("comp-light-replacer-has-lights"));

            foreach (var bulb in entities)
            {
                if (!_prototypeManager.Resolve(bulb.Key, out var bulbPrototype)) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
                    continue;

                args.PushMarkup(Loc.GetString("comp-light-replacer-light-listing", ("amount", bulb.Value), ("name", bulbPrototype.Name)));
            }
        }
    }

    /// <summary> Attempts to open UI for replacer, if there are any viable options to put into it. </summary>
    private void OnUse(Entity<LightReplacerComponent> replacer, ref UseInHandEvent args)
    {
        if (args.Handled || !_provider.TryGetEntityCounter(replacer.Owner, out var entities))
            return;

        args.ApplyDelay = false;

        if (entities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("comp-light-replacer-open-empty", ("light-replacer", replacer)), replacer, args.User);
            return;
        }

        args.Handled = true;
        _ui.OpenUi(replacer.Owner, LightReplacerUiKey.Key, args.User);
    }

    /// <summary> Tries to replace bulb/tube if applicable.</summary>
    private void OnAfterInteract(Entity<LightReplacerComponent> replacer, ref AfterInteractEvent eventArgs)
    {
        if (eventArgs.Handled
            || !eventArgs.CanReach // standard interaction checks
            || eventArgs.Target == null) // behavior will depend on the target type
            return;

        var targetUid = (EntityUid) eventArgs.Target;

        // replace broken light in fixture?
        if (TryComp<PoweredLightComponent>(targetUid, out var fixture))
            eventArgs.Handled = TryReplaceBulb(replacer.AsNullable(), (targetUid, fixture), eventArgs.User);
    }

    private void OnEjectMessage(Entity<LightReplacerComponent> replacer, ref EjectLightTypeMessage args)
    {
        _provider.TryEjectEntities(replacer.Owner, args.LightEntProtoId, out _, user: args.Actor);
    }

    /// <summary> Attempts to switch currently selected bulb type according to request. </summary>
    private void OnSwitchMessage(Entity<LightReplacerComponent> replacer, ref SwitchLightTypeMessage args)
    {
        if (!_prototypeManager.Resolve(args.LightEntProtoId, out var prototype) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
            || !_provider.TryGetEntityCounter(replacer.Owner, out var entities)
            || !entities.TryGetValue(args.LightEntProtoId, out var amount)
            || amount <= 0)
            return;

        if (args.LightType == LightBulbType.Tube)
            replacer.Comp.ActiveLightTube = args.LightEntProtoId;
        else
            replacer.Comp.ActiveLightBulb = args.LightEntProtoId;

        Dirty(replacer);

        _audio.PlayPredicted(replacer.Comp.Sound, replacer, args.Actor);
    }

    private void OnLightProviderInsertedCheck(Entity<LightBulbComponent> bulb, ref EntityProviderInsertCheckEvent args)
    {
        if (bulb.Comp.State == LightBulbState.Broken)
            args.FailureMessage = Loc.GetString("comp-light-replacer-insert-broken-light");
    }

    /// <summary>
    /// Try to replace a light bulb in <paramref name="lightHolder"/>
    /// using light replacer. Light fixture should have <see cref="PoweredLightComponent"/>.
    /// </summary>
    /// <param name="replacer">The light replacer used to replace the bulb.</param>
    /// <param name="lightHolder">The fixture whose light is being replaced.</param>
    /// <param name="userUid">The user who is replacing the light.</param>
    /// <returns>True if successfully replaced light, false otherwise</returns>
    public bool TryReplaceBulb(Entity<LightReplacerComponent?> replacer, Entity<PoweredLightComponent?> lightHolder, EntityUid? userUid = null)
    {
        if (!Resolve(replacer, ref replacer.Comp)
            || !Resolve(lightHolder, ref lightHolder.Comp))
            return false;

        var activeType = lightHolder.Comp.BulbType == LightBulbType.Tube
            ? replacer.Comp.ActiveLightTube
            : replacer.Comp.ActiveLightBulb;

        // check if light bulb is broken or missing
        EntityUid? currentBulbInHolder = _poweredLight.GetBulb(lightHolder, lightHolder.Comp);
        if (currentBulbInHolder != null)
        {
            if (!_lightBulbQuery.TryComp(currentBulbInHolder.Value, out var fixtureBulb))
                return false;

            var prototype = MetaData(currentBulbInHolder.Value).EntityPrototype;

            if (fixtureBulb.State == LightBulbState.Normal && prototype != null && prototype.ID == activeType)
            {
                _popup.PopupClient(Loc.GetString("comp-light-replacer-same-light", ("light", currentBulbInHolder)), lightHolder, userUid, PopupType.Medium); // DS14 - pre-self-predicting popup API.
                return false;
            }
        }

        if (!_provider.TryGetEntity(replacer.Owner, activeType, out var insertedBulb))
        {
            if (userUid == null || !_prototypeManager.Resolve(activeType, out var bulbPrototype)) // DS14 - current engine has no EntitySystem.ProtoMan shortcut.
                return false;

            var msg = Loc.GetString("comp-light-replacer-missing-light",
                ("light-name", bulbPrototype.Name),
                ("light-replacer", replacer));
            _popup.PopupEntity(msg, replacer, userUid.Value);
            return false;
        }

        // insert it into fixture
        var wasReplaced = _poweredLight.ReplaceBulb(lightHolder, insertedBulb.Value, lightHolder.Comp);
        if (wasReplaced)
        {
            _audio.PlayPredicted(replacer.Comp.Sound, replacer, userUid);
        }

        return wasReplaced;
    }
}

[Serializable, NetSerializable]
public enum LightReplacerUiKey : byte
{
    Key,
}

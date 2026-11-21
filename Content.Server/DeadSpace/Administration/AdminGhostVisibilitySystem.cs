// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Administration;

public sealed partial class AdminGhostVisibilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> HideContextMenuTag = "HideContextMenu";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminGhostVisibilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AdminGhostVisibilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AdminGhostVisibilityComponent, AGhostToggleVisibilityActionEvent>(OnToggleVisibility);
    }

    private void OnStartup(EntityUid uid, AdminGhostVisibilityComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionToggleVisibilityEntity, component.ActionToggleVisibility);
        _actions.SetToggled(component.ActionToggleVisibilityEntity, component.Hidden);

        if (component.Hidden)
            HideFromContextMenu(uid, component);
    }

    private void OnShutdown(EntityUid uid, AdminGhostVisibilityComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionToggleVisibilityEntity);

        if (!Terminating(uid))
            RestoreContextMenu(uid, component);
    }

    private void OnToggleVisibility(EntityUid uid, AdminGhostVisibilityComponent component, AGhostToggleVisibilityActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetHidden(uid, component, !component.Hidden);

        var popup = component.Hidden
            ? Loc.GetString("admin-ghost-visibility-hidden")
            : Loc.GetString("admin-ghost-visibility-visible");

        _popup.PopupEntity(popup, uid, uid);
    }

    private void SetHidden(EntityUid uid, AdminGhostVisibilityComponent component, bool hidden)
    {
        if (component.Hidden == hidden)
            return;

        component.Hidden = hidden;

        if (hidden)
            HideFromContextMenu(uid, component);
        else
            RestoreContextMenu(uid, component);

        _actions.SetToggled(component.ActionToggleVisibilityEntity, hidden);
        Dirty(uid, component);
    }

    private void HideFromContextMenu(EntityUid uid, AdminGhostVisibilityComponent component)
    {
        if (_tag.HasTag(uid, HideContextMenuTag))
            return;

        _tag.AddTag(uid, HideContextMenuTag);
        component.AddedHideContextMenuTag = true;
    }

    private void RestoreContextMenu(EntityUid uid, AdminGhostVisibilityComponent component)
    {
        if (!component.AddedHideContextMenuTag)
            return;

        _tag.RemoveTag(uid, HideContextMenuTag);
        component.AddedHideContextMenuTag = false;
    }
}

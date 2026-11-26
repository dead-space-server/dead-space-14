// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Loadout;

/// <summary>
/// A window around <see cref="LoadoutEditorControl"/>, opened by the "loadout" buttons of the profile editor (and for
/// antagonists). It only adds opaque window chrome and sizing; the events are the control's.
/// </summary>
public sealed class LoadoutEditorWindow : FancyWindow
{
    private static readonly Vector2 DefaultWindowSize = new(1280, 760);
    private static readonly Vector2 MinWindowSize = new(1100, 680);

    public readonly LoadoutEditorControl Editor;

    public event Action<string>? OnNameChanged
    {
        add => Editor.OnNameChanged += value;
        remove => Editor.OnNameChanged -= value;
    }

    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutPressed
    {
        add => Editor.OnLoadoutPressed += value;
        remove => Editor.OnLoadoutPressed -= value;
    }

    public event Action<ProtoId<LoadoutGroupPrototype>, ProtoId<LoadoutPrototype>>? OnLoadoutUnpressed
    {
        add => Editor.OnLoadoutUnpressed += value;
        remove => Editor.OnLoadoutUnpressed -= value;
    }

    public event Action<JobPrototype>? OnJobSelected
    {
        add => Editor.OnJobSelected += value;
        remove => Editor.OnJobSelected -= value;
    }

    public event Action<AntagPrototype>? OnAntagSelected
    {
        add => Editor.OnAntagSelected += value;
        remove => Editor.OnAntagSelected -= value;
    }

    public event Action? OnSaveRequested
    {
        add => Editor.OnSaveRequested += value;
        remove => Editor.OnSaveRequested -= value;
    }

    public event Action? OnResetRequested
    {
        add => Editor.OnResetRequested += value;
        remove => Editor.OnResetRequested -= value;
    }

    public LoadoutEditorWindow(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        RoleLoadoutPrototype proto,
        ICommonSession session,
        IDependencyCollection collection,
        JobPrototype? job,
        AntagPrototype? antag)
    {
        Editor = new LoadoutEditorControl(profile, loadout, proto, session, collection, job, antag);
        ContentsContainer.AddChild(Editor);

        // The screen covers the profile editor, so every surface of it is opaque, the window chrome included.
        foreach (var child in Children)
        {
            if (child is PanelContainer background)
                background.PanelOverride = new LoadoutStyleBox(LoadoutBoxKind.Window, 10);
        }

        if (FindHeader(this) is { } header)
            header.PanelOverride = new LoadoutStyleBox(LoadoutBoxKind.Header, 0);
    }

    public void RefreshLoadouts(RoleLoadout loadout, ICommonSession session, IDependencyCollection collection)
    {
        Editor.RefreshLoadouts(loadout, session, collection);
    }

    public void SetUnsaved(bool unsaved)
    {
        Editor.SetUnsaved(unsaved);
    }

    /// <summary>Opens the window centred at its default size, shrunk only when the screen is smaller than that.</summary>
    public void OpenFitted()
    {
        var root = UserInterfaceManager.WindowRoot.Size;
        var size = DefaultWindowSize;

        // Before the first layout pass the root can still report no size; keep the default then.
        if (root.X > 64 && root.Y > 64)
            size = Vector2.Min(size, root - new Vector2(16, 16));

        MinSize = Vector2.Min(MinWindowSize, size);
        SetSize = size;
        OpenCentered();
        RecenterWindow(new Vector2(0.5f, 0.5f));
    }

    private static PanelContainer? FindHeader(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is PanelContainer { Name: "WindowHeader" } header)
                return header;

            if (FindHeader(child) is { } found)
                return found;
        }

        return null;
    }
}

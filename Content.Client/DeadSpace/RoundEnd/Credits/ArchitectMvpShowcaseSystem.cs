// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

/// <summary>
/// Plays the Architect MVP scene full screen when an admin runs <c>architectmvp</c>.
/// </summary>
public sealed partial class ArchitectMvpShowcaseSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private RoundEndDollPreviewSystem _previews = default!;

    private ArchitectMvpShowcase? _showcase;
    private int _owner;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ArchitectMvpShowcaseEvent>(OnShowcase);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Close();
    }

    private void OnShowcase(ArchitectMvpShowcaseEvent ev)
    {
        Close();

        var showcase = new ArchitectMvpShowcase(EntityManager) { CharacterName = ev.Name };
        _ui.PopupRoot.AddChild(showcase);
        LayoutContainer.SetAnchorPreset(showcase, LayoutContainer.LayoutPreset.Wide);
        _showcase = showcase;

        _owner = _previews.CreateOwner();
        _previews.Request(_owner, ArchitectMvpShowcase.ArchitectLook, snapshot => showcase.SetCharacter(snapshot));
        showcase.SetCast(ev.Cast, _previews, _owner);

        // Removing a control while the interface is updating it is not safe, so the clean-up waits a frame.
        showcase.Finished += () => _ui.DeferAction(() =>
        {
            if (_showcase == showcase)
                Close();
        });
        showcase.Play();
    }

    private void Close()
    {
        _showcase?.Orphan();
        _showcase = null;

        if (_owner == 0)
            return;

        _previews.Cancel(_owner);
        _owner = 0;
    }
}

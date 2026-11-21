// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.AdminToy;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.AdminToy;

public sealed partial class AdminToySelectionEui : BaseEui
{
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly NetEntity _target;

    public AdminToySelectionEui(NetEntity target)
    {
        _target = target;
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminToySelectionEuiState(_target);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not AdminToySelectedMessage selected)
            return;

        var target = _entityManager.GetEntity(_target);
        _entityManager.System<AdminToySystem>().TrySpawnToy(
            Player,
            target,
            selected.Prototype,
            selected.Name,
            selected.Description,
            selected.TtsVoice);
        Close();
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RoundEnd;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed class RoundEndCreditsDollView : SpriteView
{
    private static readonly Direction[] SpinOrder = { Direction.South, Direction.East, Direction.North, Direction.West };

    private readonly RoundEndDollData _data;
    private readonly RoundEndDollPreviewSystem _previews;
    private int _owner;

    public RoundEndCreditsDollView(RoundEndDollData data, RoundEndDollPreviewSystem previews, IEntityManager entityManager)
        : base(entityManager)
    {
        _data = data;
        _previews = previews;
        OverrideDirection = Direction.South;
    }

    public bool Spin { get; set; }

    public void Request(int owner)
    {
        _owner = owner;
        _previews.Request(owner, _data, snapshot => OnSnapshotReady(owner, snapshot));
    }

    public void Release()
    {
        _owner = 0;
        SetEntity(null);
    }

    public void Animate(float time)
    {
        if (!Spin)
            return;

        var direction = SpinOrder[(int) (time / 0.45f) % SpinOrder.Length];
        if (OverrideDirection != direction)
            OverrideDirection = direction;
    }

    private void OnSnapshotReady(int owner, EntityUid? snapshot)
    {
        if (owner != _owner)
            return;

        SetEntity(snapshot);
    }
}

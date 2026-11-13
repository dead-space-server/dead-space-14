using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Lavaland;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Lavaland;

[UsedImplicitly]
public sealed partial class LavalandMiningVoucherBoundUserInterface : BoundUserInterface
{
    private static readonly EntProtoId VoucherPrototype = "LavalandMiningVoucher";

    [Dependency] private IPrototypeManager _prototype = default!;

    private SimpleRadialMenu? _menu;

    public LavalandMiningVoucherBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        if (State is LavalandMiningVoucherBoundUserInterfaceState voucherState)
            UpdateState(voucherState);
        else
            SendMessage(new LavalandMiningVoucherRequestUpdateMessage());

        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not LavalandMiningVoucherBoundUserInterfaceState voucherState || _menu == null)
            return;

        _menu.SetButtons(CreateButtons(voucherState));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
        _menu = null;
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(LavalandMiningVoucherBoundUserInterfaceState state)
    {
        var options = new List<RadialMenuOptionBase>(state.Rewards.Count + 1);

        if (state.Powered)
        {
            foreach (var reward in state.Rewards)
            {
                options.Add(new RadialMenuActionOption<int>(OnRedeem, reward.Index)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(reward.IconPrototype),
                    ToolTip = GetRewardTooltip(reward),
                });
            }
        }

        options.Add(new RadialMenuActionOption<bool>(_ => OnEject(), true)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(VoucherPrototype),
            ToolTip = state.Powered
                ? Loc.GetString("lavaland-mining-voucher-ui-eject")
                : Loc.GetString("lavaland-mining-voucher-vendor-unpowered"),
        });

        return options;
    }

    private string GetRewardTooltip(LavalandMiningVoucherEntry reward)
    {
        if (reward.Contents.Count == 0)
            return reward.Name;

        var contents = new List<string>(reward.Contents.Count);
        foreach (var item in reward.Contents)
        {
            var name = _prototype.TryIndex<EntityPrototype>(item.Prototype, out var proto)
                ? Loc.GetString(proto.Name)
                : item.Prototype.Id;

            contents.Add(Loc.GetString(
                "lavaland-mining-voucher-radial-item",
                ("amount", item.Amount),
                ("item", name)));
        }

        return Loc.GetString(
            "lavaland-mining-voucher-radial-tooltip",
            ("name", reward.Name),
            ("contents", string.Join("\n", contents)));
    }

    private void OnRedeem(int index)
    {
        SendMessage(new LavalandMiningVoucherRedeemMessage { Index = index });
    }

    private void OnEject()
    {
        SendMessage(new LavalandMiningVoucherEjectMessage());
    }
}

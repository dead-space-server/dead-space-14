using System.Linq;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Content.Client.DeadSpace.Crayon; //DS-14

namespace Content.Client.Crayon.UI
{
    public sealed class CrayonBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IPrototypeManager _protoManager = default!;
        [Dependency] private readonly CrayonGhostSystem _crayonGhostSystem = default!; //DS-14

        [ViewVariables]
        private CrayonWindow? _menu;

        public CrayonBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindowCenteredLeft<CrayonWindow>();
            _menu.OnColorSelected += SelectColor;
            _menu.OnSelected += Select;
            //DS-14 Start
            _menu.OnRotationChanged += rotation =>
            {
                _crayonGhostSystem.SetRotation(rotation);
                SendMessage(new CrayonRotationMessage(rotation));
            };
            _menu.OnOpacityChanged += opacity => SendMessage(new CrayonOpacityMessage(opacity));
            if (EntMan.TryGetComponent<CrayonComponent>(Owner, out var crayon))
                _menu.SetOpacity(crayon);
            //DS-14 End
            PopulateCrayons();
        }

        private void PopulateCrayons()
        {
            var crayonDecals = _protoManager.EnumeratePrototypes<DecalPrototype>().Where(x => x.Tags.Contains("crayon"));
            _menu?.Populate(crayonDecals.ToList());
        }

        public override void OnProtoReload(PrototypesReloadedEventArgs args)
        {
            base.OnProtoReload(args);

            if (!args.WasModified<DecalPrototype>())
                return;

            PopulateCrayons();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            base.ReceiveMessage(message);

            if (_menu is null || message is not CrayonUsedMessage crayonMessage)
                return;

            _menu.AdvanceState(crayonMessage.DrawnDecal);
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            _menu?.UpdateState((CrayonBoundUserInterfaceState) state);
            if (_menu != null && EntMan.TryGetComponent<CrayonComponent>(Owner, out var crayon))
                _menu.SetOpacity(crayon); //DS-14
        }

        public void Select(string state)
        {
            SendMessage(new CrayonSelectMessage(state));
        }

        public void SelectColor(Color color)
        {
            SendMessage(new CrayonColorMessage(color));
        }
    }
}

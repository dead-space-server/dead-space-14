// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.UI;
using Robust.Client.Graphics;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Eui
{
    [UsedImplicitly]
    public sealed class YesNoEui : BaseEui
    {
        private readonly YesNoWindow _window;
        private bool _closed;

        // Parameterless ctor is required for dynamic activation via IoC/Reflection.
        public YesNoEui() : this(state: null)
        {
        }

        public YesNoEui(YesNoEuiState? state = null)
        {
            var title = state?.Title ?? "Question";
            var text = state?.Text ?? "Proceed?";

            _window = new YesNoWindow(title, text);

            _window.NoButton.OnPressed += _ => Reply(YesNoUiButton.No);
            _window.YesButton.OnPressed += _ => Reply(YesNoUiButton.Yes);
            _window.OnClose += () => Reply(YesNoUiButton.No);
        }

        private void Reply(YesNoUiButton choice)
        {
            if (_closed)
                return;

            _closed = true;
            SendMessage(new YesNoChoiceMessage(choice));
            _window.Close();
        }

        public override void Opened()
        {
            IoCManager.Resolve<IClyde>().RequestWindowAttention();
            _window.OpenCentered();
        }

        public override void HandleState(EuiStateBase state)
        {
            if (state is YesNoEuiState s)
            {
                _window.Title = s.Title;
                _window.MessageLabel.SetMessage(s.Text);
            }
        }

        public override void Closed()
        {
            _closed = true;
            _window.Close();
        }
    }
}

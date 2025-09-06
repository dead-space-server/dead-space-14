using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        private readonly AdminAnnounceWindow _window;

        public AdminAnnounceEui()
        {
            _window = new AdminAnnounceWindow();
            _window.OnClose += () => SendMessage(new CloseEuiMessage());
            _window.AnnounceButton.OnPressed += AnnounceButtonOnOnPressed;
        }

        private void AnnounceButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            SendMessage(new AdminAnnounceEuiMsg.DoAnnounce
            {
                Announcement = Rope.Collapse(_window.Announcement.TextRope),
                Announcer =  _window.Announcer.Text,
                AnnounceType =  (AdminAnnounceType) (_window.AnnounceMethod.SelectedMetadata ?? AdminAnnounceType.Station),
                CloseAfter = !_window.KeepWindowOpen.Pressed,
                Voice = (string) (_window.VoiceSelector.SelectedMetadata ?? ""),
                EnableTTS = _window.EnableTTS.Pressed,
                CustomTTS = _window.CustomTTS.Pressed,
                ColorHex = _window.ColorHexText, // DS14-announce-color
                SoundPath = _window.SoundPathText, // DS14-announce-audio
                SoundVolume = _window.SoundVolumeValue, // DS14-announce-volume
                Sender = _window.SenderText // DS14-announce-sender
            });

        }

        public override void Opened()
        {
            _window.OpenCentered();
        }

        public override void Closed()
        {
            _window.Close();
        }
    }
}

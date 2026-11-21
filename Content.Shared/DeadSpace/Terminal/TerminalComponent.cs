using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Terminal;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TerminalComponent : Component
{
    [DataField, AutoNetworkedField]
    public string OsName = "TempOS 107.05 LTS";

    [AutoNetworkedField]
    public string Motd = string.Empty;

    [AutoNetworkedField]
    public int UserIndex;

    [AutoNetworkedField]
    public int IpFirst;

    [AutoNetworkedField]
    public int IpSecond;

    [AutoNetworkedField]
    public int IpThird;

    [AutoNetworkedField]
    public string CurrentDir = "/";

    [AutoNetworkedField]
    public string User = "user";

    [AutoNetworkedField]
    public string Home = "/";

    [AutoNetworkedField]
    public string Hostname = "localhost";

    public string IpAdress => $"204.{IpFirst}.{IpSecond}.{IpThird}";

    public bool NetworkEnabled = true;

    public string Prompt
    {
        get
        {
            var dir = CurrentDir;
            if (Home != "/" && (dir == Home || dir.StartsWith(Home + "/", StringComparison.Ordinal)))
                dir = "~" + dir[Home.Length..];

            var userColor = User == "root" ? "#E74856" : "#16C60C";
            var sign = User == "root" ? "#" : "$";
            return $"[color={userColor}]{FormattedMessage.EscapeText(User)}@{FormattedMessage.EscapeText(Hostname)}[/color]:[color=#3B78FF]{FormattedMessage.EscapeText(dir)}[/color]{sign} ";
        }
    }
}

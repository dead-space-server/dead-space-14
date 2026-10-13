// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{

    public static readonly CVarDef<float> CameraArchiveHistoryMinutes =
        CVarDef.Create("camera_archive.history_minutes", 10f, CVar.SERVERONLY);

    public static readonly CVarDef<float> CameraArchiveFocusSeconds =
        CVarDef.Create("camera_archive.focus_seconds", 3f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> CameraArchiveActive =
        CVarDef.Create("camera_archive.active", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> CameraArchiveIdleEnabled =
        CVarDef.Create("camera_archive.passive", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> CameraArchiveTickBudgetSeconds =
        CVarDef.Create("camera_archive.tick_budget_seconds", 0.04f, CVar.SERVERONLY);
}

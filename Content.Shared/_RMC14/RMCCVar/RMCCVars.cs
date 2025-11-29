using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.RMCCVar;

[CVarDefs]
public sealed partial class RMCCVars : CVars
{
    public static readonly CVarDef<int> RMCChatRepeatHistory =
        CVarDef.Create("rmc.chat_repeat_history", 4, CVar.REPLICATED | CVar.SERVER);
}

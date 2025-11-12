// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.DeadSpace.AntiAlcohol;

[RegisterComponent]
public sealed partial class AntiAlcoholWatcherComponent : Component
{
    [DataField] public ProtoId<ReagentPrototype> EthanolId = "Ethanol";
    [DataField] public float Threshold = 0.01f;
    [DataField] public float CooldownSeconds = 10f;
    [DataField] public float Probability = 1.0f;

    public TimeSpan NextAllowedVomitAt = TimeSpan.Zero;
}

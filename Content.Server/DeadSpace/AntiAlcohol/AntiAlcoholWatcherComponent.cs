using Robust.Shared.GameStates;
using System;
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
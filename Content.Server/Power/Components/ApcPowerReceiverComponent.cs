using Content.Server.Power.Pow3r;
using Content.Shared.Power.Components;

namespace Content.Server.Power.Components;

/// <inheritdoc />
[RegisterComponent]
public sealed partial class ApcPowerReceiverComponent : SharedApcPowerReceiverComponent
{
    /// <inheritdoc />
    public override float Load
    {
        get => NetworkLoad.DesiredPower;
        set => NetworkLoad.DesiredPower = value;
    }

    /// <inheritdoc />
    public override bool NeedsPower
    {
        get => _needsPower;
        set
        {
            // DS14-start
            if (_needsPower == value)
                return;
            // DS14-end

            _needsPower = value;
            NetworkLoad.QueueUpdate(); // DS14
        }
    }

    private bool _needsPower = true; // DS14

    /// <summary>
    /// The component currently providing this entity with power.
    /// </summary>
    public ApcPowerProviderComponent? Provider = null;

    /// <inheritdoc />
    public override bool PowerDisabled
    {
        get => !NetworkLoad.Enabled;
        set => NetworkLoad.Enabled = !value;
    }

    /// <summary>
    /// The load of the network as an object used by Pow3r.
    /// </summary>
    [ViewVariables]
    public PowerState.Load NetworkLoad { get; } = new PowerState.Load
    {
        DesiredPower = 5
    };

    /// <summary>
    /// The power currently being received, in watts.
    /// </summary>
    public float PowerReceived => NetworkLoad.ReceivingPower;
}

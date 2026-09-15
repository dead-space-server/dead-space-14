using Content.Server.Power.NodeGroups;
using Content.Server.Power.Pow3r;
using Content.Shared.Power.Components;

namespace Content.Server.Power.Components
{
    /// <summary>
    ///     Attempts to link with a nearby <see cref="ApcPowerProviderComponent"/>s
    ///     so that it can receive power from a <see cref="IApcNet"/>.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ApcPowerReceiverComponent : SharedApcPowerReceiverComponent
    {
        /// <summary>
        ///     Amount of charge this needs from an APC per second to function.
        /// </summary>
        [DataField("powerLoad")] // DS14
        public override float Load
        {
            get => NetworkLoad.DesiredPower;
            // DS14-start
            set
            {
                if (NetworkLoad.DesiredPower == value)
                    return;

                NetworkLoad.DesiredPower = value;
            }
            // DS14-end
        }

        public ApcPowerProviderComponent? Provider = null;

        /// <summary>
        ///     When false, causes this to appear powered even if not receiving power from an Apc.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)] // DS14
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

        [DataField("needsPower")] // DS14
        private bool _needsPower = true;

        /// <summary>
        ///     When true, causes this to never appear powered.
        /// </summary>
        [DataField("powerDisabled")] // DS14
        public override bool PowerDisabled
        {
            get => !NetworkLoad.Enabled;
            set => NetworkLoad.Enabled = !value;
        }

        [ViewVariables]
        public PowerState.Load NetworkLoad { get; } = new PowerState.Load
        {
            DesiredPower = 5
        };

        public float PowerReceived => NetworkLoad.ReceivingPower;
    }
}

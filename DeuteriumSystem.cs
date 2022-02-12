using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
namespace DeuteriumSystem
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    class CustomGasThrust : MyGameLogicComponent
    {
        internal MyThrust m_thrust;
        internal IMyThrust m_thrustInterface;
        internal MyResourceSinkComponent m_sink;
        internal MyDefinitionId FuelType;
        internal float MaxConsumption;
        internal float SavedStrength;
        internal float ThrustMultiplier;
        internal bool Forced = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (!FuelChange.ChangeFuel.StoredValues.ContainsKey((Entity as MyThrust).BlockDefinition.Id.SubtypeId))
                return;

            m_thrust = Entity as MyThrust;
            m_thrustInterface = Entity as IMyThrust;

            var values = FuelChange.ChangeFuel.StoredValues[m_thrust.BlockDefinition.Id.SubtypeId];
            FuelType = values.FuelType;
            MaxConsumption = values.MaxPowerConsumption / values.Efficiency / values.FuelEnergyDensity;

            m_thrustInterface.EnabledChanged += EnabledChanged;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            SinkInit();
        }

        public override void UpdateBeforeSimulation()
        {
            if (!m_thrust.IsFunctional || !m_thrust.IsPowered || m_thrust.MarkedForClose)
                return;

            if (m_sink.ResourceAvailableByType(FuelType) <= 0 && m_sink.CurrentInputByType(FuelType) <= 0)
            {
                if (m_thrustInterface.Enabled)
                {
                    m_thrustInterface.Enabled = false;
                    Forced = true;
                }
                return;
            }

            m_sink.Update();

            if (Forced)
            {
                m_thrustInterface.Enabled = true;
                Forced = false;
            }

            SavedStrength = m_thrust.CurrentStrength;
            ThrustMultiplier = m_sink.SuppliedRatioByType(FuelType);

            if (SavedStrength != 0 && ThrustMultiplier < 1)
            {
                m_thrustInterface.ThrustMultiplier = ThrustMultiplier;
                m_thrust.CurrentStrength *= ThrustMultiplier;
            }

            else m_thrustInterface.ThrustMultiplier = 1;

        }

        public override void Close()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (!FuelChange.ChangeFuel.StoredValues.ContainsKey((Entity as MyThrust).BlockDefinition.Id.SubtypeId))
                return;

            m_thrustInterface.EnabledChanged -= EnabledChanged;
        }

        private void EnabledChanged(IMyTerminalBlock block)
        {
            if (m_sink == null) return;

            m_sink.Update();

            if (m_sink.ResourceAvailableByType(FuelType) <= 0 && m_sink.CurrentInputByType(FuelType) <= 0)
            {
                m_thrustInterface.Enabled = false;
                Forced = true;
            }
        }

        private bool SinkInit()
        {
            var sinkInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = MaxConsumption,
                RequiredInputFunc = FuelRequired,
                ResourceTypeId = FuelType
            };
            
            var fakeController = new MyShipController()
            {
                SlimBlock = m_thrust.SlimBlock
            };

            m_sink = m_thrust.Components?.Get<MyResourceSinkComponent>();
            if (m_sink != null)
            {
                m_sink.AddType(ref sinkInfo);
            }
            else
            {
                m_sink = new MyResourceSinkComponent();
                m_sink.Init(MyStringHash.GetOrCompute("Thrust"), sinkInfo);
                m_thrust.Components.Add(m_sink);
            }

            var distributor = fakeController.GridResourceDistributor;
            if (distributor != null)
            {
                distributor.AddSink(m_sink);
                return true;
            }
            return false;
        }

        public float FuelRequired()
        {
            if (!m_thrust.IsWorking)
                return 0;

            if (m_thrust.ThrustOverride != 0)
                return MaxConsumption * m_thrustInterface.ThrustOverridePercentage;

            if (SavedStrength > 0)
                return MaxConsumption * SavedStrength;

            return 0;
        }
    }


}

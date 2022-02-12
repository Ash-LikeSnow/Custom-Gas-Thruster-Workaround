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
        private MyThrust _thrust;
        private IMyThrust _thrustInterface;
        private MyResourceSinkComponent _sink;
        private MyDefinitionId _fuelType;
        private float _maxConsumption;
        private float _savedStrength;
        private float _thrustMultiplier;
        private bool _forced = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (!FuelChange.ChangeFuel.StoredValues.ContainsKey((Entity as MyThrust).BlockDefinition.Id.SubtypeId))
                return;

            _thrust = Entity as MyThrust;
            _thrustInterface = Entity as IMyThrust;

            var values = FuelChange.ChangeFuel.StoredValues[_thrust.BlockDefinition.Id.SubtypeId];
            _fuelType = values.FuelType;
            _maxConsumption = values.MaxPowerConsumption / values.Efficiency / values.FuelEnergyDensity;

            _thrustInterface.EnabledChanged += EnabledChanged;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            SinkInit();
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_thrust.IsFunctional || !_thrust.IsPowered || _thrust.MarkedForClose)
                return;

            if (_sink.ResourceAvailableByType(_fuelType) <= 0 && _sink.CurrentInputByType(_fuelType) <= 0)
            {
                if (_thrustInterface.Enabled)
                {
                    _thrustInterface.Enabled = false;
                    _forced = true;
                }
                return;
            }

            _sink.Update();

            if (_forced)
            {
                _thrustInterface.Enabled = true;
                _forced = false;
            }

            _savedStrength = _thrust.CurrentStrength;
            _thrustMultiplier = _sink.SuppliedRatioByType(_fuelType);

            if (_savedStrength != 0 && _thrustMultiplier < 1)
            {
                _thrustInterface.ThrustMultiplier = _thrustMultiplier;
                _thrust.CurrentStrength *= _thrustMultiplier;
            }

            else _thrustInterface.ThrustMultiplier = 1;

        }

        public override void Close()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            if (!FuelChange.ChangeFuel.StoredValues.ContainsKey((Entity as MyThrust).BlockDefinition.Id.SubtypeId))
                return;

            _thrustInterface.EnabledChanged -= EnabledChanged;
        }

        private void EnabledChanged(IMyTerminalBlock block)
        {
            if (_sink == null) return;

            _sink.Update();

            if (_sink.ResourceAvailableByType(_fuelType) <= 0 && _sink.CurrentInputByType(_fuelType) <= 0)
            {
                _thrustInterface.Enabled = false;
                _forced = true;
            }
        }

        private bool SinkInit()
        {
            var sinkInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = _maxConsumption,
                RequiredInputFunc = FuelRequired,
                ResourceTypeId = _fuelType
            };
            
            var fakeController = new MyShipController()
            {
                SlimBlock = _thrust.SlimBlock
            };

            _sink = _thrust.Components?.Get<MyResourceSinkComponent>();
            if (_sink != null)
            {
                _sink.AddType(ref sinkInfo);
            }
            else
            {
                _sink = new MyResourceSinkComponent();
                _sink.Init(MyStringHash.GetOrCompute("Thrust"), sinkInfo);
                _thrust.Components.Add(_sink);
            }

            var distributor = fakeController.GridResourceDistributor;
            if (distributor != null)
            {
                distributor.AddSink(_sink);
                return true;
            }
            return false;
        }

        public float FuelRequired()
        {
            if (!_thrust.IsWorking)
                return 0;

            if (_thrust.ThrustOverride != 0)
                return _maxConsumption * _thrustInterface.ThrustOverridePercentage;

            if (_savedStrength > 0)
                return _maxConsumption * _savedStrength;

            return 0;
        }
    }


}

using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace FuelChange
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class ChangeFuel : MySessionComponentBase
    {
        internal static Dictionary<MyStringHash, DefinitionValues> StoredValues = new Dictionary<MyStringHash, DefinitionValues>();

        internal int Count = 0;

        public override void LoadData()
        {
            foreach (var def in MyDefinitionManager.Static?.GetAllDefinitions())
            {
                var thrustDef = def as MyThrustDefinition;
                if (thrustDef != null && !thrustDef.FuelConverter.FuelId.IsNull() && thrustDef.FuelConverter.FuelId != MyResourceDistributorComponent.HydrogenId && thrustDef.FuelConverter.FuelId != MyResourceDistributorComponent.ElectricityId)
                {
                    var fuelDef = MyDefinitionManager.Static.GetDefinition(thrustDef.FuelConverter.FuelId) as MyGasProperties;
                    if (fuelDef == null) continue;

                    var values = new DefinitionValues()
                    {
                        FuelType = thrustDef.FuelConverter.FuelId,
                        FuelEnergyDensity = fuelDef.EnergyDensity,
                        Efficiency = thrustDef.FuelConverter.Efficiency,
                        MaxPowerConsumption = thrustDef.MaxPowerConsumption
                    };
                    StoredValues.Add(thrustDef.Id.SubtypeId, values);

                    thrustDef.FuelConverter = new MyFuelConverterInfo() { Efficiency = 1 };
                    thrustDef.MaxPowerConsumption = 0.0001f;

                    Count++;
                }
            }
            MyLog.Default.WriteLine($"[ThrusterFix] Modified definitions for {Count} custom gas thrusters.");
        }

        protected override void UnloadData()
        {
            StoredValues = null;
        }

        internal struct DefinitionValues
        {
            internal MyDefinitionId FuelType;
            internal float FuelEnergyDensity;
            internal float Efficiency;
            internal float MaxPowerConsumption;
        }
    }

}
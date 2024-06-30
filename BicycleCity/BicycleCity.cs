using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;

namespace BicycleCity
{
    public class BicycleCity : Script
    {
        private readonly int bikesPercentage;
        private readonly bool aggressiveDrivers;
        private readonly bool aggressiveCyclists;
        private readonly bool cyclistsBreakLaws;
        private readonly bool cheeringCrowds;
        private readonly bool cantFallFromBike;
        private int lastTime = Environment.TickCount;
        private readonly List<Ped> fans = new List<Ped>();
        private readonly string[] availableBicycles = { "BMX", "CRUISER", "FIXTER", "SCORCHER", "TRIBIKE", "TRIBIKE2", "TRIBIKE3" };

        public PedestrianDefinitions.PedestrianModel[] CheeringPeds => PedestrianDefinitions.PedModels;

        public BicycleCity()
        {
            ScriptSettings settings = ScriptSettings.Load(@".\Scripts\BicycleCity.ini");
            bikesPercentage = Clamp(settings.GetValue("Main", "BikesPercentage", 0), 0, 100);
            aggressiveDrivers = settings.GetValue("Main", "AggressiveDrivers", false);
            aggressiveCyclists = settings.GetValue("Main", "AggressiveCyclists", false);
            cyclistsBreakLaws = settings.GetValue("Main", "CyclistsBreakLaws", false);
            cheeringCrowds = settings.GetValue("Main", "CheeringCrowds", true);
            cantFallFromBike = settings.GetValue("Main", "CantFallFromBike", true);
            Tick += OnTick;
            Aborted += OnAbort;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Environment.TickCount >= lastTime + 1000)
            {
                try
                {
                    SpawnBicycles();
                    SpawnCheeringCrowds();
                    RemoveFans();
                }
                catch (Exception)
                {
                    // Handle or log exceptions here if needed
                }

                lastTime = Environment.TickCount;
            }

            UpdateFanFacing();

            if (cantFallFromBike)
                Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, Game.Player.Character, 1);
        }

        private void SpawnBicycles()
        {
            List<Vehicle> canChange = new List<Vehicle>();
            int bicycles = 0;

            foreach (Vehicle vehicle in World.GetAllVehicles())
            {
                if (vehicle.Driver != null && vehicle.Driver.IsPlayer)
                    continue;

                if (vehicle.Model.IsBicycle)
                    bicycles++;
                else if (!IsSpecialVehicle(vehicle))
                {
                    canChange.Add(vehicle);

                    if (aggressiveDrivers && vehicle.Driver != null)
                        Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, vehicle.Driver, (int)AggressiveDrivingStyle());
                }
            }

            Random random = new Random();
            int toChange = (bicycles + canChange.Count) * bikesPercentage / 100 - bicycles;

            for (int i = 0; i < Math.Min(toChange, canChange.Count); i++)
            {
                Vehicle vehicle = canChange[i];
                Ped driver = vehicle.Driver;

                if (vehicle.IsInRange(Game.Player.Character.Position, 100f) && vehicle.IsOnScreen)
                    continue;

                if (driver != null)
                {
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
                    driver.AlwaysKeepTask = false;
                }

                Model newModel = new Model(availableBicycles[random.Next(availableBicycles.Length)]);
                newModel.Request();

                if (newModel.IsInCdImage && newModel.IsValid)
                {
                    while (!newModel.IsLoaded)
                        Wait(10);

                    Vector3 newPosition = vehicle.Position;
                    float newHeading = vehicle.Heading;
                    Vehicle newVehicle = World.CreateVehicle(newModel, newPosition, newHeading);

                    newModel.MarkAsNoLongerNeeded();
                    newVehicle.Mods.CustomPrimaryColor = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255));
                    newVehicle.MaxSpeed = 10;

                    vehicle.Delete();

                    if (driver != null)
                    {
                        driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
                        int drivingStyle = cyclistsBreakLaws ? (int)LawBreakerDrivingStyle() : aggressiveCyclists ? (int)AggressiveDrivingStyle() : 0;
                        Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, (float)random.Next(4, 8), drivingStyle);
                        Function.Call(Hash.SET_PED_KEEP_TASK, driver, true);
                        driver.MarkAsNoLongerNeeded();
                    }

                    newVehicle.MarkAsNoLongerNeeded();
                }
            }

            canChange.Clear();
        }

        private void SpawnCheeringCrowds()
        {
            if (cheeringCrowds && new Random().NextDouble() < 0.3) // Increased probability to 30%
            {
                Vector3 spawnPoint = GetRandomSpawnPointInFront(Game.Player.Character.Position, Game.Player.Character.Heading, 30f, 50f);

                if (spawnPoint != Vector3.Zero)
                {
                    int crowdSize = new Random().Next(1, 16); // Random crowd size between 1 to 15 people

                    for (int j = 0; j < crowdSize; j++)
                    {
                        PedestrianDefinitions.PedestrianModel randomCheeringPed = CheeringPeds[new Random().Next(CheeringPeds.Length)];
                        Model pModel = new Model(randomCheeringPed.Model);
                        pModel.Request();

                        if (pModel.IsInCdImage && pModel.IsValid)
                        {
                            while (!pModel.IsLoaded)
                                Wait(10);

                            Vector3 randomOffset = new Vector3((float)(new Random().NextDouble() * 10 - 5), (float)(new Random().NextDouble() * 10 - 5), 0f);
                            Ped fan = World.CreatePed(pModel, spawnPoint + randomOffset);

                            pModel.MarkAsNoLongerNeeded();
                            fan.Task.StartScenario(randomCheeringPed.Scenario, 0f);
                            fans.Add(fan);
                        }
                    }
                }
            }
        }

        private void RemoveFans()
        {
            foreach (Ped fan in fans.ToArray())
            {
                if (fan != null)
                {
                    if (fan.Position.DistanceTo(Game.Player.Character.Position) > 300f || IsEnemy(fan))
                    {
                        fan.Delete();
                        fans.Remove(fan);
                    }
                }
                else
                {
                    fans.Remove(fan);
                }
            }
        }

        private void UpdateFanFacing()
        {
            if (cheeringCrowds)
            {
                foreach (Ped fan in fans)
                {
                    if (fan != null && !fan.IsRunning)
                    {
                        fan.Heading = (Game.Player.Character.Position - fan.Position).ToHeading();
                    }
                }
            }
        }

        private Vector3 GetRandomSpawnPointInFront(Vector3 position, float heading, float minDistance, float maxDistance)
        {
            Random random = new Random();
            float angle = (float)(random.NextDouble() * Math.PI - Math.PI / 2); // Random angle between -90 to +90 degrees
            float distance = (float)(minDistance + random.NextDouble() * (maxDistance - minDistance)); // Random distance between minDistance and maxDistance

            Vector3 offset = new Vector3(
                (float)(Math.Cos(heading + angle) * distance),
                (float)(Math.Sin(heading + angle) * distance),
                0);

            return position + offset;
        }

        private bool IsEnemy(Ped ped)
        {
            return (ped.GetRelationshipWithPed(Game.Player.Character) == Relationship.Hate && ped.IsHuman) || ped.IsInCombat || ped.IsInMeleeCombat || ped.IsShooting;
        }

        private void OnAbort(object sender, EventArgs e)
        {
            Tick -= OnTick;

            if (cheeringCrowds)
            {
                foreach (Ped fan in fans)
                {
                    fan.Delete();
                }
                fans.Clear();
            }
        }

        private int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private VehicleDrivingFlags AggressiveDrivingStyle()
        {
            return VehicleDrivingFlags.SteerAroundStationaryVehicles |
                   VehicleDrivingFlags.SteerAroundObjects |
                   VehicleDrivingFlags.SteerAroundPeds |
                   VehicleDrivingFlags.SwerveAroundAllVehicles |
                   VehicleDrivingFlags.StopAtTrafficLights;
        }

        private VehicleDrivingFlags LawBreakerDrivingStyle()
        {
            return VehicleDrivingFlags.AllowGoingWrongWay |
                   VehicleDrivingFlags.UseShortCutLinks |
                   VehicleDrivingFlags.SteerAroundStationaryVehicles |
                   VehicleDrivingFlags.SteerAroundObjects |
                   VehicleDrivingFlags.SwerveAroundAllVehicles;
        }

        private bool IsSpecialVehicle(Vehicle vehicle)
        {
            return vehicle.Model.IsTrain || vehicle.Model.IsBoat || vehicle.Model.IsHelicopter || vehicle.Model.IsPlane ||
                   Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, vehicle);
        }
    }
}

using DV.Simulation.Brake;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

/*
	Auxillary Air Generator Modification for Derail Valley
	Copyright [2022] [Crypto [Neo]]

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

	    http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
 */

namespace DVAirGenerator
{
	[EnableReloading]
	public class AirGenerator
	{
		public static bool enabled;
		public static UnityModManager.ModEntry mod;		
		public static float maxCompressorRate = 1.50f;
		public static float targetSpeed = 30.0f;

		static void Load(UnityModManager.ModEntry modEntry)
		{
			Harmony harmony = new Harmony(modEntry.Info.Id);
			mod = modEntry;

			// Patch for updating animations on the air generator and updating the compressorProductionRate (CPR)
			// if the airbrake mod isn't installed
			harmony.Patch(
				original: AccessTools.Method(typeof(TrainCar), "Update"),
				postfix: new HarmonyMethod(typeof(Patches), "Update")
				);

			// Airbrake mod compatibilty, it updates compressor in SimulateEngineRPM, so if we modify CPR in SimulateTick
			// we're 'guaranteed' to play nice with it :)
			harmony.Patch(
					original: AccessTools.Method(typeof(ShunterLocoSimulation), "SimulateTick"),
					postfix: new HarmonyMethod(typeof(Patches), "SimulateTick"));
			harmony.Patch(
					original: AccessTools.Method(typeof(DieselLocoSimulation), "SimulateTick"),
					postfix: new HarmonyMethod(typeof(Patches), "SimulateTick"));
			harmony.Patch(
					original: AccessTools.Method(typeof(SteamLocoSimulation), "SimulateTick"),
					postfix: new HarmonyMethod(typeof(Patches), "SimulateTick"));

			// Bonk
			modEntry.OnToggle = OnToggle;
		}


		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
		{
			enabled = !enabled;
			return true;
		}

		public static class Patches
		{
			[HarmonyPostfix]
			static void SimulateTick(ShunterLocoSimulation __instance)
			{
				if (!enabled) return;

				// Get the traincar object from the instance
				TrainCar tc = __instance.gameObject.GetComponent<TrainCar>();

				// Make sure this locomotive has cars
				if (tc.trainset.cars.Count == 1) return;

				// Get the forward speed, convert to Kmh (I think)
				float speed = Mathf.Abs(tc.GetForwardSpeed()) * Units.Ms2Kmh;
				
				// Loop through all the cars, save a list of locomotive BrakeSystem
				// objects, and count how many air generators are attached to the chain
				List<BrakeSystem> locos = new List<BrakeSystem>();
				int airCarCount = 0;
				foreach (TrainCar car in tc.trainset.cars) {
					if (car.IsLoco)
						locos.Add(car.brakeSystem);
					if (car.name.Contains("Air Generator"))
						airCarCount++;
				}

				// Calculate the compressor boost
				float compressorBoost = Mathf.Clamp((maxCompressorRate / locos.Count * airCarCount) * (speed / targetSpeed), 0.0f, maxCompressorRate); ;

				// Loop through each BrakeSystem object and add in our boost 
				foreach (BrakeSystem bs in locos)
					bs.compressorProductionRate += compressorBoost;
			}

			[HarmonyPostfix]
			static void Update(TrainCar __instance)
			{
				// Check to see if this is an Air Generator and update the animation speed
				// and compressor production rate
				if (__instance.gameObject.name.Contains("Air Generator") && enabled)
				{
					// Get the current speed of the air generator car
					float speed = Mathf.Abs(__instance.GetForwardSpeed()) * Units.Ms2Kmh;

					// Go through each animator an update the speed of the moving parts
					Animator[] anims = __instance.gameObject.GetComponentsInChildren<Animator>();
					foreach (Animator a in anims)
						a.SetFloat("Speed", Mathf.Clamp(speed / 45.0f, 0.0f, 2.0f));
				}
			}
		}
	}
}
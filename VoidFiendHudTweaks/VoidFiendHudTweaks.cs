using BepInEx;
using BepInEx.Configuration;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using IL.RoR2.UI;
using On.RoR2.UI;
using System;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace VoidFiendHudTweaks
{
	// R2API dependency
    [BepInDependency(R2API.R2API.PluginGUID)]

    // Add Risk of Options dependency
    [BepInDependency("com.rune580.riskofoptions")]


    // This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

	// This is the main declaration of our plugin class.
	// BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
	// BaseUnityPlugin itself inherits from MonoBehaviour,
	// so you can use this as a reference for what you can declare and use in your plugin class
	// More information in the Unity Docs: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
	public class VoidFiendHudTweaks : BaseUnityPlugin
	{
		// The Plugin GUID should be a unique ID for this plugin,
		// which is human readable (as it is used in places like the config).
		// If we see this PluginGUID as it is on thunderstore,
		// we will deprecate this mod.
		// Change the PluginAuthor and the PluginName !
		public const string PluginGUID = PluginAuthor + "." + PluginName;
		public const string PluginAuthor = "fiend";
		public const string PluginName = "Void-Fiend-UI-Tweak";
		public const string PluginVersion = "1.0.0";
		
		public static ConfigEntry<bool> ConfigCorruptionPercentageTweak { get; set; }
		// TODO: Implement these features
        public static ConfigEntry<bool> ConfigCorruptionTimer { get; set; }
        public static ConfigEntry<bool> ConfigCorruptionDelta { get; set; }



        // The Awake() method is run at the very start when the game is initialized.
        public void Awake()
		{
			// Init our logging class so that we can properly log for debugging
			Log.Init(Logger);

			ConfigCorruptionPercentageTweak = Config.Bind<bool>(
				"UI Tweaks",
				"Always decrement corruption to 0%",
				true,
				"When in corrupted mode, always decrement the hud percentage from 100% to 0% regardless of increased minimum." +
				"\nDirect changes to the percentage, such as the \"Corrupted Supress\" ability will appear to change the percentage more than they actually do!" +
				"\nCurrently requires a game restart to change"
				);
			ModSettingsManager.AddOption(new CheckBoxOption(ConfigCorruptionPercentageTweak));

			ModSettingsManager.SetModDescription("Tweak various things about the Void Fiend corruption hud to fit your liking");


			// (fiend) new code, just adding on top for now.
			IL.RoR2.VoidSurvivorController.UpdateUI += (il) =>
			{
				if (ConfigCorruptionPercentageTweak.Value == true)
				{
					ILCursor cursor = new ILCursor(il);
					cursor.GotoNext(
						x => x.MatchLdarg(0)
					//x => x.MatchLdfld<RoR2.UI.ImageFillController>("fillUi in fillUiList"),
					//x => x.MatchCallvirt<RoR2.UI.ImageFillController>("GetEnumerator"),
					//x => x.MatchStloc(0)
					);
					cursor.Index += 11;

					cursor.Emit(OpCodes.Ldarg_0);

					// In order to get the original 0-100%, we have to convert it by assuming that the current corruption percentage is
					// (corruption - minCorruption) / (maxCorruption - minCorruption)
					// While technically, the game always uses 0-100%, if for example, minCorruption was 30%, then its technically a 0-70% scale.
					// So from there we can get the percentage along that 0-70% scale and that is our 0-100% scale percentage.
					// Example:
					// corruption = 90, minCorruption = 30, maxCorruption = 100
					// (corruption - minCorruption) [60] / (maxCorruption - minCorruption) [70] = 0.8571428571
					cursor.EmitDelegate<Func<float, float, VoidSurvivorController, float>>((corruption, maxCorruption, currController) =>
					{
						if (currController.isCorrupted == true)
						{
							var minCorruption = currController.minimumCorruption;
							return (corruption - minCorruption) / (maxCorruption - minCorruption);
						}
						else
						{
							return corruption / maxCorruption;
						}
					});
					cursor.Next.Operand = null;
					cursor.Next.OpCode = OpCodes.Nop;

					// Maybe a bit too loose?
					cursor.GotoNext(
						//x => x.MatchCallOrCallvirt("HG.StringBuilderPool","RentStringBuilder"),
						x => x.MatchStloc(1),
						x => x.MatchLdloc(1),
						x => x.MatchLdarg(0)
						//x => x.MatchCallOrCallvirt<VoidSurvivorController>("get_controller")
						//x => x.MatchCallOrCallvirt<Mathf>("FloorToInt")
					);

					cursor.Index += 4;

					cursor.Emit(OpCodes.Ldarg_0);
					cursor.EmitDelegate<Func<float, VoidSurvivorController, float>>((corruption, currController) =>
					{
						if (currController.isCorrupted == true)
						{
							var minCorruption = currController.minimumCorruption;
							return ((corruption - minCorruption) / (currController.maxCorruption - minCorruption)) * 100;
						}
						else
						{
							return corruption;
						}
					});
#if DEBUG
					Debug.Log(il.ToString());
#endif
				}
			};
		}
	}
}

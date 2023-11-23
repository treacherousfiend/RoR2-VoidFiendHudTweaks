using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using RoR2.HudOverlay;
using RoR2.UI;
using UnityEngine.UI;
using TMPro;
using System.Text;

namespace VoidFiendHudTweaks
{
	// R2API dependency
	[BepInDependency(R2API.R2API.PluginGUID)]

	// Add Risk of Options dependency
	[BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]

	// This attribute is required, and lists metadata for your plugin.
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]

	// This is the main declaration of our plugin class.
	// BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
	// BaseUnityPlugin itself inherits from MonoBehaviour,
	// so you can use this as a reference for what you can declare and use in your plugin class
	// More information in the Unity Docs: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
	public class VoidFiendHudTweaks : BaseUnityPlugin
	{
		public const string PluginGUID = PluginAuthor + "." + PluginName;
		public const string PluginAuthor = "fiend";
		public const string PluginName = "Void-Fiend-UI-Tweak";
		public const string PluginVersion = "1.0.0";
		
		public static ConfigEntry<bool> ConfigCorruptionPercentageTweak { get; set; }
		
		// TODO: Implement these features
		public static ConfigEntry<bool> ConfigCorruptionTimer { get; set; }
		
		public static ConfigEntry<bool> ConfigCorruptionDelta { get; set; }
		
		public static ConfigEntry<bool> ConfigSuppressHealDelta { get; set; }
		public static ConfigEntry<Color> ConfigSuppressHealDeltaColor { get; set; }
		
		public static ConfigEntry<bool> ConfigSuppressCorruptDelta { get; set; }
		public static ConfigEntry<Color> ConfigSuppressCorruptDeltaColor { get; set; }

		public static Image SuppressHealDeltaImageObject = null;
		public static Image SuppressCorruptDeltaImageObject = null;

		public void OnEnable()
		{
			On.RoR2.VoidSurvivorController.OnEnable += VoidSurvivorController_OnEnable;
			On.RoR2.VoidSurvivorController.OnDisable += VoidSurvivorController_OnDisable;
			On.RoR2.VoidSurvivorController.UpdateUI += CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded += VoidSurvivorController_OnOverlayInstanceAdded;
		}

		public void OnDisable()
		{
			On.RoR2.VoidSurvivorController.OnEnable -= VoidSurvivorController_OnEnable;
			On.RoR2.VoidSurvivorController.OnDisable -= VoidSurvivorController_OnDisable;
			On.RoR2.VoidSurvivorController.UpdateUI -= CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded -= VoidSurvivorController_OnOverlayInstanceAdded;
		}

		private void CustomUpdateUI(On.RoR2.VoidSurvivorController.orig_UpdateUI orig, VoidSurvivorController self)
		{
			orig(self);

			ImageFillController baseFillUi = null;

			// For now, just pick the last one in the list.
			// In normal gameplay this should only have 1 value, but mods may change this, so for compat this should be more indepth. 
			foreach (ImageFillController fillUi in self.fillUiList)
			{
				baseFillUi = fillUi;
			}

			// Workaround because UpdateUI is run once before OnOverlayInstanceAdded
			if (self.fillUiList.Count > 0)
			{
				float CorruptionPercentage = self.corruption / self.maxCorruption;
				float SuppressHealDelta = 0f;

				if (self.isCorrupted == false)
				{
					if (ConfigSuppressHealDelta.Value)
					{
						if (CorruptionPercentage >= 0.25f && CorruptionPercentage < 1f)
						{
							SuppressHealDelta = CorruptionPercentage;
							CorruptionPercentage = Mathf.Clamp(CorruptionPercentage, self.minimumCorruption, CorruptionPercentage - 0.25f);
						}
						else
						{
							SuppressHealDeltaImageObject.fillAmount = 0f;
						}
					}

					baseFillUi.SetTValue(CorruptionPercentage);
					SuppressHealDeltaImageObject.fillAmount = SuppressHealDelta;
					SuppressCorruptDeltaImageObject.fillAmount = 0f;

				}
				else
				{
					float CorruptionDelta = 0f;

					if (ConfigCorruptionPercentageTweak.Value)
					{
						// In order to get the original 0-100%, we have to convert it by assuming that the current corruption percentage is
						// (corruption - minCorruption) / (maxCorruption - minCorruption)
						// While technically, the game always uses 0-100%, if for example, minCorruption was 30%, then its technically a 0-70% scale.
						// So from there we can get the percentage along that 0-70% scale and that is our 0-100% scale percentage.
						// Example:
						// corruption = 90, minCorruption = 30, maxCorruption = 100
						// (corruption - minCorruption) [60] / (maxCorruption - minCorruption) [70] = 0.8571428571
						CorruptionPercentage = (self.corruption - self.minimumCorruption) / (self.maxCorruption - self.minimumCorruption);

						StringBuilder stringBuilder = HG.StringBuilderPool.RentStringBuilder();
						stringBuilder.AppendInt(Mathf.FloorToInt(CorruptionPercentage * 100), 1u, 3u).Append("%");
						self.uiCorruptionText.GetComponentInChildren<TextMeshProUGUI>().SetText(stringBuilder);
						HG.StringBuilderPool.ReturnStringBuilder(stringBuilder);

						if (ConfigSuppressCorruptDelta.Value)
						{
							CorruptionDelta = Mathf.Min(1f, CorruptionPercentage + (25f / (self.maxCorruption - self.minimumCorruption)));
						}
						
					}
					else if(ConfigSuppressCorruptDelta.Value)
					{
						CorruptionDelta = Mathf.Min(1f, CorruptionPercentage + 0.25f);
					};

					baseFillUi.SetTValue(CorruptionPercentage);
					SuppressHealDeltaImageObject.fillAmount = 0f;
					SuppressCorruptDeltaImageObject.fillAmount = CorruptionDelta;
				}
			}
		}

		private void RecursiveFindGameObjectChildren(Transform gameObjectTransform, int indentCount)
		{
			foreach (Transform childTransform in gameObjectTransform)
			{
				int i;
				string DebugString = "";
				for (i = 0; i <= indentCount; i++)
				{
					DebugString += "\t";
				}

				Debug.Log(DebugString + childTransform.gameObject);

				RecursiveFindGameObjectChildren(childTransform.transform, i + 1);
			}
		}

		private void VoidSurvivorController_OnOverlayInstanceAdded(On.RoR2.VoidSurvivorController.orig_OnOverlayInstanceAdded orig, VoidSurvivorController self, OverlayController controller, GameObject instance)
		{
			GameObject FillObject = null;

			// Someone please kill me for this code
			foreach (Transform childTransform in instance.transform)
			{
				if (childTransform.gameObject.name == "FillRoot")
				{
					foreach (Transform childTransform2 in childTransform)
					{
						if (childTransform2.gameObject.name == "Fill")
						{
							foreach (Transform childTransform3 in childTransform2)
							{
								if (childTransform3.gameObject.name == "Fill")
								{
									FillObject = childTransform3.gameObject;
								}
							}
						}
					}
				}
			}
			if (FillObject != null)
			{
				GameObject SuppressHealDeltaObject = Instantiate(FillObject);
				SuppressHealDeltaObject.name = "FillSuppressHealDelta";
				SuppressHealDeltaImageObject = SuppressHealDeltaObject.GetComponent<UnityEngine.UI.Image>();
				SuppressHealDeltaImageObject.color = ConfigSuppressHealDeltaColor.Value;
				SuppressHealDeltaObject.transform.SetParent(FillObject.transform.parent, false);
				SuppressHealDeltaObject.transform.SetSiblingIndex(1);

				GameObject SuppressCorruptDeltaObject = Instantiate(FillObject);
				SuppressCorruptDeltaObject.name = "FillSuppressCorruptDelta";
				SuppressCorruptDeltaImageObject = SuppressCorruptDeltaObject.GetComponent<UnityEngine.UI.Image>();
				SuppressCorruptDeltaImageObject.color = ConfigSuppressCorruptDeltaColor.Value;
				SuppressCorruptDeltaObject.transform.SetParent(FillObject.transform.parent, false);
				SuppressCorruptDeltaObject.transform.SetSiblingIndex(2);
			}
			

			orig(self, controller, instance);
		}

		private void VoidSurvivorController_OnEnable(On.RoR2.VoidSurvivorController.orig_OnEnable orig, VoidSurvivorController self)
		{
			orig(self);
		}

		private void VoidSurvivorController_OnDisable(On.RoR2.VoidSurvivorController.orig_OnDisable orig, VoidSurvivorController self)
		{
			orig(self);
		}

		// The Awake() method is run at the very start when the game is initialized.
		public void Awake()
		{
			// Init our logging class so that we can properly log for debugging
			Log.Init(Logger);

			ConfigCorruptionPercentageTweak = Config.Bind(
				"UI Tweaks",
				"Always decrement corruption to 0%",
				true,
				"When in corrupted mode, always decrement the hud percentage from 100% to 0% regardless of increased minimum." +
				"\n\nDirect changes to the percentage, such as the \"Corrupted Suppress\" ability will appear to change the percentage more than they actually do!" +
				"\nI recommend using \"Show Suppress Corruption Delta\" with this option to make these changes a bit clearer" +
				"\n\nDefault: True"
				);

			ConfigSuppressHealDelta = Config.Bind(
				"UI Tweaks",
				"Show Suppress Heal Delta",
				false,
				"Show on the corruption meter how much will be removed by Suppress" +
				"\n\nDefault: False"
				);

			ConfigSuppressHealDeltaColor = Config.Bind(
				"UI Tweaks",
				"Suppress Heal Delta Color",
				new Color(0.58984375f, 0.265625f, 0.375f, 1f),
				"The color that will be used if \"Show Suppress Heal Delta\" is enabled" +
				"\n\nDefault: 964460FF"
				);

			ConfigSuppressCorruptDelta = Config.Bind(
				"UI Tweaks",
				"Show Suppress Corruption Delta",
				false,
				"Show on the corruption meter how much will be added by Corrupted Suppress" +
				"\n\nDefault: False"
				);

			ConfigSuppressCorruptDeltaColor = Config.Bind(
				"UI Tweaks",
				"Suppress Corruption Delta Color",
				new Color(0.6953125f, 0.15234375f, 0.1796875f, 1f),
				"The color that will be used if \"Show Suppress Corruption Delta\" is enabled" +
				"\n\nDefault: B2272EFF"
				);

			// Only do this is RiskOfOptions is installed!
			if (RiskOfOptionsCompatibility.Enabled)
			{
				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigCorruptionPercentageTweak);
				
				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigSuppressHealDelta);
				RiskOfOptionsCompatibility.AddColorOption(ConfigSuppressHealDeltaColor, delegate () { return !ConfigSuppressHealDelta.Value; } );

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigSuppressCorruptDelta);
				RiskOfOptionsCompatibility.AddColorOption(ConfigSuppressCorruptDeltaColor, delegate () { return !ConfigSuppressCorruptDelta.Value; });

				RiskOfOptionsCompatibility.InvokeSetModDescription("Tweak various things about the Void Fiend corruption hud to fit your liking");
			}
		}
	}
}

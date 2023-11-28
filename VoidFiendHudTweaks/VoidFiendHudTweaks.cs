using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.HudOverlay;
using RoR2.Skills;
using RoR2.UI;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace VoidFiendHudTweaks
{
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
		public const string PluginAuthor = "treacherousfiend";
		public const string PluginName = "Void-Fiend-UI-Tweak";
		public const string PluginVersion = "1.0.0";

		public static ConfigEntry<bool> ConfigCorruptionPercentageTweak { get; set; }

		// TODO: Implement these features
		public static ConfigEntry<bool> ConfigCorruptionTimer { get; set; }

		public static ConfigEntry<bool> ConfigCorruptionDelta { get; set; }
		public static GameObject CorruptionDeltaNumber = null;
		public static ConfigEntry<Color> ConfigCorruptionDeltaPositiveColor { get; set; }
		public static ConfigEntry<Color> ConfigCorruptionDeltaNegativeColor { get; set; }

		public static float CorruptionDeltaElementTimer = 0f;

		public static ConfigEntry<bool> ConfigSuppressHealDelta { get; set; }
		public static ConfigEntry<Color> ConfigSuppressHealDeltaColor { get; set; }

		public static ConfigEntry<bool> ConfigSuppressCorruptDelta { get; set; }
		public static ConfigEntry<Color> ConfigSuppressCorruptDeltaColor { get; set; }

		public static Image SuppressDeltaImageObject = null;
		public static Image SuppressCorruptDeltaImageObject = null;

		// In case mods or DLC adds alternate special skills for Void Fiend, add the regular and corrupted versions to these dictionaries.
		// Need to add the SkillDef and a dictionary of all of the serializedfields from the entity state.
		// This mod has a helper function called "GetSerializedValues" which will generate this dictionary.
		// The actual meter deltas are calculated using the corruptionChange value of the ability, so its all done for you :)
		public static Dictionary<SkillDef, Dictionary<string, string>> SpecialSkillDefs = new Dictionary<SkillDef, Dictionary<string, string>>();
		public static Dictionary<SkillDef, Dictionary<string, string>> CorruptedSpecialSkillDefs = new Dictionary<SkillDef, Dictionary<string, string>>();

		public static ConfigEntry<bool> ConfigShowPermaCorruptionMin { get; set; }

		public void OnEnable()
		{
			On.RoR2.VoidSurvivorController.UpdateUI += CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded += VoidSurvivorController_OnOverlayInstanceAdded;
			On.RoR2.VoidSurvivorController.AddCorruption += VoidSurvivorController_AddCorruption;
		}

		public void OnDisable()
		{
			On.RoR2.VoidSurvivorController.UpdateUI -= CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded -= VoidSurvivorController_OnOverlayInstanceAdded;
			On.RoR2.VoidSurvivorController.AddCorruption -= VoidSurvivorController_AddCorruption;
		}

		private float GenerateMeterDelta(Dictionary<SkillDef, Dictionary<string, string>> skillDefDict, SkillDef currSpecialSkill, VoidSurvivorController survivorController, ImageFillController baseFillUi)
		{
			float CorruptionFraction = survivorController.corruption / survivorController.maxCorruption;
			float TextCorruptionPercentage = CorruptionFraction * 100f;
			float CorruptionDelta = 0f;

			float CorruptionChange = float.Parse(skillDefDict[currSpecialSkill]["corruptionChange"]) / 100f;

			// Only show the delta meter if we have more than 0 stock (if the ability doesn't recharge), or if the ability recharges
			// TODO: only hide once the ability actually changes the value.
			if (survivorController.characterBody.skillLocator.special.stock > 0 || currSpecialSkill.rechargeStock > 0)
			{
				if (CorruptionChange < 0f && ConfigSuppressHealDelta.Value)
				{
					// separate if statement because i'm bad at coding.
					if (CorruptionFraction >= CorruptionChange && CorruptionFraction < 1f)
					{
						SuppressDeltaImageObject.color = ConfigSuppressHealDeltaColor.Value;
						CorruptionDelta = CorruptionFraction;
						// Only show delta if we're above the minimum needed to use the ability.
						// TODO: the minimumCorruption needed for an ability is different than how much is changed
						// some mods might use this, so it should be supported!
						if (CorruptionFraction >= Mathf.Abs(CorruptionChange))
						{
							CorruptionFraction = Mathf.Max(survivorController.minimumCorruption / 100, CorruptionFraction + CorruptionChange);
						}
					}
				}
				else
				{
					if (ConfigCorruptionPercentageTweak.Value)
					{
						float adjustedMaxCorruption = survivorController.maxCorruption - survivorController.minimumCorruption;

						// In order to get the original 0-100%, we have to convert it by assuming that the current corruption percentage is
						// (corruption - minCorruption) / (maxCorruption - minCorruption)
						// While technically, the game always uses 0-100%, if for example, minCorruption was 30%, then its technically a 0-70% scale.
						// So from there we can get the percentage along that 0-70% scale and that is our 0-100% scale percentage.
						// Example:
						// corruption = 90, minCorruption = 30, maxCorruption = 100
						// (corruption - minCorruption) [60] / (maxCorruption - minCorruption) [70] = 0.8571428571
						CorruptionFraction = (survivorController.corruption - survivorController.minimumCorruption) / adjustedMaxCorruption;
						TextCorruptionPercentage = CorruptionFraction * 100f;

						if (ConfigSuppressCorruptDelta.Value)
						{
							SuppressDeltaImageObject.color = ConfigSuppressCorruptDeltaColor.Value;
							CorruptionDelta = Mathf.Min(1f, CorruptionFraction + ((CorruptionChange * 100f) / adjustedMaxCorruption));
						}
					}
					else if (ConfigSuppressCorruptDelta.Value)
					{
						SuppressDeltaImageObject.color = ConfigSuppressCorruptDeltaColor.Value;
						CorruptionDelta = Mathf.Min(1f, CorruptionFraction + CorruptionChange);
					};
				}
			}
			else
			{
				// Needed since we just completely skip the code normally once the player is out of their special ability charges
				// there HAS to be a better way than this, but i'm not sure how.
				if (ConfigCorruptionPercentageTweak.Value)
				{
					float adjustedMaxCorruption = survivorController.maxCorruption - survivorController.minimumCorruption;

					CorruptionFraction = (survivorController.corruption - survivorController.minimumCorruption) / adjustedMaxCorruption;
					TextCorruptionPercentage = CorruptionFraction * 100f;
				}
			}
			baseFillUi.SetTValue(CorruptionFraction);
			SuppressDeltaImageObject.fillAmount = CorruptionDelta;
			return TextCorruptionPercentage;
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
				SkillDef CurrentSpecialSkill = self.characterBody.skillLocator.special.skillDef;
				float CorruptionPercentage = self.corruption; // in case we don't change this value, just use the default

				if (!self.isPermanentlyCorrupted)
				{
					if (!self.isCorrupted && SpecialSkillDefs.ContainsKey(CurrentSpecialSkill))
					{
						CorruptionPercentage = GenerateMeterDelta(SpecialSkillDefs, CurrentSpecialSkill, self, baseFillUi);
					}
					else if (CorruptedSpecialSkillDefs.ContainsKey(CurrentSpecialSkill))
					{
						CorruptionPercentage = GenerateMeterDelta(CorruptedSpecialSkillDefs, CurrentSpecialSkill, self, baseFillUi);
					}
				}
				else if (!ConfigShowPermaCorruptionMin.Value)
				{
					CorruptionPercentage = self.minimumCorruption;
				}
				else
				{
					// Clamp CorruptionPercentage to 100% to fix a vanilla bug which caused the percentage to flicker when minCorruption was over 100%
					CorruptionPercentage = 100f;
				}

				// Create the visible percentage string
				StringBuilder stringBuilder = HG.StringBuilderPool.RentStringBuilder();
				stringBuilder.AppendInt(Mathf.FloorToInt(CorruptionPercentage), 1u, 3u).Append("%");
				self.uiCorruptionText.GetComponentInChildren<TextMeshProUGUI>().SetText(stringBuilder);
				HG.StringBuilderPool.ReturnStringBuilder(stringBuilder);

				if (CorruptionDeltaElementTimer > 2f)
				{
					CorruptionDeltaNumber.SetActive(false);
					// probably unnecesary
					CorruptionDeltaElementTimer = 0f;
				}

				if (ConfigCorruptionDelta.Value && CorruptionDeltaNumber.activeSelf)
				{
					CorruptionDeltaElementTimer += Time.fixedDeltaTime;
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
			CorruptionDeltaNumber = new GameObject("CorruptionDeltaNumber");
			CorruptionDeltaNumber.transform.SetParent(instance.transform, false);
			RectTransform rectTransform = CorruptionDeltaNumber.AddComponent<RectTransform>();
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchorMax = Vector2.one;
			rectTransform.sizeDelta = Vector2.zero;
			rectTransform.anchoredPosition = new Vector2(-65f, 60f);
			TextMeshProUGUI textMeshProUGUI = CorruptionDeltaNumber.AddComponent<TextMeshProUGUI>();
			textMeshProUGUI.alignment = TextAlignmentOptions.Center;
			textMeshProUGUI.horizontalAlignment = HorizontalAlignmentOptions.Center;
			textMeshProUGUI.verticalAlignment = VerticalAlignmentOptions.Middle;

			if (!ConfigCorruptionDelta.Value)
			{
				CorruptionDeltaNumber.SetActive(false);
			}

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
				GameObject SuppressDeltaObject = Instantiate(FillObject);
				SuppressDeltaObject.name = "FillSuppressHealDelta";
				SuppressDeltaImageObject = SuppressDeltaObject.GetComponent<UnityEngine.UI.Image>();
				SuppressDeltaImageObject.color = ConfigSuppressHealDeltaColor.Value;
				SuppressDeltaObject.transform.SetParent(FillObject.transform.parent, false);
				SuppressDeltaObject.transform.SetSiblingIndex(1);
			}

			orig(self, controller, instance);
		}

		private void VoidSurvivorController_AddCorruption(On.RoR2.VoidSurvivorController.orig_AddCorruption orig, VoidSurvivorController self, float amount)
		{
			float oldCorruption = self.corruption;

			orig(self, amount);
			// Just skip everything if we're perma corrupted
			if (!self.isPermanentlyCorrupted)
			{
				// copied from RoR2 code. I hate this code.
				// Out of combat and in combat numbers are the same, but some mods may use it so...
				float num = ((!self.characterBody.HasBuff(self.corruptedBuffDef)) ?
					(self.characterBody.outOfCombat ?
						self.corruptionPerSecondOutOfCombat : self.corruptionPerSecondInCombat)
					: (self.corruptionFractionPerSecondWhileCorrupted * (self.maxCorruption - self.minimumCorruption)));

				// Don't show the delta if its passive corruption
				// HACK: also don't show if delta is 100. in theory should only happen on corruption state transitions
				// But this probably should have a better check just in case somehow you add 100 or more or smth
				// since a mod might change the values to go over 100 normally, so checking for 100 is bad 
				if (!Mathf.Approximately(amount, num * Time.fixedDeltaTime) && Mathf.Abs(amount) != 100f)
				{
					if (ConfigCorruptionDelta.Value && !CorruptionDeltaNumber.activeSelf)
					{
						CorruptionDeltaNumber.SetActive(true);
					}

					if (CorruptionDeltaNumber.activeSelf)
					{
						TextMeshProUGUI elementText = CorruptionDeltaNumber.GetComponent<TextMeshProUGUI>();

						StringBuilder stringBuilder = HG.StringBuilderPool.RentStringBuilder();

						if (amount > 0)
						{
							elementText.color = Color.green;
							stringBuilder.Append("+");
						}
						else
						{
							// Don't need to append "-" here because its already part of the float value
							elementText.color = Color.red;
						}

						if (ConfigCorruptionPercentageTweak.Value && self.isCorrupted)
						{
							amount = (amount / (self.maxCorruption - self.minimumCorruption)) * 100;
						}
						else if (!self.isCorrupted && oldCorruption + amount < self.minimumCorruption)
						{
							// Clamp corruption removed by supress to only show the actual delta
							// ex: corruption 30, minCorruption 20
							// using supress will bring corruption to 20, and the delta will show -10% instead of -25%
							amount = -(oldCorruption - self.corruption);
						}

						// If the delta is less than 1, early out before we actually set the values.
						// We need this here because we may sometimes modify the delta
						if (Mathf.Abs(amount) < 1)
						{
							CorruptionDeltaNumber.SetActive(false);
							return;
						}

						stringBuilder.AppendInt(Mathf.FloorToInt(amount), 1u, 3u).Append("%");
						elementText.SetText(stringBuilder);
						HG.StringBuilderPool.ReturnStringBuilder(stringBuilder);

						// Reset Timer
						CorruptionDeltaElementTimer = 0f;
					}
				}
			}
		}

		// The Awake() method is run at the very start when the game is initialized.
		public void Awake()
		{
			// Init our logging class so that we can properly log for debugging
			Log.Init(Logger);

			SpecialSkillDefs.Add(
				Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC1/VoidSurvivor/CrushCorruption.asset").WaitForCompletion(),
				GetSerializedValues(Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/DLC1/VoidSurvivor/EntityStates.VoidSurvivor.Weapon.CrushCorruption.asset").WaitForCompletion().serializedFieldsCollection.serializedFields)
				);
			CorruptedSpecialSkillDefs.Add(
				Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC1/VoidSurvivor/CrushHealth.asset").WaitForCompletion(),
				GetSerializedValues(Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/DLC1/VoidSurvivor/EntityStates.VoidSurvivor.Weapon.CrushHealth.asset").WaitForCompletion().serializedFieldsCollection.serializedFields)
				);

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
				"Show Meter Suppress Heal Delta",
				false,
				"Show on the corruption meter how much will be removed by Suppress" +
				"\n\nDefault: False"
				);

			ConfigSuppressHealDeltaColor = Config.Bind(
				"UI Tweaks",
				"Meter Suppress Heal Delta Color",
				new Color(0.58984375f, 0.265625f, 0.375f, 1f),
				"The color that will be used if \"Show Suppress Heal Delta\" is enabled" +
				"\n\nDefault: 964460FF"
				);

			ConfigSuppressCorruptDelta = Config.Bind(
				"UI Tweaks",
				"Show Meter Suppress Corruption Delta",
				false,
				"Show on the corruption meter how much will be added by Corrupted Suppress" +
				"\n\nDefault: False"
				);

			ConfigSuppressCorruptDeltaColor = Config.Bind(
				"UI Tweaks",
				"Meter Suppress Corruption Delta Color",
				new Color(0.6953125f, 0.15234375f, 0.1796875f, 1f),
				"The color that will be used if \"Show Suppress Corruption Delta\" is enabled" +
				"\n\nDefault: B2272EFF"
				);

			ConfigCorruptionDelta = Config.Bind(
				"UI Tweaks",
				"Show Corruption Delta Number",
				false,
				"Show next to the corruption UI the exact amount added or removed to your corruption percentage" +
				"\nPassive corruption buildup/removal is not shown as part of this delta" +
				"\nDelta is also hidden if you are permanently corrupted" +
				"\n\nDefault: False"
				);
			ConfigCorruptionDeltaPositiveColor = Config.Bind(
				"UI Tweaks",
				"Corruption Delta Number Positive Color",
				Color.green,
				"The color that will be used for positive corruption changes if \"Show Corruption Delta Number\" is enabled" +
				"\n\nDefault: 00FF00FF"
				);
			ConfigCorruptionDeltaNegativeColor = Config.Bind(
			"UI Tweaks",
			"Corruption Delta Number Negative Color",
				Color.red,
				"The color that will be used for positive corruption changes if \"Show Corruption Delta Number\" is enabled" +
				"\n\nDefault: FF0000FF"
				);

			ConfigShowPermaCorruptionMin = Config.Bind(
				"UI Tweaks",
				"Clamp Corruption Percentage to 100%",
				true,
				"When disabled, show the minimum corruption when permanently corrupted rather than capping it to 100%" +
				"\nJust for fun." +
				"\n\nDefault: True"
				);

			// can't figure this out, come back to it later.
			//ConfigCorruptionDelta.SettingChanged += (new object(), new SettingChangedEventArgs()) => CorruptionDeltaNumber.SetActive(ConfigCorruptionDelta.Value);

			// Only do this is RiskOfOptions is installed!
			if (RiskOfOptionsCompatibility.Enabled)
			{
				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigCorruptionPercentageTweak);

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigSuppressHealDelta);
				RiskOfOptionsCompatibility.AddColorOption(ConfigSuppressHealDeltaColor, delegate () { return !ConfigSuppressHealDelta.Value; });

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigSuppressCorruptDelta);
				RiskOfOptionsCompatibility.AddColorOption(ConfigSuppressCorruptDeltaColor, delegate () { return !ConfigSuppressCorruptDelta.Value; });

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigCorruptionDelta);
				RiskOfOptionsCompatibility.AddColorOption(ConfigCorruptionDeltaPositiveColor, delegate () { return !ConfigCorruptionDelta.Value; });
				RiskOfOptionsCompatibility.AddColorOption(ConfigCorruptionDeltaNegativeColor, delegate () { return !ConfigCorruptionDelta.Value; });

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigShowPermaCorruptionMin);


				RiskOfOptionsCompatibility.InvokeSetModDescription("Tweak various things about the Void Fiend corruption hud to fit your liking");
			}
		}

		// Due to dictionary being limited to single types, all values are stored as strings, you will have to parse them yourself.
		public Dictionary<string, string> GetSerializedValues(HG.GeneralSerializer.SerializedField[] serializedFields)
		{
			Dictionary<string, string> KVDict = new Dictionary<string, string>();

			foreach (var serializedField in serializedFields)
			{
				KVDict.Add(serializedField.fieldName, serializedField.fieldValue.stringValue);
			}

			return new Dictionary<string, string>(KVDict);
		}
	}
}

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using R2API.Utils;
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

	[BepInDependency(R2API.R2API.PluginGUID)]
	[NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]

	//[BepInDependency(LanguageAPI.PluginGUID)]

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
		public static ConfigEntry<Color> ConfigCorruptionDeltaPositiveColor { get; set; }
		public static ConfigEntry<Color> ConfigCorruptionDeltaNegativeColor { get; set; }

		public class CorruptionDeltaNotice
		{
			public float ElementTimer = 0f;
			public string DeltaValue = string.Empty;
			public bool PositiveDelta = false;
		}

		public static List<CorruptionDeltaNotice> CorruptionDeltaNoticeList = [];
		public static List<GameObject> CorruptionDeltaNoticeObjects = new(3);

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
		public static Dictionary<SkillDef, Dictionary<string, string>> SpecialSkillDefs = [];
		public static Dictionary<SkillDef, Dictionary<string, string>> CorruptedSpecialSkillDefs = [];

		public static ConfigEntry<bool> ConfigShowPermaCorruptionMin { get; set; }

		public static ConfigEntry<bool> ConfigShowCorruptionTimerWhenUncorrupted { get; set; }
		public static ConfigEntry<bool> ConfigShowCorruptionTimerWhenCorrupted { get; set; }
		public static GameObject CorruptionTimerObject = null;

		public void OnEnable()
		{
			On.RoR2.VoidSurvivorController.UpdateUI += CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded += VoidSurvivorController_OnOverlayInstanceAdded;
			On.RoR2.VoidSurvivorController.OnCorruptionModified += VoidSurvivorController_OnCorruptionModified;
		}

		public void OnDisable()
		{
			On.RoR2.VoidSurvivorController.UpdateUI -= CustomUpdateUI;
			On.RoR2.VoidSurvivorController.OnOverlayInstanceAdded -= VoidSurvivorController_OnOverlayInstanceAdded;
			On.RoR2.VoidSurvivorController.OnCorruptionModified -= VoidSurvivorController_OnCorruptionModified;
		}

		private void UpdateCorruptionTimer(VoidSurvivorController survivorController)
		{
			float secondsToCorruption;
			if (!survivorController.isPermanentlyCorrupted)
			{
				if (survivorController.isCorrupted)
				{
					if (!ConfigShowCorruptionTimerWhenCorrupted.Value)
					{
						CorruptionTimerObject.SetActive(false);
						return;
					}

					CorruptionTimerObject.SetActive(true);
					secondsToCorruption = (survivorController.corruption - survivorController.minimumCorruption) / Mathf.Abs(survivorController.corruptionFractionPerSecondWhileCorrupted * 100f);
				}
				else
				{
					if (!ConfigShowCorruptionTimerWhenUncorrupted.Value)
					{
						CorruptionTimerObject.SetActive(false);
						return;
					}

					if (survivorController.characterBody.outOfCombat)
					{
						secondsToCorruption = (survivorController.maxCorruption - survivorController.corruption) / survivorController.corruptionPerSecondOutOfCombat;
					}
					else
					{
						secondsToCorruption = (survivorController.maxCorruption - survivorController.corruption) / survivorController.corruptionPerSecondInCombat;
					}
					CorruptionTimerObject.SetActive(true);
				}

				CorruptionTimerObject.GetComponent<TimerText>().seconds = secondsToCorruption;
			}
			else
			{
				CorruptionTimerObject.SetActive(false);
			}
		}

		private void UpdateCorruptionDeltaNotices()
		{
			for (int i = 0; i < CorruptionDeltaNoticeList.Count; i++)
			{
				CorruptionDeltaNotice currNotice = CorruptionDeltaNoticeList[i];
				TextMeshProUGUI noticeText = CorruptionDeltaNoticeObjects[i].GetComponent<TextMeshProUGUI>();
				noticeText.text = currNotice.DeltaValue;
				noticeText.color = currNotice.PositiveDelta ? ConfigCorruptionDeltaPositiveColor.Value : ConfigCorruptionDeltaNegativeColor.Value;
				CorruptionDeltaNoticeObjects[i].SetActive(true);
			}
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
					if (ConfigCorruptionPercentageTweak.Value && survivorController.isCorrupted)
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
				if (ConfigCorruptionPercentageTweak.Value && survivorController.isCorrupted)
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

				if (ConfigCorruptionDelta.Value)
				{
					for (int i = 0; i < CorruptionDeltaNoticeList.Count; i++)
					{
						CorruptionDeltaNotice currNotice = CorruptionDeltaNoticeList[i];
						if (currNotice.ElementTimer >= 2f)
						{
							CorruptionDeltaNoticeObjects[i].SetActive(false);
							CorruptionDeltaNoticeList.RemoveAt(i);
						}
						else if (CorruptionDeltaNoticeObjects[i].activeSelf)
						{
							currNotice.ElementTimer += Time.fixedDeltaTime;
						}
					}
				}
				else
				{
					// TODO: should probably just have a parent object that can be disabled
					foreach (GameObject noticeObject in CorruptionDeltaNoticeObjects)
					{
						noticeObject.SetActive(false);
					}
				}

				UpdateCorruptionTimer(self);
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

		private GameObject GenerateCorruptionDeltaHudElement(string name, float ypos, GameObject overlayInstance, float fontSize = -1)
		{
			GameObject CorruptionDeltaNumber = new(name);
			CorruptionDeltaNumber.transform.SetParent(overlayInstance.transform, false);
			RectTransform CorruptionDeltaNumberTransform = CorruptionDeltaNumber.AddComponent<RectTransform>();
			CorruptionDeltaNumberTransform.anchorMin = Vector2.zero;
			CorruptionDeltaNumberTransform.anchorMax = Vector2.one;
			CorruptionDeltaNumberTransform.sizeDelta = Vector2.zero;
			CorruptionDeltaNumberTransform.anchoredPosition = new Vector2(30f, ypos);
			TextMeshProUGUI CorruptionDeltaNumberText = CorruptionDeltaNumber.AddComponent<TextMeshProUGUI>();
			CorruptionDeltaNumberText.alignment = TextAlignmentOptions.Left;
			CorruptionDeltaNumberText.horizontalAlignment = HorizontalAlignmentOptions.Left;
			CorruptionDeltaNumberText.verticalAlignment = VerticalAlignmentOptions.Middle;
			if (fontSize != -1)
			{
				CorruptionDeltaNumberText.fontSize = fontSize;
			}

			return CorruptionDeltaNumber;
		}

		private void VoidSurvivorController_OnOverlayInstanceAdded(On.RoR2.VoidSurvivorController.orig_OnOverlayInstanceAdded orig, VoidSurvivorController self, OverlayController controller, GameObject instance)
		{
			CorruptionDeltaNoticeObjects = []; // Make sure its empty before we add to it. Fixes NREs after going to a new stage
			CorruptionDeltaNoticeObjects.Add(GenerateCorruptionDeltaHudElement("CorruptionDeltaNumber", 60, instance));
			CorruptionDeltaNoticeObjects.Add(GenerateCorruptionDeltaHudElement("CorruptionDeltaNumber2", 85, instance, 24));
			CorruptionDeltaNoticeObjects.Add(GenerateCorruptionDeltaHudElement("CorruptionDeltaNumber3", 105, instance, 24));

			CorruptionTimerObject = new GameObject("CorruptionTimerObject");
			CorruptionTimerObject.transform.SetParent(instance.transform, false);
			RectTransform CorruptionTimerTransform = CorruptionTimerObject.AddComponent<RectTransform>();
			CorruptionTimerTransform.anchorMin = Vector2.zero;
			CorruptionTimerTransform.anchorMax = Vector2.one;
			CorruptionTimerTransform.sizeDelta = Vector2.zero;
			CorruptionTimerTransform.anchoredPosition = new Vector2(-145f, 2.5f);
			TimerText CorruptionTimerText = CorruptionTimerObject.AddComponent<TimerText>();
			CorruptionTimerText.format = ScriptableObject.CreateInstance<TimerStringFormatter>();

			TimerStringFormatter.Format.Unit[] newUnits = [];

			// If player is using a mod that increases the amount of time for a corruption state to over a minute, show them minutes!
			if ((self.maxCorruption / Mathf.Abs(self.corruptionFractionPerSecondWhileCorrupted * 100f) > 60f) ||
				self.maxCorruption / self.corruptionPerSecondOutOfCombat > 60f ||
				self.maxCorruption / self.corruptionPerSecondInCombat > 60f)
			{
				newUnits = newUnits.AddToArray(new TimerStringFormatter.Format.Unit
				{
					name = "minutes",
					conversionRate = 60.0,
					maxDigits = 2u,
					minDigits = 2u,
					prefix = string.Empty,
					suffix = ":"
				});

				// move this so that the colon is more centered.
				CorruptionTimerTransform.anchoredPosition = new Vector2(-135, 2.5f);
			}

			newUnits = newUnits.AddRangeToArray([
				new TimerStringFormatter.Format.Unit
				{
					name = "seconds",
					conversionRate = 1.0,
					maxDigits = 2u,
					minDigits = 2u,
					prefix = string.Empty,
					suffix = string.Empty
				},
				new TimerStringFormatter.Format.Unit
				{
					name = "centiseconds",
					conversionRate = 0.01,
					maxDigits = 2u,
					minDigits = 2u,
					prefix = "<voffset=0.4em><size=40%><mspace=0.5em>.",
					suffix = "</size></voffset></mspace>"
				}
			]);


			CorruptionTimerText.format.format = new TimerStringFormatter.Format
			{
				prefix = "<mspace=0.5em>",
				suffix = "</mspace>",
				units = newUnits
			};
			TextMeshProUGUI CorruptionTimerText2 = CorruptionTimerText.targetLabel = CorruptionTimerObject.AddComponent<TextMeshProUGUI>();
			CorruptionTimerText2.alignment = TextAlignmentOptions.Center;
			CorruptionTimerText2.horizontalAlignment = HorizontalAlignmentOptions.Center;
			CorruptionTimerText2.verticalAlignment = VerticalAlignmentOptions.Middle;

			CorruptionTimerObject.SetActive(false);

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

		private void VoidSurvivorController_OnCorruptionModified(On.RoR2.VoidSurvivorController.orig_OnCorruptionModified orig, VoidSurvivorController self, float newCorruption)
		{
			float oldCorruption = self.corruption;

			orig(self, newCorruption);

			// Just skip everything if we're perma corrupted
			if (!self.isPermanentlyCorrupted && ConfigCorruptionDelta.Value)
			{
				float corruptionDelta = newCorruption - oldCorruption;
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
				if (!Mathf.Approximately(corruptionDelta, num * Time.fixedDeltaTime) && Mathf.Abs(corruptionDelta) != 100f)
				{
					CorruptionDeltaNotice newNotice = new()
					{
						ElementTimer = 0f
					};

					StringBuilder stringBuilder = HG.StringBuilderPool.RentStringBuilder();

					if (corruptionDelta > 0)
					{
						newNotice.PositiveDelta = true;
						stringBuilder.Append("+");
					}
					else
					{
						// Don't need to append "-" here because its already part of the float value
						newNotice.PositiveDelta = false;
					}

					if (ConfigCorruptionPercentageTweak.Value && self.isCorrupted)
					{
						corruptionDelta = (corruptionDelta / (self.maxCorruption - self.minimumCorruption)) * 100;
					}
					else if (!self.isCorrupted && oldCorruption + newCorruption < self.minimumCorruption)
					{
						// Clamp corruption removed by supress to only show the actual delta
						// ex: corruption 30, minCorruption 20
						// using supress will bring corruption to 20, and the delta will show -10% instead of -25%
						corruptionDelta = -(oldCorruption - self.corruption);
					}

					// If the delta is less than 1, early out before we actually set the values.
					// We need this here because we may sometimes modify the delta
					if (Mathf.Abs(corruptionDelta) < 1)
					{
						return;
					}

					stringBuilder.AppendInt(Mathf.FloorToInt(corruptionDelta), 1u, 3u).Append("%");
					newNotice.DeltaValue = stringBuilder.ToString();
					HG.StringBuilderPool.ReturnStringBuilder(stringBuilder);

					if (CorruptionDeltaNoticeList.Count >= 3)
					{
						CorruptionDeltaNoticeList.RemoveRange(2, CorruptionDeltaNoticeList.Count - 2);
					}
					// Make sure to insert at the START of the list.
					CorruptionDeltaNoticeList.Insert(0, newNotice);
					CorruptionDeltaNoticeList.TrimExcess();

					UpdateCorruptionDeltaNotices();
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
				"Show Corruption Delta Notices",
				false,
				"Show next to the corruption UI the exact amount added or removed to your corruption percentage" +
				"\nPassive corruption buildup/removal is not shown as part of this delta" +
				"\nDelta is also hidden if you are permanently corrupted" +
				"\n\nDefault: False"
				);
			ConfigCorruptionDeltaPositiveColor = Config.Bind(
				"UI Tweaks",
				"Corruption Delta Notice Positive Color",
				Color.green,
				"The color that will be used for positive corruption changes if \"Show Corruption Delta Notice\" is enabled" +
				"\n\nDefault: 00FF00FF"
				);
			ConfigCorruptionDeltaNegativeColor = Config.Bind(
				"UI Tweaks",
				"Corruption Delta Notice Negative Color",
				Color.red,
				"The color that will be used for positive corruption changes if \"Show Corruption Delta Notice\" is enabled" +
				"\n\nDefault: FF0000FF"
				);

			ConfigShowCorruptionTimerWhenUncorrupted = Config.Bind(
				"UI Tweaks",
				"Show Corruption Timer",
				false,
				"Show timer until corrupted underneath corruption UI" +
				"\n\nDefault: False"
				);
			ConfigShowCorruptionTimerWhenCorrupted = Config.Bind(
				"UI Tweaks",
				"Show Corruption Timer While Corrupted",
				false,
				"Show timer until no longer corrupted underneath corruption UI" +
				"\n\nDefault: False"
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

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigShowCorruptionTimerWhenUncorrupted);
				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigShowCorruptionTimerWhenCorrupted);

				RiskOfOptionsCompatibility.AddCheckBoxOption(ConfigShowPermaCorruptionMin);

				RiskOfOptionsCompatibility.InvokeSetModDescription("Tweak various things about the Void Fiend corruption hud to fit your liking");
			}
		}

		// Due to dictionary being limited to single types, all values are stored as strings, you will have to parse them yourself.
		public Dictionary<string, string> GetSerializedValues(HG.GeneralSerializer.SerializedField[] serializedFields)
		{
			Dictionary<string, string> KVDict = [];

			foreach (var serializedField in serializedFields)
			{
				KVDict.Add(serializedField.fieldName, serializedField.fieldValue.stringValue);
			}

			return new Dictionary<string, string>(KVDict);
		}
	}
}

using BepInEx.Configuration;
using System;

namespace VoidFiendHudTweaks
{
	public static class RiskOfOptionsCompatibility
	{
		private static bool? _enabled;
		public static bool Enabled
		{
			get
			{
				if (_enabled == null)
				{
					_enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");
				}
				return (bool)_enabled;
			}
		}

		public static void AddCheckBoxOption(ConfigEntry<bool> newEntry)
		{
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(newEntry));
		}

		public static void AddCheckBoxOption(ConfigEntry<bool> newEntry, bool restartRequired = false)
		{
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(newEntry, restartRequired));
		}

		public static void AddCheckBoxOption(ConfigEntry<bool> newEntry, string inputName = null, string inputDescription = null, string inputCategory = null, bool inputRestartRequired = false, Delegate inputCheckIfDisabled = null)
		{
			var OptionConfig = new RiskOfOptions.OptionConfigs.CheckBoxConfig()
			{
				name = inputName,
				description = inputDescription,
				category = inputCategory,
				restartRequired = inputRestartRequired,
				// I have no clue how this works, so I hope that it does in fact work.
				checkIfDisabled = (RiskOfOptions.OptionConfigs.BaseOptionConfig.IsDisabledDelegate)inputCheckIfDisabled
			};
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(newEntry, OptionConfig));
		}

		public static void AddColorOption(ConfigEntry<UnityEngine.Color> newEntry)
		{
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ColorOption(newEntry));
		}

		public static void AddColorOption(ConfigEntry<UnityEngine.Color> newEntry, bool restartRequired = false)
		{
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ColorOption(newEntry, restartRequired));
		}

		public static void AddColorOption(ConfigEntry<UnityEngine.Color> newEntry, string inputName = null, string inputDescription = null, string inputCategory = null, bool inputRestartRequired = false, Delegate inputCheckIfDisabled = null)
		{
			var OptionConfig = new RiskOfOptions.OptionConfigs.ColorOptionConfig()
			{
				name = inputName,
				description = inputDescription,
				category = inputCategory,
				restartRequired = inputRestartRequired,
				// I have no clue how this works, so I hope that it does in fact work.
				checkIfDisabled = (RiskOfOptions.OptionConfigs.BaseOptionConfig.IsDisabledDelegate)inputCheckIfDisabled
			};
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ColorOption(newEntry, OptionConfig));
		}

		public static void AddColorOption(ConfigEntry<UnityEngine.Color> newEntry, Func<bool> inputCheckIfDisabled = null)
		{
			var OptionConfig = new RiskOfOptions.OptionConfigs.ColorOptionConfig()
			{
				// I have no clue how this works, so I hope that it does in fact work.
				checkIfDisabled = new RiskOfOptions.OptionConfigs.BaseOptionConfig.IsDisabledDelegate(inputCheckIfDisabled)
			};
			RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ColorOption(newEntry, OptionConfig));
		}

		//public static RiskOfOptions.Options.CheckBoxOption CreateCheckBoxOption(ConfigEntry<bool> newEntry)
		//{
		//	return new RiskOfOptions.Options.CheckBoxOption(newEntry);
		//}

		//public static void InvokeAddOption(RiskOfOptions.Options.BaseOption option)
		//{
		//	RiskOfOptions.ModSettingsManager.AddOption(option);
		//}

		public static void InvokeSetModDescription(string description)
		{
			RiskOfOptions.ModSettingsManager.SetModDescription(description);
		}
	}
}

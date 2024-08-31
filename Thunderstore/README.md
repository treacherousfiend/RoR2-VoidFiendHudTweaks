# Void Fiend Hud Tweaks

A collection of customizable tweaks for Void Fiend's corruption hud to hopefully make the convoluted mechanic a bit easier to understand for some people (aka me)

Client-side mod<br>
This mod *ONLY* affects the hud, and all it does is read the numbers the game already has, so it should be flexible enough to handle mods which change the actual gameplay of Void Fiend with no issues!

If you have any bug reports, suggestions, etc. Please make an issue post on the GitHub page!

## Features:

### Always decrement corruption to 0%

Enabled by default.

A toggle to have the corruption meter *always* decrement to 0% when corrupted, no matter what your minimum corruption is.

![CorruptionPercentageTweak](https://raw.githubusercontent.com/treacherousfiend/RoR2-VoidFiendHudTweaks/main/assets/CorruptionPercentageTweak.png)

Hopefully makes a bit clearer that the duration of your corrupted form is NOT changed the more void items you have.<br>
The downside to this is that it makes changes to your corruption appear larger than they actually are. Because of this, I recommend using the "Show  Meter Suppress Corruption Delta" option so that you'll always know how much the Corrupted Suppress ability will add.

### Show Meter Suppress Heal Delta & Show Meter Suppress Corruption Delta

Show on the corruption meter how much corruption will be removed by Suppress, Corrupted Suppress, or other (likely modded) special skill.

![HealthSuppressDelta](https://raw.githubusercontent.com/treacherousfiend/RoR2-VoidFiendHudTweaks/main/assets/SuppressHealDelta.png)

The delta meter is hidden if you cannot use the ability (either due to not having any charges or because you have a replacement skill that does not affect corruption).<br>
Colors are customizable for when corrupted and when not corrupted.

### Show Corruption Delta Notices

Show a number to the right of the hud meter when a change has been made to your corruption (i.e. -25% from using Suppress).

![CorruptionDeltaNotice](https://raw.githubusercontent.com/treacherousfiend/RoR2-VoidFiendHudTweaks/main/assets/CorruptionDeltaNotice.png)

Shows the 3 most recent notices, passive corruption changes are not shown.<br>
Colors is customizable for positive and negative changes.

### Show Corruption Timer & Show Corruption Timer While Corrupted

Show a timer underneath the hud meter which counts down until you either become corrupted or uncorrupted.

![CorruptionTimer](https://raw.githubusercontent.com/treacherousfiend/RoR2-VoidFiendHudTweaks/main/assets/CorruptionTimer.png)

If you have a mod that increases the time of either form to over 1 minute, the timer should also show minutes.

### Clamp Corruption Percentage to 100%

Enabled by default.

Cap the percentage on the hud to 100%, even if your minimum corruption is higher than that.<br>
For fun, you can disable it just to see how high your corruption *really* is.

### Other

Fixed a vanilla bug which caused the percentage to flicker between minimum corruption and 100% if your minimum corruption was over 100%

## Known Issues:

With Corruption Delta Notices enabled, adding 2 notices at the same time (say from a crit and leeching seed) will cause one of them to not be removed until overwritten by a new one.

<br>

## For mod devs:

If you are adding a new special skill which changes corruption, you will need to add it to a list of special skills that the mod uses to determine whether or not it needs to show the meter deltas.

`SpecialSkillDefs` and `CorruptedSpecialSkillDefs` are both of type `Dictionary<SkillDef, Dictionary<string, string>>`<br>
`SkillDef` should be self-explanatory, the second Dictionary inside of it is for serialized values from the `EntityStateConfiguration` of the ability.<br>
This mod comes with a helper function `GetSerializedValues(HG.GeneralSerializer.SerializedField[] serializedFields)`, which will automatically generate the dictionary for you.

In my case, in order to add Suppress to `SpecialSkillDefs`, I grab the `SkillDef` from the asset, and then put the skill's `EntityStateConfiguration.serializedFieldsCollection.serializedFields` as the input to `GetSerializedValues()`:
```
SpecialSkillDefs.Add(
	Addressables.LoadAssetAsync<SkillDef>("RoR2/DLC1/VoidSurvivor/CrushCorruption.asset").WaitForCompletion(),
	GetSerializedValues(Addressables.LoadAssetAsync<EntityStateConfiguration>("RoR2/DLC1/VoidSurvivor/EntityStates.VoidSurvivor.Weapon.CrushCorruption.asset").WaitForCompletion().serializedFieldsCollection.serializedFields)
	);
```

The mod currently only supports checking special skills, if you're adding an ability to change corruption to another skill slot, let me know!<br> I'm totally fine with looking into making it check for other abilities, just haven't done it yet.
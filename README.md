# Void Fiend Hud Tweaks

A collection of customizable tweaks for Void Fiend's corruption hud to hopefully make the convoluted mechanic a bit easier to understand for some people (aka me)

If you're only here for mod downloads, you'll find more info on the Thunderstore page.<br>
https://thunderstore.io/package/fiendtopia/VoidFiendHudTweaks/

## To-Do

- Localization Support
- Setup setting change hooks so that code is completely skipped instead of checking if an option is enabled every frame
- Make the code a bit more modular so that hud elements can potentially be generated on the fly

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

## Licensing

This repository is dedicated to the public domain under CC0

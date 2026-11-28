# Changelog

All notable changes to this project will be documented in this file.

## 1.1.1 - 27-Nov-26

### Changed

- Opening the crafting menu for the first time no longer hitches. The scroll-tracking hooks used to patch every ScrollBehaviour method with Harmony on that first open; they now patch only the five methods the mod reads, once at startup.
- The search box's template input is reused across scenes instead of being found again with a scene-wide scan, and the BLUEPRINTS header label is remembered so later opens skip reading every label.
- Each filter pass is lighter: the detail and requirement fields blanked on no matches are collected once per crafting panel, and the reflection lookups used for ingredient matching, scroll tracking and list refreshes are cached.
- Typing in the search box no longer stutters. A keystroke used to call the game's ApplyFilter, which retriggered our postfix every frame (and set m_NeedsUpdate again from scroll-list hooks). The category list is now snapshotted once, then filtered in place, and an unchanged result skips the UI refresh.
- Blueprint names, ingredient names and source-mod names are cached so later keystrokes only do string Contains checks. Row click handlers are reused instead of being rebuilt on every filter pass.

## 1.1.0 - 24-Nov-26

### Added

- Unprefixed (default) search also matches a blueprint's source mod or pack name when [ModdedBlueprintUtils](https://github.com/leatherneck6017/TLD-ModdedBlueprintUtils) is installed. The lookup uses the `TryGetModSourceInfo` instance API through reflection, so ModdedBlueprintUtils is an optional soft dependency; CraftingSearch is unchanged without it. A fallback still recognizes the older BlueprintInfo mod name. The `name:` and `ingredient:`/`ing:` prefixes do not include mod-name matching.

### Fixed

- Clicking the search bar's X clear button keeps focus in the search box so you can continue typing.

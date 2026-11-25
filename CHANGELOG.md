# Changelog

All notable changes to this project will be documented in this file.

## 1.1.0 - 24-Nov-26

### Added

- Unprefixed (default) search also matches a blueprint's source mod or pack name when [ModdedBlueprintUtils](https://github.com/leatherneck6017/TLD-ModdedBlueprintUtils) is installed. The lookup uses the `TryGetModSourceInfo` instance API through reflection, so ModdedBlueprintUtils is an optional soft dependency; CraftingSearch is unchanged without it. A fallback still recognizes the older BlueprintInfo mod name. The `name:` and `ingredient:`/`ing:` prefixes do not include mod-name matching.

### Fixed

- Clicking the search bar's X clear button keeps focus in the search box so you can continue typing.

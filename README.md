# CraftingSearch

A MelonLoader mod for The Long Dark that adds a live, responsive search bar into the crafting menu to filter blueprints by name, required ingredients, or both. Not having a native blueprint search in the base game has long been a small friction point for me, especially as the crafting list grows alongside all the great mods available for the game.

## Features

- **In-Menu Search Bar**: Anchored above the BLUEPRINTS header in the crafting menu, styled to match the surrounding UI. Complete with a one-click X clear button, and syntax placeholder text.
- **Real-Time Name & Ingredient Filtering**: Filters the blueprint list on text changes against blueprint names and displayed/localized ingredient names rather than internal asset names.
- **Flexible Search Qualifiers**: Supports multiple intuitive prefix delimiters (see below) out of the box with zero configuration required.
- **Empty State Handling**: Restores category blueprints when a query is cleared and blanks detail and requirement fields when a filter produces no matches.
- **Keyboard and Input Handling**:
  - Tab toggles focus on the search input while the crafting menu is open.
  - Enter, Numpad Enter, and Escape unfocus the search input while typing.
  - Temporarily locks player input maps while typing to prevent hotkey keystrokes from being sent to the game.
- **Scroll and Row Selection Sync**: Dynamically updates visible blueprint rows, icons, and row click selection indices when filtering or scrolling.

## Search Syntax & Prefixes

Typing a plain search term searches **both** blueprint names and their required ingredients. To narrow your results to a specific field, start your query with any of the supported prefixes (case-insensitive):

| Search Mode | Supported Prefixes | Examples |
| :--- | :--- | :--- |
| **General Search** (Name & Ingredients) | (No prefix) | `cloth`, `sapling`, `bandage` |
| **Blueprint Name Only** | `name:`, `name!`, `name/`, `name\` | `name:bow`, `name!sapling`, `name/coat` |
| **Ingredients Only** | `ing:`, `ing!`, `ing/`, `ing\`<br>`ingredient:`, `ingredient!`, `ingredient/`, `ingredient\` | `ing:leather`, `ing!sapling`, `ingredient/gut` |

> **Tip:** Spaces after a prefix (such as `ing: cloth` or `name! sapling`) are automatically trimmed and handled cleanly.

## Issues

If you encounter a bug or have a feature request, please open a report on the GitHub Issues tracker or reach out in the TLD Modding Discord server @leatherneck6017.

## Roadmap

- **Inventory Panel Search**: Expand live search filtering to the player inventory so gear, clothing, food, and materials can be located quickly without manual scrolling.
- **Cooking Panel Search**: Bring name and ingredient search filtering to the cooking interface to make finding recipes and usable ingredients fast when preparing meals.
- - **ModSettings Integration**: Add a dedicated in-game settings page to customize search behavior, focus hotkeys, and UI preferences.

## Requirements

- The Long Dark (v2.58+)
- MelonLoader targeting (v0.7.2)

## Installation

1. Install MelonLoader for The Long Dark.
2. Place `CraftingSearch.dll` into your game's Mods folder.
3. Launch The Long Dark and open the crafting menu.

## Credits

- Hinterland Studio for creating this amazing game
- Other TLD modders for inspiration and reference materials
- The TLD Modding Discord server for guidance, testing, and feedback
- Modding Framework and Libraries: MelonLoader, Harmony (HarmonyLib), and Il2CppInterop

## Project Structure

- `Patches.cs` — Harmony patches (Panel_Crafting_Patches) on Panel_Crafting (Enable, ApplyFilter, and Update) for focus hotkeys, input locking, and filter hooks.
- `NameSearch.cs` — Filtering orchestration (ApplySearchFilter), qualifier mode dispatch, category restoration, and detail pane blanking.
- `IngredientSearch.cs` — Query mode parsing (ParseQuery / SearchMode) across supported prefixes and display-name ingredient matching on BlueprintData.
- `ScrollListSync.cs` — ScrollBehaviour patching, blueprint row synchronization, row icon updates, and UIScrollView handling.
- `RowSelection.cs` — Row click binding, scroll offset calculation, and blueprint selection index resolution.
- `InputLock.cs` — Rewired input map locking while typing in the search bar.
- `SearchBarUI.cs` — NGUI UIInput creation, positioning above BLUEPRINTS, clear button handling, placeholder styling sync, and deferred filter refresh coroutine.

## Building from Source

1. Clone or open the project in Visual Studio / .NET SDK targeting `net6.0`.
2. Update `TLDPath` in the `.csproj` file to point to your The Long Dark installation directory (currently set to `E:\SteamLibrary\steamapps\common\TheLongDark`).
3. Build the project so references from `$(TLDPath)\MelonLoader\net6\*.dll` and `$(TLDPath)\MelonLoader\Il2CppAssemblies\*.dll` resolve.

## AI Disclosure

AI tools were consulted during the development of this mod as part of my learning process for exploring The Long Dark's IL2CPP APIs, Harmony patching, and troubleshooting. All code was tested and verified in-game.
using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.Gear;
using MelonLoader;

namespace CraftingSearch
{
    internal static class SearchCache
    {
        internal sealed class Entry
        {
            internal string displayedName = string.Empty;
            internal string assetName = string.Empty;
            internal string[] ingredients = Array.Empty<string>();
            internal string sourceLabel = string.Empty;
            internal string friendlyModName = string.Empty;
        }

        private static readonly Dictionary<int, Entry> _entries = new();
        private static readonly List<string> _nameBuffer = new();
        private static int _listPanelId;
        private static int _listCount;
        private static int _listFirst;
        private static int _listLast;

        internal static void BeginWarm(Panel_Crafting panel)
        {
            if (panel == null)
                return;

            MelonCoroutines.Start(WarmRoutine(panel));
        }

        private static IEnumerator WarmRoutine(Panel_Crafting panel)
        {
            yield return null;
            if (panel == null || !panel.IsEnabled())
                yield break;

            EnsureList(panel);
            var all = panel.m_Blueprints;
            if (all == null)
                yield break;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                    Get(all[i]);

                // spread the first-time GetDisplayedNameWithCount / ingredient walk across frames
                if ((i & 31) == 31)
                    yield return null;
            }
        }

        internal static void EnsureList(Panel_Crafting panel)
        {
            if (panel == null)
                return;

            var all = panel.m_Blueprints;
            int count = all != null ? all.Count : 0;
            int first = count > 0 && all[0] != null ? all[0].GetInstanceID() : 0;
            int last = count > 0 && all[count - 1] != null ? all[count - 1].GetInstanceID() : 0;
            int panelId = panel.GetInstanceID();
            if (_listPanelId == panelId && _listCount == count && _listFirst == first && _listLast == last)
                return;

            _entries.Clear();
            _listPanelId = panelId;
            _listCount = count;
            _listFirst = first;
            _listLast = last;
        }

        internal static Entry Get(BlueprintData bp)
        {
            if (bp == null)
                return null;

            int id = bp.GetInstanceID();
            if (_entries.TryGetValue(id, out Entry entry))
                return entry;

            entry = Build(bp);
            _entries[id] = entry;
            return entry;
        }

        internal static string GetDisplayedName(BlueprintData bp)
        {
            Entry entry = Get(bp);
            return entry != null ? entry.displayedName : string.Empty;
        }

        internal static bool Matches(BlueprintData bp, SearchMode mode, string term)
        {
            Entry entry = Get(bp);
            if (entry == null)
                return false;

            bool nameMatches =
                (!string.IsNullOrEmpty(entry.displayedName) &&
                 entry.displayedName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(entry.assetName) &&
                 entry.assetName.Contains(term, StringComparison.OrdinalIgnoreCase));

            if (mode == SearchMode.NameOnly)
                return nameMatches;

            bool ingredientMatches = false;
            string[] ingredients = entry.ingredients;
            for (int i = 0; i < ingredients.Length; i++)
            {
                string name = ingredients[i];
                if (!string.IsNullOrEmpty(name) && name.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    ingredientMatches = true;
                    break;
                }
            }

            if (mode == SearchMode.IngredientOnly)
                return ingredientMatches;

            return nameMatches || ingredientMatches ||
                   (!string.IsNullOrEmpty(entry.friendlyModName) &&
                    entry.friendlyModName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrEmpty(entry.sourceLabel) &&
                    entry.sourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static Entry Build(BlueprintData bp)
        {
            var entry = new Entry();
            try
            {
                entry.displayedName = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
            }
            catch
            {
                entry.displayedName = string.Empty;
            }

            entry.assetName = bp.name ?? string.Empty;

            _nameBuffer.Clear();
            IngredientSearch.CollectNames(bp, _nameBuffer);
            entry.ingredients = _nameBuffer.Count == 0 ? Array.Empty<string>() : _nameBuffer.ToArray();

            if (ModSearch.TryGetSourceFields(bp, out string sourceLabel, out string friendlyModName))
            {
                entry.sourceLabel = sourceLabel ?? string.Empty;
                entry.friendlyModName = friendlyModName ?? string.Empty;
            }

            return entry;
        }
    }
}

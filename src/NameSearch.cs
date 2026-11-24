using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace CraftingSearch
{
    internal static class NameSearch
    {
        private const System.Reflection.BindingFlags InstanceMemberFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        private static bool _hadSearchQuery = false;

        internal static void EnsureScrollBehaviourPatched() =>
            ScrollListSync.EnsureScrollBehaviourPatched();

        internal static void SetNativeInputLock(bool locked) =>
            InputLock.SetNativeInputLock(locked);

        internal static void ApplySearchFilter(Panel_Crafting __instance)
        {
            if (__instance == null)
                return;

            RowSelection.ResetSelectionTracking();

            string query = SearchBarUI.CurrentQuery;
            var filtered = __instance.m_FilteredBlueprints;
            bool isQueryEmpty = string.IsNullOrEmpty(query);
            bool queryJustCleared = _hadSearchQuery && isQueryEmpty;
            _hadSearchQuery = !isQueryEmpty;

            if (queryJustCleared && filtered != null && filtered.Count == 0 && __instance.m_Blueprints != null && __instance.m_Blueprints.Count > 0)
            {
                filtered.Clear();
                for (int i = 0; i < __instance.m_Blueprints.Count; i++)
                {
                    var bp = __instance.m_Blueprints[i];
                    if (bp != null)
                    {
                        filtered.Add(bp);
                    }
                }

                foreach (string methodName in new[] { "FilterByCategory", "ApplyCategoryFilter", "RefreshCategory", "RefreshFilteredBlueprints", "UpdateFilteredBlueprints" })
                {
                    var m = typeof(Panel_Crafting).GetMethod(methodName, InstanceMemberFlags);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        try { m.Invoke(__instance, null); break; } catch { }
                    }
                }
            }

            if (filtered == null)
                return;

            RestoreCategoryBlueprintsIfEmpty(__instance, filtered, query);

            SearchMode mode = IngredientSearch.ParseQuery(query, out string searchTerm);
            bool hasEffectiveQuery = !string.IsNullOrEmpty(searchTerm);

            if (hasEffectiveQuery)
            {
                for (int i = filtered.Count - 1; i >= 0; i--)
                {
                    BlueprintData bp = filtered[i];
                    if (bp == null)
                    {
                        filtered.RemoveAt(i);
                        continue;
                    }

                    bool nameMatches = (bp.GetDisplayedNameWithCount() ?? string.Empty).Contains(searchTerm, System.StringComparison.OrdinalIgnoreCase) ||
                                       (bp.name ?? string.Empty).Contains(searchTerm, System.StringComparison.OrdinalIgnoreCase);

                    bool matches = mode switch
                    {
                        SearchMode.NameOnly => nameMatches,
                        SearchMode.IngredientOnly => IngredientSearch.MatchesAnyIngredient(bp, searchTerm),
                        _ => nameMatches || IngredientSearch.MatchesAnyIngredient(bp, searchTerm)
                    };

                    if (!matches)
                    {
                        filtered.RemoveAt(i);
                    }
                }

                RepositionCraftingWidgets(__instance);
            }

            __instance.m_NeedsUpdate = true;

            var refreshAvail = typeof(Panel_Crafting).GetMethod("RefreshAvailableBlueprints", InstanceMemberFlags);
            if (refreshAvail != null && refreshAvail.GetParameters().Length == 0)
            {
                try { refreshAvail.Invoke(__instance, null); } catch { }
            }

            RepositionCraftingWidgets(__instance);

            bool noSearchMatches = hasEffectiveQuery && filtered.Count == 0;
            SetDetailPaneBlank(__instance, noSearchMatches);
            ScrollListSync.RefreshCraftingScrollList(__instance, filtered);
        }

        private static void RepositionCraftingWidgets(Panel_Crafting panel)
        {
            if (panel == null || panel.gameObject == null || !panel.IsEnabled())
                return;

            foreach (var grid in panel.GetComponentsInChildren<UIGrid>(true))
            {
                if (grid != null)
                {
                    grid.repositionNow = true;
                    grid.Reposition();
                }
            }

            foreach (var table in panel.GetComponentsInChildren<UITable>(true))
            {
                if (table != null)
                {
                    table.repositionNow = true;
                    table.Reposition();
                }
            }

            foreach (var scrollView in panel.GetComponentsInChildren<UIScrollView>(true))
            {
                if (scrollView != null)
                {
                    scrollView.UpdateScrollbars(true);
                }
            }
        }

        private static void RestoreCategoryBlueprintsIfEmpty(
            Panel_Crafting panel,
            Il2CppSystem.Collections.Generic.List<BlueprintData> filtered,
            string query)
        {
            if (panel == null || filtered == null || !string.IsNullOrEmpty(query))
                return;

            if (filtered.Count > 0)
                return;

            var allBlueprints = panel.m_Blueprints;
            if (allBlueprints == null || allBlueprints.Count == 0)
                return;

            object selectedCategory = null;
            foreach (string memberName in new[] { "m_SelectedCategory", "m_CurrentCategory", "m_Category", "SelectedCategory", "CurrentCategory" })
            {
                var field = typeof(Panel_Crafting).GetField(memberName, InstanceMemberFlags);
                if (field != null)
                {
                    try { selectedCategory = field.GetValue(panel); break; } catch { }
                }

                var prop = typeof(Panel_Crafting).GetProperty(memberName, InstanceMemberFlags);
                if (prop != null && prop.GetIndexParameters().Length == 0)
                {
                    try { selectedCategory = prop.GetValue(panel); break; } catch { }
                }
            }

            filtered.Clear();

            for (int i = 0; i < allBlueprints.Count; i++)
            {
                var bp = allBlueprints[i];
                if (bp == null)
                    continue;

                if (selectedCategory != null)
                {
                    bool categoryMatches = false;
                    foreach (string bpCatMember in new[] { "m_Category", "Category", "m_CraftingCategory", "CraftingCategory" })
                    {
                        var bpField = bp.GetType().GetField(bpCatMember, InstanceMemberFlags);
                        if (bpField != null)
                        {
                            try
                            {
                                var val = bpField.GetValue(bp);
                                if (val != null && (Equals(val, selectedCategory) || string.Equals(val.ToString(), selectedCategory.ToString(), System.StringComparison.OrdinalIgnoreCase)))
                                {
                                    categoryMatches = true;
                                    break;
                                }
                            }
                            catch { }
                        }

                        var bpProp = bp.GetType().GetProperty(bpCatMember, InstanceMemberFlags);
                        if (bpProp != null && bpProp.GetIndexParameters().Length == 0)
                        {
                            try
                            {
                                var val = bpProp.GetValue(bp);
                                if (val != null && (Equals(val, selectedCategory) || string.Equals(val.ToString(), selectedCategory.ToString(), System.StringComparison.OrdinalIgnoreCase)))
                                {
                                    categoryMatches = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (!categoryMatches)
                        continue;
                }

                filtered.Add(bp);
            }
        }

        private static void SetDetailPaneBlank(Panel_Crafting panel, bool blank)
        {
            if (panel == null)
                return;

            Transform searchBarGo = SearchBarUI.SearchInput != null
                ? SearchBarUI.SearchInput.transform
                : null;

            foreach (var t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t == null)
                    continue;

                if (searchBarGo != null && (t == searchBarGo || t.IsChildOf(searchBarGo)))
                    continue;

                if (t.name.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Contains("Button_", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string n = t.gameObject.name;
                string parentName = t.parent != null ? t.parent.gameObject.name : string.Empty;

                bool isDetailOrRequirementTarget =
                    n.Equals("BlueprintItemRoot", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("ItemName", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("ItemDescription", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftMaterial 1", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftMaterial 2", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftMaterial 3", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftMaterial 4", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftLocation", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftTime", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftTool", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftMultiTool", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftPhotoSelect", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftQuantitySelect", System.StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RequirementCraftTimeSelect", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.Equals("ItemStats", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.Equals("ClothingOnly", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.Equals("HoursRemaining", System.StringComparison.OrdinalIgnoreCase) ||
                    parentName.Equals("HoursToCraft", System.StringComparison.OrdinalIgnoreCase);

                if (isDetailOrRequirementTarget)
                {
                    if (blank)
                    {
                        t.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (!n.StartsWith("RequirementCraftMaterial ", System.StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(" 1", System.StringComparison.OrdinalIgnoreCase))
                        {
                            t.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }
    }
}
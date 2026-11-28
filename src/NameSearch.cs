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

        private static bool _hadSearchQuery;
        private static bool _applying;

        private static System.Reflection.MethodInfo _refreshAvailMethod;
        private static bool _refreshAvailResolved;

        // category list captured from the game's ApplyFilter, so typing can filter without
        // calling ApplyFilter again (that retriggers this postfix and costs ~30 ms of game time)
        private static readonly System.Collections.Generic.List<BlueprintData> _categorySnapshot = new();
        private static int _snapPanelId;
        private static int _snapCategory;
        private static int _snapFilter;

        private static string _lastQuery = string.Empty;
        private static int _lastResultCount = -1;
        private static int _lastResultFirst;
        private static int _lastResultLast;
        private static int _layoutPanelId;
        private static UIGrid[] _layoutGrids = System.Array.Empty<UIGrid>();
        private static UITable[] _layoutTables = System.Array.Empty<UITable>();
        private static UIScrollView[] _layoutScrollViews = System.Array.Empty<UIScrollView>();

        // the detail/requirement objects are fixed in the panel prefab, so find them once per panel
        // instead of walking every transform and marshalling names on each filter pass
        private static int _detailTargetsPanelId;
        private static readonly System.Collections.Generic.List<(GameObject go, bool showOnRestore)> _detailTargets = new();

        internal static void SetNativeInputLock(bool locked) =>
            InputLock.SetNativeInputLock(locked);

        // called from the game's ApplyFilter postfix
        internal static void ApplySearchFilter(Panel_Crafting panel) =>
            ApplySearchFilter(panel, fromGameApplyFilter: true);

        // called from the search box onChange; filters the snapshotted category list in place
        internal static void ApplySearchFromQuery(Panel_Crafting panel) =>
            ApplySearchFilter(panel, fromGameApplyFilter: false);

        private static void ApplySearchFilter(Panel_Crafting panel, bool fromGameApplyFilter)
        {
            if (panel == null || _applying)
                return;

            var filtered = panel.m_FilteredBlueprints;
            if (filtered == null)
                return;

            string query = SearchBarUI.CurrentQuery;
            SearchMode mode = IngredientSearch.ParseQuery(query, out string searchTerm);
            bool hasEffectiveQuery = !string.IsNullOrEmpty(searchTerm);
            bool queryJustCleared = _hadSearchQuery && !hasEffectiveQuery;
            _hadSearchQuery = hasEffectiveQuery;

            int panelId = panel.GetInstanceID();
            int category = (int)panel.m_CurrentCategory;
            int filter = (int)panel.m_CurrentFilter;
            bool snapshotMatches = HasSnapshot(panelId, category, filter);

            if (fromGameApplyFilter)
            {
                if (!hasEffectiveQuery || !snapshotMatches || filtered.Count > _lastResultCount)
                    SaveCategorySnapshot(panel, filtered, panelId, category, filter);

                if (!hasEffectiveQuery)
                {
                    if (queryJustCleared)
                        CommitFilterChange(panel, filtered, string.Empty, blankDetails: false, refreshAvail: true);
                    else
                        RememberResult(string.Empty, filtered);
                    return;
                }
            }
            else if (!snapshotMatches)
            {
                // first keystroke before the game has given us a category list
                panel.ApplyFilter();
                return;
            }

            if (snapshotMatches)
                RestoreFromSnapshot(filtered);

            if (!hasEffectiveQuery)
            {
                if (!queryJustCleared && SameResult(string.Empty, filtered))
                    return;

                CommitFilterChange(panel, filtered, string.Empty, blankDetails: false, refreshAvail: true);
                return;
            }

            SearchCache.EnsureList(panel);
            for (int i = filtered.Count - 1; i >= 0; i--)
            {
                BlueprintData bp = filtered[i];
                if (bp == null || !SearchCache.Matches(bp, mode, searchTerm))
                    filtered.RemoveAt(i);
            }

            if (SameResult(query, filtered))
                return;

            CommitFilterChange(panel, filtered, query, blankDetails: filtered.Count == 0, refreshAvail: false);
        }

        private static void CommitFilterChange(
            Panel_Crafting panel,
            Il2CppSystem.Collections.Generic.List<BlueprintData> filtered,
            string query,
            bool blankDetails,
            bool refreshAvail)
        {
            _applying = true;
            try
            {
                RowSelection.ResetSelectionTracking();
                SetDetailPaneBlank(panel, blankDetails);
                if (refreshAvail)
                {
                    if (!_refreshAvailResolved)
                    {
                        _refreshAvailResolved = true;
                        var m = typeof(Panel_Crafting).GetMethod("RefreshAvailableBlueprints", InstanceMemberFlags);
                        _refreshAvailMethod = m != null && m.GetParameters().Length == 0 ? m : null;
                    }

                    if (_refreshAvailMethod != null)
                    {
                        try { _refreshAvailMethod.Invoke(panel, null); } catch { }
                    }
                }

                RepositionCraftingWidgets(panel);
                ScrollListSync.RefreshCraftingScrollList(panel, filtered);
                RememberResult(query, filtered);
            }
            finally
            {
                _applying = false;
            }
        }

        private static bool HasSnapshot(int panelId, int category, int filter) =>
            _snapPanelId == panelId && _snapCategory == category && _snapFilter == filter && _snapPanelId != 0;

        private static void SaveCategorySnapshot(
            Panel_Crafting panel,
            Il2CppSystem.Collections.Generic.List<BlueprintData> filtered,
            int panelId,
            int category,
            int filter)
        {
            _categorySnapshot.Clear();
            if (filtered != null)
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    var bp = filtered[i];
                    if (bp != null)
                        _categorySnapshot.Add(bp);
                }
            }

            _snapPanelId = panelId;
            _snapCategory = category;
            _snapFilter = filter;
        }

        private static void RestoreFromSnapshot(Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            filtered.Clear();
            for (int i = 0; i < _categorySnapshot.Count; i++)
            {
                var bp = _categorySnapshot[i];
                if (bp != null)
                    filtered.Add(bp);
            }
        }

        private static void RememberResult(string query, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            _lastQuery = query ?? string.Empty;
            _lastResultCount = filtered != null ? filtered.Count : 0;
            _lastResultFirst = _lastResultCount > 0 && filtered[0] != null ? filtered[0].GetInstanceID() : 0;
            _lastResultLast = _lastResultCount > 0 && filtered[_lastResultCount - 1] != null
                ? filtered[_lastResultCount - 1].GetInstanceID()
                : 0;
        }

        private static bool SameResult(string query, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            int count = filtered != null ? filtered.Count : 0;
            int first = count > 0 && filtered[0] != null ? filtered[0].GetInstanceID() : 0;
            int last = count > 0 && filtered[count - 1] != null ? filtered[count - 1].GetInstanceID() : 0;
            return (query ?? string.Empty) == _lastQuery
                && count == _lastResultCount
                && first == _lastResultFirst
                && last == _lastResultLast;
        }

        private static void RepositionCraftingWidgets(Panel_Crafting panel)
        {
            if (panel == null || panel.gameObject == null || !panel.IsEnabled())
                return;

            if (_layoutPanelId != panel.GetInstanceID() || LayoutCacheStale())
            {
                _layoutPanelId = panel.GetInstanceID();
                _layoutGrids = panel.GetComponentsInChildren<UIGrid>(true);
                _layoutTables = panel.GetComponentsInChildren<UITable>(true);
                _layoutScrollViews = panel.GetComponentsInChildren<UIScrollView>(true);
            }

            foreach (var grid in _layoutGrids)
            {
                if (grid != null)
                {
                    grid.repositionNow = true;
                    grid.Reposition();
                }
            }

            foreach (var table in _layoutTables)
            {
                if (table != null)
                {
                    table.repositionNow = true;
                    table.Reposition();
                }
            }

            foreach (var scrollView in _layoutScrollViews)
            {
                if (scrollView != null)
                    scrollView.UpdateScrollbars(true);
            }
        }

        private static bool LayoutCacheStale()
        {
            foreach (var grid in _layoutGrids)
            {
                if (grid == null)
                    return true;
            }

            foreach (var table in _layoutTables)
            {
                if (table == null)
                    return true;
            }

            foreach (var scrollView in _layoutScrollViews)
            {
                if (scrollView == null)
                    return true;
            }

            return _layoutGrids.Length == 0 && _layoutTables.Length == 0 && _layoutScrollViews.Length == 0;
        }

        private static void SetDetailPaneBlank(Panel_Crafting panel, bool blank)
        {
            if (panel == null)
                return;

            if (!DetailTargetsValid(panel))
                CollectDetailTargets(panel);

            foreach (var (go, showOnRestore) in _detailTargets)
            {
                if (blank)
                    go.SetActive(false);
                else if (showOnRestore)
                    go.SetActive(true);
            }
        }

        private static bool DetailTargetsValid(Panel_Crafting panel)
        {
            if (_detailTargetsPanelId != panel.GetInstanceID() || _detailTargets.Count == 0)
                return false;

            foreach (var (go, _) in _detailTargets)
            {
                if (go == null)
                    return false;
            }
            return true;
        }

        private static void CollectDetailTargets(Panel_Crafting panel)
        {
            _detailTargets.Clear();
            _detailTargetsPanelId = panel.GetInstanceID();

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
                    _detailTargets.Add((t.gameObject,
                        !n.StartsWith("RequirementCraftMaterial ", System.StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith(" 1", System.StringComparison.OrdinalIgnoreCase)));
                }
            }
        }
    }
}

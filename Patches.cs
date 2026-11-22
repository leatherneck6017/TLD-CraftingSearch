using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.UI.Scroll;
using UnityEngine;

namespace CraftingSearch
{
    [HarmonyPatch(typeof(Panel_Crafting))]
    internal static class Panel_Crafting_Patches
    {
        private static bool _wasTyping;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Panel_Crafting.Enable), [typeof(bool), typeof(bool)])]
        private static bool Enable_Prefix(bool enable)
        {
            if (!enable && SearchBarUI.IsTyping)
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Panel_Crafting.Enable), [typeof(bool), typeof(bool)])]
        private static void Enable_Postfix(Panel_Crafting __instance, bool enable)
        {
            if (enable)
            {
                SearchBarUI.AttachTo(__instance);
            }
            else
            {
                SetNativeInputLock(false);
                _wasTyping = false;
                SearchBarUI.Clear();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Panel_Crafting.ApplyFilter))]
        private static void ApplyFilter_Postfix(Panel_Crafting __instance)
        {
            _lastIndexByBaseName.Clear();
            string query = SearchBarUI.CurrentQuery;
            var filtered = __instance.m_FilteredBlueprints;
            if (filtered == null)
                return;

            int beforeCount = filtered.Count;

            if (!string.IsNullOrEmpty(query))
            {
                for (int i = filtered.Count - 1; i >= 0; i--)
                {
                    BlueprintData bp = filtered[i];
                    if (bp == null)
                    {
                        filtered.RemoveAt(i);
                        continue;
                    }

                    string disp = bp.GetDisplayedNameWithCount() ?? string.Empty;
                    string raw = bp.name ?? string.Empty;
                    bool matches = disp.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                                   raw.Contains(query, System.StringComparison.OrdinalIgnoreCase);

                    if (!matches)
                    {
                        filtered.RemoveAt(i);
                    }
                }
                
                LogPanelFields(__instance);

                if (__instance.gameObject != null && __instance.IsEnabled())
                {
                    foreach (var grid in __instance.GetComponentsInChildren<UIGrid>(true))
                    {
                        if (grid != null)
                        {
                            grid.repositionNow = true;
                            grid.Reposition();
                        }
                    }

                    foreach (var table in __instance.GetComponentsInChildren<UITable>(true))
                    {
                        if (table != null)
                        {
                            table.repositionNow = true;
                            table.Reposition();
                        }
                    }

                    foreach (var scrollView in __instance.GetComponentsInChildren<UIScrollView>(true))
                    {
                        if (scrollView != null)
                        {
                            scrollView.UpdateScrollbars(true);
                        }
                    }
                }
            }

            MelonLoader.MelonLogger.Msg($"[CraftingSearch] ApplyFilter_Postfix query='{query}' count {beforeCount} -> {filtered.Count}");

            __instance.m_NeedsUpdate = true;

            //if (__instance.gameObject != null && __instance.IsEnabled())
            //{
            //    foreach (var grid in __instance.GetComponentsInChildren<UIGrid>(true))
            //    {
            //        if (grid != null)
            //        {
            //            grid.repositionNow = true;
            //            grid.Reposition();
            //        }
            //    }

            //    foreach (var table in __instance.GetComponentsInChildren<UITable>(true))
            //    {
            //        if (table != null)
            //        {
            //            table.repositionNow = true;
            //            table.Reposition();
            //        }
            //    }

            //    foreach (var scrollView in __instance.GetComponentsInChildren<UIScrollView>(true))
            //    {
            //        if (scrollView != null)
            //        {
            //            scrollView.UpdateScrollbars(true);
            //        }
            //    }
            //}

            RefreshCraftingScrollList(__instance, filtered);
        }

        private static readonly System.Collections.Generic.Dictionary<string, int> _lastIndexByBaseName =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        private static string StripTrailingCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string trimmed = text.Trim();
            int openIdx = trimmed.LastIndexOf('(');
            if (openIdx > 0 && trimmed.EndsWith(")"))
            {
                string inside = trimmed.Substring(openIdx + 1, trimmed.Length - openIdx - 2).Trim();
                if (int.TryParse(inside, out _))
                {
                    return trimmed.Substring(0, openIdx).Trim();
                }
            }
            return trimmed;
        }
        // Attempt to synchronize the icon sprite of a crafting row with the corresponding blueprint data
        private static void SyncRowIcon(Transform row, BlueprintData bp)
        {
            if (row == null || bp == null)
                return;

            UITexture iconTexture = null;
            foreach (var tex in row.GetComponentsInChildren<UITexture>(true))
            {
                if (tex != null && string.Equals(tex.gameObject.name, "BlueprintIcon", System.StringComparison.OrdinalIgnoreCase))
                {
                    iconTexture = tex;
                    break;
                }
            }

            if (iconTexture == null)
                return;

            try
            {
                var addrRef = bp.m_CraftingIcon;
                Texture resolvedTex = null;

                if (addrRef != null)
                {
                    var flags = System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance;

                    var assetProp = addrRef.GetType().GetProperty("Asset", flags);
                    if (assetProp != null)
                    {
                        resolvedTex = assetProp.GetValue(addrRef) as Texture;
                    }

                    if (resolvedTex == null)
                    {
                        var loadMethod = addrRef.GetType().GetMethod("LoadAssetAsync", flags, null, System.Type.EmptyTypes, null);
                        var op = loadMethod?.Invoke(addrRef, null);
                        if (op != null)
                        {
                            var waitMethod = op.GetType().GetMethod("WaitForCompletion", flags, null, System.Type.EmptyTypes, null);
                            object completed = waitMethod != null
                                ? waitMethod.Invoke(op, null)
                                : op.GetType().GetProperty("Result", flags)?.GetValue(op);
                            resolvedTex = completed as Texture;
                        }
                    }
                }

                if (resolvedTex != null)
                {
                    iconTexture.mainTexture = resolvedTex;
                    iconTexture.enabled = true;
                    iconTexture.gameObject.SetActive(true);
                    iconTexture.MarkAsChanged();
                }
            }
            catch
            {
                // Ignore if Addressables asset isn't loaded or signature differs
            }
        }

        // HELPER: Attempt to refresh the crafting scroll list and re-select the first matching blueprint if available
        private static void RefreshCraftingScrollList(Panel_Crafting panel, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            if (panel == null)
                return;

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            var refreshSelMethod = typeof(Panel_Crafting).GetMethod("RefreshSelectedBlueprint", flags);
            if (refreshSelMethod != null)
            {
                var parms = refreshSelMethod.GetParameters();
                if (parms.Length == 1)
                {
                    try
                    {
                        if (parms[0].ParameterType == typeof(int) && filtered != null && filtered.Count > 0)
                        {
                            refreshSelMethod.Invoke(panel, [0]);
                        }
                        else if (parms[0].ParameterType == typeof(BlueprintData) && filtered != null && filtered.Count > 0)
                        {
                            refreshSelMethod.Invoke(panel, [filtered[0]]);
                        }
                    }
                    catch
                    {
                        // Ignore if signature or state doesn't match
                    }
                }

                foreach (var widget in panel.GetComponentsInChildren<UIWidget>(true))
                {
                    if (widget == null) continue;
                    string path = widget.transform != null ? widget.transform.name : string.Empty;
                    if (path.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        widget.MarkAsChanged();
                    }
                }

                SyncBlueprintListRows(panel, filtered);

            }

            foreach (var widget in panel.GetComponentsInChildren<UIWidget>(true))
            {
                if (widget == null) continue;
                string path = widget.transform != null ? widget.transform.name : string.Empty;
                if (path.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    widget.MarkAsChanged();
                }
            }
        }

        private static bool _loggedIconInfo;

        // HELPER: Attempt to log diagnostic information about the icon sprite and blueprint data for a given row
        private static void LogIconDiagnostic(Transform row, BlueprintData bp)
        {
            if (_loggedIconInfo || row == null || bp == null)
                return;

            _loggedIconInfo = true;
            MelonLoader.MelonLogger.Msg($"[CraftingSearch] IconDiag bp.name='{bp.name}' disp='{bp.GetDisplayedNameWithCount()}'");

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            foreach (var p in typeof(BlueprintData).GetProperties(flags))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object val = null;
                try { val = p.GetValue(bp); } catch { }
                string valStr = val != null ? val.ToString() : "null";
                MelonLoader.MelonLogger.Msg($"[CraftingSearch]   bp prop {p.Name} ({p.PropertyType.Name}) = {valStr}");
            }

            foreach (var f in typeof(BlueprintData).GetFields(flags))
            {
                object val = null;
                try { val = f.GetValue(bp); } catch { }
                string valStr = val != null ? val.ToString() : "null";
                MelonLoader.MelonLogger.Msg($"[CraftingSearch]   bp field {f.Name} ({f.FieldType.Name}) = {valStr}");
            }

            foreach (var tex in row.GetComponentsInChildren<UITexture>(true))
            {
                if (tex == null) continue;
                string mTexName = tex.mainTexture != null ? tex.mainTexture.name : "null";
                MelonLoader.MelonLogger.Msg($"[CraftingSearch]   row UITexture '{tex.gameObject.name}' mainTexture='{mTexName}'");
            }
        }

        // HELPER: Attempt to select a blueprint by index, with fallback to refresh the list and re-select if necessary
        private static void SelectBlueprintByIndex(Panel_Crafting panel, int blueprintIndex)
        {
            if (panel == null || blueprintIndex < 0)
                return;

            var list = panel.m_FilteredBlueprints ?? panel.m_Blueprints;
            if (list == null || blueprintIndex >= list.Count)
                return;

            try
            {
                BlueprintData bp = list[blueprintIndex];
                string selectedDisp = bp != null ? (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim() : string.Empty;
                string selectedRaw = bp != null ? (bp.name ?? string.Empty).Trim() : string.Empty;
                MelonLoader.MelonLogger.Msg($"[CraftingSearch] Selected blueprint idx={blueprintIndex} raw='{selectedRaw}' disp='{selectedDisp}'");

                panel.RefreshSelectedBlueprint(blueprintIndex);
                panel.m_NeedsUpdate = true;

                var flags = System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance;

                foreach (string methodName in new[] { "UpdateSelected", "RefreshAvailableBlueprints" })
                {
                    var m = typeof(Panel_Crafting).GetMethod(methodName, flags);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        try { m.Invoke(panel, null); } catch { }
                    }
                }

                foreach (var widget in panel.GetComponentsInChildren<UIWidget>(true))
                {
                    if (widget != null)
                    {
                        widget.MarkAsChanged();
                    }
                }
            }
            catch
            {
                // Ignore if IL2CPP signature or state doesn't match
            }
        }
        // Attempt to bind a click handler to a row that selects the corresponding blueprint index
        private static void BindRowIndexClickHandler(Panel_Crafting panel, Transform row, int blueprintIndex)
        {
            if (panel == null || row == null || blueprintIndex < 0)
                return;

            var collider = row.GetComponent<UnityEngine.BoxCollider>();
            if (collider == null)
            {
                var widget = row.GetComponent<UIWidget>();
                collider = row.gameObject.AddComponent<UnityEngine.BoxCollider>();
                if (widget != null)
                {
                    collider.size = new UnityEngine.Vector3(widget.width, widget.height, 1f);
                    collider.center = new UnityEngine.Vector3(widget.pivotOffset.x * -widget.width, widget.pivotOffset.y * -widget.height, 0f);
                }
            }
            collider.enabled = true;

            var listener = row.GetComponent<UIEventListener>() ?? row.gameObject.AddComponent<UIEventListener>();
            listener.onClick = null;
            listener.onSelect = null;

            int capturedIndex = blueprintIndex;

            listener.onClick = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UIEventListener.VoidDelegate>(
                new System.Action<UnityEngine.GameObject>((_) =>
                {
                    var activeList = panel.m_FilteredBlueprints ?? panel.m_Blueprints;
                    int resolvedIndex = ResolveBlueprintIndexForRow(panel, row, activeList);
                    int targetIndex = resolvedIndex >= 0 ? resolvedIndex : capturedIndex;
                    SelectBlueprintByIndex(panel, targetIndex);
                })
            );
        }

        // Attempt to resolve the blueprint index for a given row based on its labels and the active blueprint list
        private static int ResolveBlueprintIndexForRow(
                    Panel_Crafting panel,
                    UnityEngine.Transform row,
                    Il2CppSystem.Collections.Generic.List<BlueprintData> list)
        {
            if (panel == null || row == null || list == null || list.Count == 0)
                return -1;

            var rowLabels = new System.Collections.Generic.List<string>();
            foreach (var lbl in row.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl == null) continue;
                string text = (lbl.text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    rowLabels.Add(text);
                }
            }

            int count = list.Count;
            int startIndex = GetApproxScrollListIndex(panel, count);
            string primaryLabel = rowLabels.Count > 0 ? rowLabels[0] : string.Empty;
            string primaryBase = StripTrailingCount(primaryLabel);

            int searchStart = startIndex;
            if (!string.IsNullOrEmpty(primaryBase) && _lastIndexByBaseName.TryGetValue(primaryBase, out int prevIdx) && prevIdx >= 0 && prevIdx < count)
            {
                searchStart = (prevIdx + 1) % count;
            }

            // Pass 1A: Exact label or raw name match starting from searchStart
            if (!string.IsNullOrEmpty(primaryLabel))
            {
                for (int offset = 0; offset < count; offset++)
                {
                    int idx = (searchStart + offset) % count;
                    BlueprintData bp = list[idx];
                    if (bp == null) continue;

                    string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                    string raw = (bp.name ?? string.Empty).Trim();

                    if (string.Equals(disp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(raw, primaryLabel, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(primaryBase))
                            _lastIndexByBaseName[primaryBase] = idx;
                        return idx;
                    }
                }
            }

            // Pass 1B: Count-stripped base name match starting from searchStart
            if (!string.IsNullOrEmpty(primaryBase))
            {
                for (int offset = 0; offset < count; offset++)
                {
                    int idx = (searchStart + offset) % count;
                    BlueprintData bp = list[idx];
                    if (bp == null) continue;

                    string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                    string dispBase = StripTrailingCount(disp);
                    string raw = (bp.name ?? string.Empty).Trim();

                    if (string.Equals(dispBase, primaryBase, System.StringComparison.OrdinalIgnoreCase) ||
                        raw.Contains(primaryBase.Replace(" ", string.Empty), System.StringComparison.OrdinalIgnoreCase))
                    {
                        _lastIndexByBaseName[primaryBase] = idx;
                        return idx;
                    }
                }
            }

            // Pass 2: Fallback across remaining labels on the row
            for (int offset = 0; offset < count; offset++)
            {
                int idx = (searchStart + offset) % count;
                BlueprintData bp = list[idx];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string dispBase = StripTrailingCount(disp);
                string raw = (bp.name ?? string.Empty).Trim();

                foreach (string lbl in rowLabels)
                {
                    string lblBase = StripTrailingCount(lbl);
                    if (string.Equals(disp, lbl, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(raw, lbl, System.StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(lblBase) && string.Equals(dispBase, lblBase, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!string.IsNullOrEmpty(lblBase))
                            _lastIndexByBaseName[lblBase] = idx;
                        return idx;
                    }
                }
            }

            // Pass 3: Substring fallback for modded or custom-formatted names
            for (int offset = 0; offset < count; offset++)
            {
                int idx = (searchStart + offset) % count;
                BlueprintData bp = list[idx];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                foreach (string lbl in rowLabels)
                {
                    string lblBase = StripTrailingCount(lbl);
                    if (disp.Contains(lblBase, System.StringComparison.OrdinalIgnoreCase) ||
                        raw.Contains(lblBase, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return idx;
                    }
                }
            }

            return -1;
        }
        // Attempt to estimate the scroll list index based on the current scroll position and list count
        private static int GetApproxScrollListIndex(Panel_Crafting panel, int listCount)
        {
            if (panel == null || listCount <= 1)
                return 0;

            try
            {
                var scrollView = panel.GetComponentInChildren<UIScrollView>(true);
                if (scrollView != null)
                {
                    if (scrollView.verticalScrollBar != null)
                    {
                        float val = UnityEngine.Mathf.Clamp01(scrollView.verticalScrollBar.value);
                        int maxStart = UnityEngine.Mathf.Max(0, listCount - 1);
                        return UnityEngine.Mathf.Clamp(
                            UnityEngine.Mathf.RoundToInt(val * maxStart),
                            0,
                            maxStart
                        );
                    }

                    // Fallback using UIScrollView localPosition Y offset scaled by list size
                    float localY = UnityEngine.Mathf.Abs(scrollView.transform.localPosition.y);
                    float approxTotalHeight = UnityEngine.Mathf.Max(600f, listCount * 58f);
                    if (localY > 0.01f)
                    {
                        float normalized = UnityEngine.Mathf.Clamp01(localY / approxTotalHeight);
                        int maxStart = UnityEngine.Mathf.Max(0, listCount - 1);
                        return UnityEngine.Mathf.Clamp(
                            UnityEngine.Mathf.RoundToInt(normalized * maxStart),
                            0,
                            maxStart
                        );
                    }
                }
            }
            catch
            {
                // Ignore if scroll components are unavailable
            }

            return 0;
        }
        // Attempt to find the next matching blueprint index based on row labels, query, and used indices
        private static int FindNextMatchingBlueprintIndex(
            Il2CppSystem.Collections.Generic.List<BlueprintData> list,
            System.Collections.Generic.List<string> rowLabels,
            string query,
            bool hasQuery,
            System.Collections.Generic.HashSet<int> usedIndices,
            int startIndex)
        {
            if (list == null || list.Count == 0)
                return -1;

            int count = list.Count;
            int safeStart = ((startIndex % count) + count) % count;
            string primaryLabel = rowLabels != null && rowLabels.Count > 0 ? rowLabels[0] : string.Empty;

            // Pass 1A: Exact or prefix match on primary row label starting from scroll offset
            if (!string.IsNullOrEmpty(primaryLabel))
            {
                for (int offset = 0; offset < count; offset++)
                {
                    int i = (safeStart + offset) % count;
                    if (usedIndices.Contains(i)) continue;
                    BlueprintData bp = list[i];
                    if (bp == null) continue;

                    string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                    string raw = (bp.name ?? string.Empty).Trim();

                    bool primaryMatch = string.Equals(disp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(raw, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                                        disp.StartsWith(primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                                        primaryLabel.StartsWith(disp, System.StringComparison.OrdinalIgnoreCase);

                    if (!primaryMatch) continue;

                    bool queryOk = !hasQuery ||
                                   disp.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                                   raw.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                                   primaryLabel.Contains(query, System.StringComparison.OrdinalIgnoreCase);

                    if (queryOk)
                    {
                        usedIndices.Add(i);
                        return i;
                    }
                }
            }

            // Pass 1B: Exact or prefix match across row labels starting from scroll offset
            for (int offset = 0; offset < count; offset++)
            {
                int i = (safeStart + offset) % count;
                if (usedIndices.Contains(i)) continue;
                BlueprintData bp = list[i];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                bool labelOk = rowLabels == null || rowLabels.Count == 0;
                if (rowLabels != null)
                {
                    foreach (string lbl in rowLabels)
                    {
                        if (string.Equals(disp, lbl, System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(raw, lbl, System.StringComparison.OrdinalIgnoreCase) ||
                            disp.StartsWith(lbl, System.StringComparison.OrdinalIgnoreCase) ||
                            lbl.StartsWith(disp, System.StringComparison.OrdinalIgnoreCase))
                        {
                            labelOk = true;
                            break;
                        }
                    }
                }

                if (!labelOk) continue;

                bool queryOk = !hasQuery ||
                               disp.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                               raw.Contains(query, System.StringComparison.OrdinalIgnoreCase);
                if (!queryOk && rowLabels != null)
                {
                    foreach (string lbl in rowLabels)
                    {
                        if (lbl.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                        {
                            queryOk = true;
                            break;
                        }
                    }
                }

                if (queryOk)
                {
                    usedIndices.Add(i);
                    return i;
                }
            }

            // Pass 2: Substring fallback starting from scroll offset
            for (int offset = 0; offset < count; offset++)
            {
                int i = (safeStart + offset) % count;
                if (usedIndices.Contains(i)) continue;
                BlueprintData bp = list[i];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                bool labelOk = rowLabels == null || rowLabels.Count == 0;
                if (rowLabels != null)
                {
                    foreach (string lbl in rowLabels)
                    {
                        if (disp.Contains(lbl, System.StringComparison.OrdinalIgnoreCase) ||
                            raw.Contains(lbl, System.StringComparison.OrdinalIgnoreCase))
                        {
                            labelOk = true;
                            break;
                        }
                    }
                }

                if (!labelOk) continue;

                bool queryOk = !hasQuery ||
                               disp.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                               raw.Contains(query, System.StringComparison.OrdinalIgnoreCase);
                if (!queryOk && rowLabels != null)
                {
                    foreach (string lbl in rowLabels)
                    {
                        if (lbl.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                        {
                            queryOk = true;
                            break;
                        }
                    }
                }

                if (queryOk)
                {
                    usedIndices.Add(i);
                    return i;
                }
            }

            return -1;
        }
        // Debugging utility to log all tinder-related indices in the active blueprint list
        private static bool _loggedTinderIndices;
        // 
        private static void LogTinderIndices(Il2CppSystem.Collections.Generic.List<BlueprintData> list)
        {
            if (_loggedTinderIndices || list == null)
                return;

            _loggedTinderIndices = true;
            for (int i = 0; i < list.Count; i++)
            {
                var bp = list[i];
                if (bp == null) continue;

                string disp = bp.GetDisplayedNameWithCount() ?? string.Empty;
                string raw = bp.name ?? string.Empty;
                if (disp.Contains("tinder", System.StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("tinder", System.StringComparison.OrdinalIgnoreCase))
                {
                    MelonLoader.MelonLogger.Msg($"[CraftingSearch] activeList[{i}] raw='{raw}' disp='{disp}'");
                }
            }
        }
        // Attempt to synchronize the visibility and position of blueprint list rows based on the filtered results
        private static void SyncBlueprintListRows(Panel_Crafting panel, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            if (panel == null || filtered == null)
                return;

            var allowedNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < filtered.Count; i++)
            {
                var bp = filtered[i];
                if (bp == null) continue;

                string disp = bp.GetDisplayedNameWithCount();
                if (!string.IsNullOrWhiteSpace(disp))
                    allowedNames.Add(disp.Trim());

                if (!string.IsNullOrWhiteSpace(bp.name))
                    allowedNames.Add(bp.name.Trim());
            }

            string query = SearchBarUI.CurrentQuery;
            bool hasQuery = !string.IsNullOrEmpty(query);

            var scrollBehaviour = panel.GetComponentInChildren<ScrollBehaviour>(true);
            if (scrollBehaviour == null)
                return;

            Transform listRoot = scrollBehaviour.transform;

            var rowTransforms = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < listRoot.childCount; i++)
            {
                var child = listRoot.GetChild(i);
                if (child == null) continue;

                string childName = child.gameObject.name ?? string.Empty;
                if (childName.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    rowTransforms.Add(child);
                }
            }

            if (rowTransforms.Count == 0)
                return;

            float topY = rowTransforms[0].localPosition.y;
            float stepY = rowTransforms.Count > 1
                ? UnityEngine.Mathf.Abs(rowTransforms[1].localPosition.y - rowTransforms[0].localPosition.y)
                : 64f;
            if (stepY < 1f) stepY = 64f;

            var activeList = panel.m_FilteredBlueprints ?? panel.m_Blueprints;
            int activeCount = activeList != null ? activeList.Count : -1;
            int startIndex = GetApproxScrollListIndex(panel, activeCount);
            LogTinderIndices(activeList);
            MelonLoader.MelonLogger.Msg($"[CraftingSearch] Sync rows={rowTransforms.Count}, activeList={activeCount}, query='{query}'");
            
            var usedIndices = new System.Collections.Generic.HashSet<int>();
            var matchingRows = new System.Collections.Generic.List<Transform>();

            foreach (var row in rowTransforms)
            {
                var rowLabels = new System.Collections.Generic.List<string>();
                foreach (var lbl in row.GetComponentsInChildren<UILabel>(true))
                {
                    if (lbl == null) continue;
                    string t = (lbl.text ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        rowLabels.Add(t);
                    }
                }

                int bpIndex = FindNextMatchingBlueprintIndex(activeList, rowLabels, query, hasQuery, usedIndices, startIndex);

                //if (!hasQuery)
                //{
                //    if (bpIndex >= 0)
                //    {
                //        BindRowIndexClickHandler(panel, row, bpIndex);
                //    }
                //    row.gameObject.SetActive(true);
                //    matchingRows.Add(row);
                //    continue;
                //}

                bool rowMatches = bpIndex >= 0;
                if (rowMatches)
                {
                    BindRowIndexClickHandler(panel, row, bpIndex);
                }

                row.gameObject.SetActive(rowMatches);
                if (rowMatches)
                {
                    matchingRows.Add(row);
                }
            }

            for (int i = 0; i < matchingRows.Count; i++)
            {
                var r = matchingRows[i];
                var pos = r.localPosition;
                pos.y = topY - (i * stepY);
                r.localPosition = pos;
            }

            if (!hasQuery)
            {
                usedIndices = new System.Collections.Generic.HashSet<int>();

                foreach (var row in rowTransforms)
                {
                    var rowLabels = new System.Collections.Generic.List<string>();
                    foreach (var lbl in row.GetComponentsInChildren<UILabel>(true))
                    {
                        if (lbl == null) continue;
                        string t = (lbl.text ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(t))
                        {
                            rowLabels.Add(t);
                        }
                    }

                    int bpIndex = FindNextMatchingBlueprintIndex(activeList, rowLabels, query, false, usedIndices, startIndex);
                    if (bpIndex >= 0)
                    {
                        BindRowIndexClickHandler(panel, row, bpIndex);
                    }
                    row.gameObject.SetActive(true);
                }
                return;
            }

            // When a search query is active, populate visible rows directly from the filtered list
            int visibleCount = UnityEngine.Mathf.Min(rowTransforms.Count, filtered.Count);
            for (int i = 0; i < rowTransforms.Count; i++)
            {
                var row = rowTransforms[i];
                if (row == null) continue;

                if (i < visibleCount)
                {
                    BlueprintData bp = filtered[i];
                    string disp = bp != null ? (bp.GetDisplayedNameWithCount() ?? bp.name ?? string.Empty).Trim() : string.Empty;
                    if (i == 0)
                    {
                        LogIconDiagnostic(row, bp);
                    }
                    var labels = row.GetComponentsInChildren<UILabel>(true);
                    if (labels != null && labels.Length > 0 && !string.IsNullOrEmpty(disp))
                    {
                        labels[0].text = disp;
                        labels[0].gameObject.SetActive(true);
                    }

                    SyncRowIcon(row, bp);

                    var pos = row.localPosition;
                    pos.y = topY - (i * stepY);
                    row.localPosition = pos;

                    row.gameObject.SetActive(true);
                    BindRowIndexClickHandler(panel, row, i);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }

            foreach (var scrollView in scrollBehaviour.GetComponentsInChildren<UIScrollView>(true))
            {
                if (scrollView != null)
                {
                    scrollView.currentMomentum = UnityEngine.Vector3.zero;
                    scrollView.ResetPosition();
                    scrollView.UpdateScrollbars(true);
                }
            }
        }
        // Attempt to lock or unlock native input handling when the search bar is active
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static bool Update_Prefix()
        {
            bool isTyping = SearchBarUI.IsTyping;
            if (isTyping != _wasTyping)
            {
                _wasTyping = isTyping;
                SetNativeInputLock(isTyping);
            }

            if (isTyping)
            {
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape) ||
                    UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return) ||
                    UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter))
                {
                    SearchBarUI.SearchInput?.isSelected = false;
                }
            }

            // Always return true so Panel_Crafting.Update() processes m_NeedsUpdate live!
            return true;
        }

        private static void SetNativeInputLock(bool locked)
        {
            try
            {
                var reInputType = System.Type.GetType("Il2CppRewired.ReInput, Il2CppRewired_Core");
                if (reInputType == null)
                {
                    // Fallback: search loaded assemblies for ReInput
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        reInputType = asm.GetType("Il2CppRewired.ReInput") ?? asm.GetType("Rewired.ReInput");
                        if (reInputType != null) break;
                    }
                }

                if (reInputType == null)
                {
                    MelonLoader.MelonLogger.Warning("[CraftingSearch] Could not locate ReInput type.");
                    return;
                }

                var playersProp = reInputType.GetProperty("players");
                object players = playersProp?.GetValue(null);
                if (players == null) return;

                var getPlayerMethod = players.GetType().GetMethod("GetPlayer", [typeof(int)]);
                object player = getPlayerMethod?.Invoke(players, [0]);
                if (player == null) return;

                var controllersProp = player.GetType().GetProperty("controllers");
                object controllers = controllersProp?.GetValue(player);
                var mapsProp = controllers?.GetType().GetProperty("maps");
                object maps = mapsProp?.GetValue(controllers);

                // Toggle Keyboard maps (ControllerType.Keyboard = 0): enabled = !locked
                var setMapsEnabled = maps?.GetType().GetMethod("SetMapsEnabled", [typeof(bool), typeof(int)]);
                if (setMapsEnabled != null)
                {
                    setMapsEnabled.Invoke(maps, [!locked, 0]);
                    //MelonLoader.MelonLogger.Msg($"Rewired Keyboard maps enabled = {!locked}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[CraftingSearch] Rewired lock error: {ex.Message}");
            }
        }

        // Debugging utility to log all fields of Panel_Crafting that may be relevant to crafting lists
        //private static void LogListFields(Panel_Crafting panel)
        //{
        //    var flags = System.Reflection.BindingFlags.Public |
        //                System.Reflection.BindingFlags.NonPublic |
        //                System.Reflection.BindingFlags.Instance;

        //    foreach (var field in typeof(Panel_Crafting).GetFields(flags))
        //    {
        //        string fName = field.Name;
        //        if (fName.Contains("Blueprint", System.StringComparison.OrdinalIgnoreCase) ||
        //            fName.Contains("List", System.StringComparison.OrdinalIgnoreCase) ||
        //            fName.Contains("Scroll", System.StringComparison.OrdinalIgnoreCase) ||
        //            fName.Contains("Item", System.StringComparison.OrdinalIgnoreCase))
        //        {
        //            MelonLoader.MelonLogger.Msg($"[CraftingSearch] Panel_Crafting field: {fName} ({field.FieldType.Name})");
        //        }
        //    }
        //}
        // 
        private static bool _loggedFields;

        private static void LogPanelFields(Panel_Crafting panel)
        {
            if (_loggedFields || panel == null)
                return;

            _loggedFields = true;
            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            foreach (var field in typeof(Panel_Crafting).GetFields(flags))
            {
                string fName = field.Name;
                if (fName.Contains("Blueprint", System.StringComparison.OrdinalIgnoreCase) ||
                    fName.Contains("List", System.StringComparison.OrdinalIgnoreCase) ||
                    fName.Contains("Scroll", System.StringComparison.OrdinalIgnoreCase) ||
                    fName.Contains("Filter", System.StringComparison.OrdinalIgnoreCase))
                {
                    MelonLoader.MelonLogger.Msg($"[CraftingSearch] Panel_Crafting field: {fName} ({field.FieldType.Name})");
                }
            }
        }
    }
}
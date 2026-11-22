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
        private static bool _hadSearchQuery = false;
        private static bool _scrollBehaviourPatched;

        private static void EnsureScrollBehaviourPatched()
        {
            if (_scrollBehaviourPatched)
                return;

            _scrollBehaviourPatched = true;
            var harmony = new HarmonyLib.Harmony("leatherneck6017.CraftingSearch.ScrollBehaviour");
            PatchScrollBehaviourScroll(harmony);
        }
        
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
                EnsureScrollBehaviourPatched();
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
            _trackedFirstLineIndex = 0; 
            string query = SearchBarUI.CurrentQuery;
            var filtered = __instance.m_FilteredBlueprints;
            bool isQueryEmpty = string.IsNullOrEmpty(query);
            bool queryJustCleared = _hadSearchQuery && isQueryEmpty;
            _hadSearchQuery = !isQueryEmpty;

            if (queryJustCleared && filtered.Count == 0 && __instance.m_Blueprints != null && __instance.m_Blueprints.Count > 0)
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

                var catFlags = System.Reflection.BindingFlags.Public |
                               System.Reflection.BindingFlags.NonPublic |
                               System.Reflection.BindingFlags.Instance;

                foreach (string methodName in new[] { "FilterByCategory", "ApplyCategoryFilter", "RefreshCategory", "RefreshFilteredBlueprints", "UpdateFilteredBlueprints" })
                {
                    var m = typeof(Panel_Crafting).GetMethod(methodName, catFlags);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        try { m.Invoke(__instance, null); break; } catch { }
                    }
                }
            }

            if (filtered == null)
                return;

            RestoreCategoryBlueprintsIfEmpty(__instance, filtered, query);

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

            __instance.m_NeedsUpdate = true;

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            var refreshAvail = typeof(Panel_Crafting).GetMethod("RefreshAvailableBlueprints", flags);
            if (refreshAvail != null && refreshAvail.GetParameters().Length == 0)
            {
                try { refreshAvail.Invoke(__instance, null); } catch { }
            }

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
            RefreshCraftingScrollList(__instance, filtered);
        }

        private static readonly System.Collections.Generic.Dictionary<string, float> _lastScrollLogTime =
                    new System.Collections.Generic.Dictionary<string, float>();

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

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            object selectedCategory = null;
            foreach (string memberName in new[] { "m_SelectedCategory", "m_CurrentCategory", "m_Category", "SelectedCategory", "CurrentCategory" })
            {
                var field = typeof(Panel_Crafting).GetField(memberName, flags);
                if (field != null)
                {
                    try { selectedCategory = field.GetValue(panel); break; } catch { }
                }

                var prop = typeof(Panel_Crafting).GetProperty(memberName, flags);
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
                        var bpField = bp.GetType().GetField(bpCatMember, flags);
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

                        var bpProp = bp.GetType().GetProperty(bpCatMember, flags);
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
        
        private static void PatchScrollBehaviourScroll(HarmonyLib.Harmony harmony)
        {
            if (harmony == null)
                return;

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.DeclaredOnly;

            var sbType = typeof(ScrollBehaviour);
            var postfixMethod = typeof(Panel_Crafting_Patches).GetMethod(
                nameof(ScrollBehaviourAnyMethod_Postfix),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            if (postfixMethod == null)
                return;

            foreach (var method in sbType.GetMethods(flags))
            {
                if (method == null || method.IsSpecialName)
                    continue;

                string name = method.Name ?? string.Empty;
                if (name.StartsWith("get_", System.StringComparison.Ordinal) ||
                    name.StartsWith("set_", System.StringComparison.Ordinal) ||
                    name.StartsWith("add_", System.StringComparison.Ordinal) ||
                    name.StartsWith("remove_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    harmony.Patch(method, postfix: new HarmonyLib.HarmonyMethod(postfixMethod));
                }
                catch
                {
                    // Skip methods Harmony cannot patch in IL2CPP
                }
            }
        }
        private static int _trackedFirstLineIndex = 0;

        private static int ReadScrollBehaviourFirstLine(ScrollBehaviour sb)
        {
            if (sb == null)
                return -1;

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            var type = sb.GetType();
            foreach (string memberName in new[] { "FirstLine", "m_FirstLine", "m_TopIndex", "TopIndex", "m_StartIndex", "StartIndex" })
            {
                var prop = type.GetProperty(memberName, flags);
                if (prop != null && prop.PropertyType == typeof(int) && prop.GetIndexParameters().Length == 0)
                {
                    try { return (int)prop.GetValue(sb); } catch { }
                }

                var field = type.GetField(memberName, flags);
                if (field != null && field.FieldType == typeof(int))
                {
                    try { return (int)field.GetValue(sb); } catch { }
                }
            }

            return -1;
        }

        private static void ScrollBehaviourAnyMethod_Postfix(ScrollBehaviour __instance, System.Reflection.MethodBase __originalMethod)
        {
            if (__instance == null || __originalMethod == null)
                return;

            string methodName = __originalMethod.Name ?? "Unknown";

            if (string.Equals(methodName, "UpdateFirstLine", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(methodName, "RefreshItems", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(methodName, "OnScrollBarChange", System.StringComparison.OrdinalIgnoreCase))
            {
                int line = ReadScrollBehaviourFirstLine(__instance);
                if (line >= 0)
                {
                    _trackedFirstLineIndex = line;
                }
            }

            if (string.Equals(methodName, "HandleOnItemSelected", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(methodName, "SetSelectedIndex", System.StringComparison.OrdinalIgnoreCase))
            {
                var panel = __instance.GetComponentInParent<Panel_Crafting>();
                if (panel != null && !string.IsNullOrEmpty(SearchBarUI.CurrentQuery))
                {
                    panel.m_NeedsUpdate = true;
                }
            }
            
            float now = UnityEngine.Time.unscaledTime;
            if (_lastScrollLogTime.TryGetValue(methodName, out float lastTime) && (now - lastTime) < 2.0f)
                return;

            _lastScrollLogTime[methodName] = now;
            MelonLoader.MelonLogger.Msg($"[CraftingSearch] ScrollBehaviour.{methodName} invoked (firstLine={_trackedFirstLineIndex})");
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
                if (addrRef == null)
                    return;

                var flags = System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance;

                Texture resolvedTex = null;
                var assetProp = addrRef.GetType().GetProperty("Asset", flags);
                if (assetProp != null)
                {
                    resolvedTex = assetProp.GetValue(addrRef) as Texture;
                }

                if (resolvedTex != null && iconTexture.mainTexture != resolvedTex)
                {
                    iconTexture.mainTexture = resolvedTex;
                    iconTexture.MarkAsChanged();
                }
            }
            catch
            {
                // Ignore if Addressables asset is not loaded yet
            }
        }

        private static readonly System.Collections.Generic.HashSet<int> _hookedScrollViews =
            new System.Collections.Generic.HashSet<int>();

        private static void HookCraftingScrollViews(Panel_Crafting panel)
        {
            if (panel == null)
                return;

            foreach (var sv in panel.GetComponentsInChildren<UIScrollView>(true))
            {
                if (sv == null)
                    continue;

                int id = sv.GetInstanceID();
                if (_hookedScrollViews.Contains(id))
                    continue;

                _hookedScrollViews.Add(id);

                var onScrollAction = new System.Action(() =>
                {
                    MelonLoader.MelonLogger.Msg($"[CraftingSearch] Game scroll event fired on '{sv.name}'");
                    var filtered = panel.m_FilteredBlueprints;
                    if (filtered != null && !string.IsNullOrEmpty(SearchBarUI.CurrentQuery))
                    {
                        SyncBlueprintListRows(panel, filtered);
                    }
                });

                var del = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UIScrollView.OnDragNotification>(onScrollAction);
                sv.onMomentumMove += del;
                sv.onDragFinished += del;
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
        private static void SelectBlueprintByIndex(Panel_Crafting panel, int blueprintIndex)
        {
            if (panel == null || blueprintIndex < 0)
                return;

            var filteredList = panel.m_FilteredBlueprints;
            var allList = panel.m_Blueprints;
            var sourceList = filteredList ?? allList;
            if (sourceList == null || blueprintIndex >= sourceList.Count)
                return;

            try
            {
                BlueprintData bp = sourceList[blueprintIndex];
                if (bp == null)
                    return;

                string selectedDisp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string selectedRaw = (bp.name ?? string.Empty).Trim();

                panel.RefreshSelectedBlueprint(blueprintIndex);

                var flags = System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance;

                var sb = panel.GetComponentInChildren<ScrollBehaviour>(true);
                if (sb != null)
                {
                    var setSelMethod = sb.GetType().GetMethod("SetSelectedIndex", flags, null, [typeof(int)], null);
                    if (setSelMethod != null)
                    {
                        try { setSelMethod.Invoke(sb, [blueprintIndex]); } catch { }
                    }
                }

                var refreshAvail = typeof(Panel_Crafting).GetMethod("RefreshAvailableBlueprints", flags);
                if (refreshAvail != null && refreshAvail.GetParameters().Length == 0)
                {
                    try { refreshAvail.Invoke(panel, null); } catch { }
                }

                panel.m_NeedsUpdate = true;

                MelonLoader.MelonLogger.Msg($"[CraftingSearch] Select directIdx={blueprintIndex}/{sourceList.Count} raw='{selectedRaw}' disp='{selectedDisp}'");

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
        private static int GetCurrentScrollOffset(Panel_Crafting panel, int totalItems, int visibleSlots)
        {
            if (panel == null || totalItems <= 1)
                return 0;

            try
            {
                var scrollView = panel.GetComponentInChildren<UIScrollView>(true);
                if (scrollView != null)
                {
                    // 1. Check clipOffset.y first (moves as rows scroll past the initial 7 slots)
                    if (scrollView.panel != null)
                    {
                        float clipY = UnityEngine.Mathf.Abs(scrollView.panel.clipOffset.y);
                        if (clipY > 0.5f)
                        {
                            float rowStep = 60f;
                            var scrollBehaviour = panel.GetComponentInChildren<ScrollBehaviour>(true);
                            if (scrollBehaviour != null)
                            {
                                var rowTransforms = new System.Collections.Generic.List<Transform>();
                                for (int i = 0; i < scrollBehaviour.transform.childCount; i++)
                                {
                                    var child = scrollBehaviour.transform.GetChild(i);
                                    if (child != null && child.gameObject.name.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        rowTransforms.Add(child);
                                    }
                                }

                                if (rowTransforms.Count > 1)
                                {
                                    float measured = UnityEngine.Mathf.Abs(rowTransforms[1].localPosition.y - rowTransforms[0].localPosition.y);
                                    if (measured >= 10f) rowStep = measured;
                                }
                            }

                            int offsetFromClip = UnityEngine.Mathf.FloorToInt((clipY + (rowStep * 0.25f)) / rowStep);
                            return UnityEngine.Mathf.Clamp(offsetFromClip, 0, UnityEngine.Mathf.Max(0, totalItems - 1));
                        }
                    }

                    // 2. Fallback to verticalScrollBar.value
                    if (scrollView.verticalScrollBar != null && scrollView.verticalScrollBar.value > 0.0001f)
                    {
                        float val = UnityEngine.Mathf.Clamp01(scrollView.verticalScrollBar.value);
                        int maxOffset = UnityEngine.Mathf.Max(0, totalItems - 1);
                        return UnityEngine.Mathf.Clamp(
                            UnityEngine.Mathf.RoundToInt(val * maxOffset),
                            0,
                            maxOffset
                        );
                    }
                }
            }
            catch
            {
                // Ignore if NGUI scroll components are unavailable
            }
            
            if (_trackedFirstLineIndex > 0)
            {
                return UnityEngine.Mathf.Clamp(_trackedFirstLineIndex, 0, UnityEngine.Mathf.Max(0, totalItems - 1));
            }
            
            return 0;
        }

        private static int FindBlueprintIndexInList(
                    Il2CppSystem.Collections.Generic.List<BlueprintData> list,
                    BlueprintData targetBp,
                    int preferredIndex)
        {
            if (list == null || list.Count == 0)
                return -1;

            if (targetBp != null)
            {
                // 1. Exact object reference match first
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && ReferenceEquals(list[i], targetBp))
                        return i;
                }

                // 2. Internal blueprint name match (e.g., distinct recipe names behind identical "Tinder Plug (2)" labels)
                string targetInternalName = targetBp.name;
                if (!string.IsNullOrEmpty(targetInternalName))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var candidate = list[i];
                        if (candidate != null && string.Equals(candidate.name, targetInternalName, System.StringComparison.Ordinal))
                            return i;
                    }
                }
            }

            if (preferredIndex >= 0 && preferredIndex < list.Count)
                return preferredIndex;

            return -1;
        }
        private static int FindMatchingIndexNearHint(
                    Il2CppSystem.Collections.Generic.List<BlueprintData> list,
                    UnityEngine.Transform row,
                    int hintIndex)
        {
            if (list == null || list.Count == 0 || row == null)
                return -1;

            string primaryLabel = string.Empty;
            foreach (var lbl in row.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl == null) continue;
                string t = (lbl.text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(t))
                {
                    primaryLabel = t;
                    break;
                }
            }

            if (string.IsNullOrEmpty(primaryLabel))
                return -1;

            int count = list.Count;
            int start = UnityEngine.Mathf.Clamp(hintIndex, 0, count - 1);

            // 1. Check exact index first if it already matches this row's label
            var directBp = list[start];
            if (directBp != null)
            {
                string dDisp = (directBp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string dRaw = (directBp.name ?? string.Empty).Trim();
                if (string.Equals(dDisp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dRaw, primaryLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    return start;
                }
            }

            // 2. Search forward from start (no wrap-around) so scrolled duplicate-named items win
            for (int idx = start + 1; idx < count; idx++)
            {
                var bp = list[idx];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                if (string.Equals(disp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, primaryLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    return idx;
                }
            }

            // 3. Fallback: search before start if nothing matched past start
            for (int idx = 0; idx < start; idx++)
            {
                var bp = list[idx];
                if (bp == null) continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                if (string.Equals(disp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, primaryLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    return idx;
                }
            }

            return -1;
        }
        // Attempt to bind a click handler to a crafting row that resolves the correct blueprint index based on the current filtered list and search state
        private static void BindRowIndexClickHandler(
                    Panel_Crafting panel,
                    Transform row,
                    BlueprintData boundBlueprint,
                    int fallbackIndex,
                    int slotIndex,
                    int visibleSlots,
                    bool preferPositionalIndex)
        {
            if (panel == null || row == null)
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

            BlueprintData capturedBp = boundBlueprint;
            int capturedFallback = fallbackIndex;
            int capturedSlot = slotIndex;
            int capturedVisible = visibleSlots;
            bool capturedPreferPositional = preferPositionalIndex;

            listener.onClick = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UIEventListener.VoidDelegate>(
                new System.Action<UnityEngine.GameObject>((_) =>
                {
                    MelonLoader.MelonLogger.Msg($"[CraftingSearch] RawOnClick fired on '{row.name}' slot={capturedSlot} search={capturedPreferPositional}");

                    var activeList = panel.m_FilteredBlueprints ?? panel.m_Blueprints;
                    if (activeList == null || activeList.Count == 0)
                        return;

                    int scrollOffset = GetCurrentScrollOffset(panel, activeList.Count, capturedVisible);
                    int positionalIndex = capturedSlot >= 0 ? (scrollOffset + capturedSlot) : -1;
                    int chosenIndex = -1;

                    if (capturedPreferPositional)
                    {
                        if (scrollOffset > 0 && positionalIndex >= 0 && positionalIndex < activeList.Count)
                        {
                            chosenIndex = positionalIndex;
                        }
                        else if (capturedBp != null)
                        {
                            int anchor = capturedSlot >= 0 ? capturedSlot : (positionalIndex >= 0 ? positionalIndex : capturedFallback);
                            int matchedBpIndex = FindBlueprintIndexInList(activeList, capturedBp, anchor);
                            if (matchedBpIndex >= 0 && matchedBpIndex < activeList.Count)
                            {
                                chosenIndex = matchedBpIndex;
                            }
                        }

                        if (chosenIndex < 0 && capturedSlot >= 0 && capturedSlot < activeList.Count && scrollOffset == 0)
                        {
                            chosenIndex = capturedSlot;
                        }

                        if (chosenIndex < 0 && positionalIndex >= 0 && positionalIndex < activeList.Count)
                        {
                            chosenIndex = positionalIndex;
                        }
                    }

                    if (chosenIndex < 0 && capturedFallback >= 0 && capturedFallback < activeList.Count)
                    {
                        chosenIndex = capturedFallback;
                    }

                    if (chosenIndex < 0)
                    {
                        int resolvedByLabel = ResolveBlueprintIndexForRow(panel, row, activeList);
                        if (resolvedByLabel >= 0 && resolvedByLabel < activeList.Count)
                        {
                            chosenIndex = resolvedByLabel;
                        }
                    }
                    // If the row's current displayed label differs from activeList[chosenIndex], find the matching item near capturedSlot
                    if (chosenIndex >= 0 && chosenIndex < activeList.Count)
                    {
                        string rowText = string.Empty;
                        foreach (var lbl in row.GetComponentsInChildren<UILabel>(true))
                        {
                            if (lbl != null && !string.IsNullOrWhiteSpace(lbl.text))
                            {
                                rowText = lbl.text.Trim();
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(rowText))
                        {
                            var currentBp = activeList[chosenIndex];
                            string currentDisp = currentBp != null ? (currentBp.GetDisplayedNameWithCount() ?? string.Empty).Trim() : string.Empty;
                            if (!string.Equals(currentDisp, rowText, System.StringComparison.OrdinalIgnoreCase))
                            {
                                int labelMatch = FindMatchingIndexNearHint(activeList, row, UnityEngine.Mathf.Max(chosenIndex, capturedSlot));
                                if (labelMatch >= 0 && labelMatch < activeList.Count)
                                {
                                    chosenIndex = labelMatch;
                                }
                            }
                        }
                    }
                    MelonLoader.MelonLogger.Msg($"[CraftingSearch] Resolve slot={capturedSlot} count={activeList.Count} offset={scrollOffset} pos={positionalIndex} fallback={capturedFallback} -> chosen={chosenIndex}");

                    if (chosenIndex >= 0 && chosenIndex < activeList.Count)
                    {
                        SelectBlueprintByIndex(panel, chosenIndex);
                        panel.m_NeedsUpdate = true;
                    }
                })
            );
        }
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
        // Attempt to synchronize the visible blueprint list rows with the filtered list, adjusting labels, icons, and click handlers
        private static void SyncBlueprintListRows(Panel_Crafting panel, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            if (panel == null || filtered == null)
                return;

            string query = SearchBarUI.CurrentQuery;
            bool hasQuery = !string.IsNullOrEmpty(query);

            var scrollBehaviour = panel.GetComponentInChildren<ScrollBehaviour>(true);

            HookCraftingScrollViews(panel);

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

            // Leave native row clicks/scrolling alone when no search filter is active
            if (!hasQuery)
            {
                foreach (var row in rowTransforms)
                {
                    if (row != null)
                    {
                        row.gameObject.SetActive(true);
                    }
                }
                MelonLoader.MelonLogger.Msg($"[CraftingSearch] SyncBlueprintListRows skipped custom click override (!hasQuery, rows={rowTransforms.Count}, filtered={filtered.Count})");
                return;
            }

            float topY = rowTransforms[0].localPosition.y;
            float stepY = rowTransforms.Count > 1
                ? UnityEngine.Mathf.Abs(rowTransforms[1].localPosition.y - rowTransforms[0].localPosition.y)
                : 64f;
            if (stepY < 1f) stepY = 64f;

            int visibleCount = UnityEngine.Mathf.Min(rowTransforms.Count, filtered.Count);
            for (int i = 0; i < rowTransforms.Count; i++)
            {
                var row = rowTransforms[i];
                if (row == null) continue;

                if (i < visibleCount)
                {
                    BlueprintData bp = filtered[i];
                    string disp = bp != null ? (bp.GetDisplayedNameWithCount() ?? bp.name ?? string.Empty).Trim() : string.Empty;
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
                    BindRowIndexClickHandler(panel, row, bp, i, i, rowTransforms.Count, preferPositionalIndex: true);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }

            bool allFitInView = filtered.Count <= rowTransforms.Count;
            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            foreach (var sb in panel.GetComponentsInChildren<ScrollBehaviour>(true))
            {
                if (sb == null) continue;

                var rebuildMethod = sb.GetType().GetMethod("RebuildItems", flags, null, [typeof(int)], null);
                if (rebuildMethod != null)
                {
                    try { rebuildMethod.Invoke(sb, [filtered.Count]); } catch { }
                }

                foreach (var mName in new[] { "RefreshItems", "LayoutItems" })
                {
                    var m = sb.GetType().GetMethod(mName, flags);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        try { m.Invoke(sb, null); } catch { }
                    }
                }
            }

            foreach (var scrollView in scrollBehaviour.GetComponentsInChildren<UIScrollView>(true))
            {
                if (scrollView == null) continue;

                scrollView.currentMomentum = UnityEngine.Vector3.zero;
                scrollView.ResetPosition();
                scrollView.enabled = !allFitInView;
                scrollView.disableDragIfFits = allFitInView;

                if (scrollView.verticalScrollBar != null)
                {
                    if (allFitInView)
                    {
                        scrollView.verticalScrollBar.value = 0f;
                        scrollView.verticalScrollBar.gameObject.SetActive(false);
                    }
                    else
                    {
                        scrollView.verticalScrollBar.gameObject.SetActive(true);
                    }
                }

                scrollView.UpdateScrollbars(true);
            }

            panel.m_NeedsUpdate = true;
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
    }
}
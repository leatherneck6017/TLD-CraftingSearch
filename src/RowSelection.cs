using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.UI.Scroll;
using UnityEngine;

namespace CraftingSearch
{
    internal static class RowSelection
    {
        private const System.Reflection.BindingFlags InstanceMemberFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        private static System.Reflection.MethodInfo _setSelectedIndexMethod;
        private static bool _setSelectedIndexResolved;
        private static System.Reflection.MethodInfo _clickRefreshAvailMethod;
        private static bool _clickRefreshAvailResolved;


        private static readonly System.Collections.Generic.Dictionary<string, int> _lastIndexByBaseName =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        private struct RowBind
        {
            public Panel_Crafting panel;
            public Transform row;
            public BlueprintData bp;
            public int fallbackIndex;
            public int slotIndex;
            public int visibleSlots;
            public bool preferPositional;
        }

        private static readonly System.Collections.Generic.Dictionary<int, RowBind> _rowBinds = new();
        private static readonly System.Collections.Generic.HashSet<int> _hookedRows = new();
        private static UIEventListener.VoidDelegate _sharedClick;

        internal static void ResetSelectionTracking()
        {
            _lastIndexByBaseName.Clear();
            ScrollListSync.ResetScrollTracking();
        }

        internal static void BindRowIndexClickHandler(
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

            int id = row.gameObject.GetInstanceID();
            _rowBinds[id] = new RowBind
            {
                panel = panel,
                row = row,
                bp = boundBlueprint,
                fallbackIndex = fallbackIndex,
                slotIndex = slotIndex,
                visibleSlots = visibleSlots,
                preferPositional = preferPositionalIndex
            };

            if (_sharedClick == null)
            {
                _sharedClick = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UIEventListener.VoidDelegate>(
                    new System.Action<UnityEngine.GameObject>(OnRowClicked));
            }

            if (_hookedRows.Add(id))
            {
                var listener = row.GetComponent<UIEventListener>() ?? row.gameObject.AddComponent<UIEventListener>();
                listener.onSelect = null;
                listener.onClick = _sharedClick;
            }
        }

        private static void OnRowClicked(UnityEngine.GameObject go)
        {
            if (go == null || !_rowBinds.TryGetValue(go.GetInstanceID(), out RowBind bind) || bind.panel == null || bind.row == null)
                return;

            var panel = bind.panel;
            var row = bind.row;
            var capturedBp = bind.bp;
            int capturedFallback = bind.fallbackIndex;
            int capturedSlot = bind.slotIndex;
            int capturedVisible = bind.visibleSlots;
            bool capturedPreferPositional = bind.preferPositional;

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
                        string currentDisp = currentBp != null ? SearchCache.GetDisplayedName(currentBp) : string.Empty;
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

                if (chosenIndex >= 0 && chosenIndex < activeList.Count)
                {
                    SelectBlueprintByIndex(panel, chosenIndex);
                    panel.m_NeedsUpdate = true;
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

                panel.RefreshSelectedBlueprint(blueprintIndex);

                var sb = panel.GetComponentInChildren<ScrollBehaviour>(true);
                if (sb != null)
                {
                    if (_setSelectedIndexMethod == null && !_setSelectedIndexResolved)
                    {
                        _setSelectedIndexResolved = true;
                        _setSelectedIndexMethod = sb.GetType().GetMethod("SetSelectedIndex", InstanceMemberFlags, null, [typeof(int)], null);
                    }

                    if (_setSelectedIndexMethod != null)
                    {
                        try { _setSelectedIndexMethod.Invoke(sb, [blueprintIndex]); } catch { }
                    }
                }

                if (!_clickRefreshAvailResolved)
                {
                    _clickRefreshAvailResolved = true;
                    var m = typeof(Panel_Crafting).GetMethod("RefreshAvailableBlueprints", InstanceMemberFlags);
                    _clickRefreshAvailMethod = m != null && m.GetParameters().Length == 0 ? m : null;
                }

                if (_clickRefreshAvailMethod != null)
                {
                    try { _clickRefreshAvailMethod.Invoke(panel, null); } catch { }
                }

                panel.m_NeedsUpdate = true;
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
                                    if (measured >= 10f)
                                        rowStep = measured;
                                }
                            }

                            int offsetFromClip = UnityEngine.Mathf.FloorToInt((clipY + (rowStep * 0.25f)) / rowStep);
                            return UnityEngine.Mathf.Clamp(offsetFromClip, 0, UnityEngine.Mathf.Max(0, totalItems - 1));
                        }
                    }

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

            if (ScrollListSync.TrackedFirstLineIndex > 0)
            {
                return UnityEngine.Mathf.Clamp(ScrollListSync.TrackedFirstLineIndex, 0, UnityEngine.Mathf.Max(0, totalItems - 1));
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
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && ReferenceEquals(list[i], targetBp))
                        return i;
                }

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
                if (lbl == null)
                    continue;

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

            for (int idx = start; idx < count; idx++)
            {
                var bp = list[idx];
                if (bp == null)
                    continue;

                string disp = (bp.GetDisplayedNameWithCount() ?? string.Empty).Trim();
                string raw = (bp.name ?? string.Empty).Trim();

                if (string.Equals(disp, primaryLabel, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, primaryLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    return idx;
                }
            }

            for (int idx = 0; idx < start; idx++)
            {
                var bp = list[idx];
                if (bp == null)
                    continue;

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
                if (lbl == null)
                    continue;

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

            if (!string.IsNullOrEmpty(primaryLabel))
            {
                for (int offset = 0; offset < count; offset++)
                {
                    int idx = (searchStart + offset) % count;
                    BlueprintData bp = list[idx];
                    if (bp == null)
                        continue;

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

            if (!string.IsNullOrEmpty(primaryBase))
            {
                for (int offset = 0; offset < count; offset++)
                {
                    int idx = (searchStart + offset) % count;
                    BlueprintData bp = list[idx];
                    if (bp == null)
                        continue;

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

            for (int offset = 0; offset < count; offset++)
            {
                int idx = (searchStart + offset) % count;
                BlueprintData bp = list[idx];
                if (bp == null)
                    continue;

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

            for (int offset = 0; offset < count; offset++)
            {
                int idx = (searchStart + offset) % count;
                BlueprintData bp = list[idx];
                if (bp == null)
                    continue;

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
    }
}
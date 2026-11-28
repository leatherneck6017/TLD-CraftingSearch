using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.UI.Scroll;
using UnityEngine;

namespace CraftingSearch
{
    internal static class ScrollListSync
    {
        private const System.Reflection.BindingFlags InstanceMemberFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        private static int _trackedFirstLineIndex = 0;

        private static readonly System.Collections.Generic.HashSet<int> _hookedScrollViews =
            new System.Collections.Generic.HashSet<int>();

        internal static int TrackedFirstLineIndex => _trackedFirstLineIndex;

        internal static void ResetScrollTracking()
        {
            _trackedFirstLineIndex = 0;
        }

        // only these ScrollBehaviour methods matter to the postfix. patching them once at startup
        // keeps the Harmony cost off the first crafting open
        private static readonly string[] TrackedScrollMethods =
            ["UpdateFirstLine", "RefreshItems", "OnScrollBarChange", "HandleOnItemSelected", "SetSelectedIndex"];

        private static System.Type _firstLineType;
        private static System.Reflection.PropertyInfo _firstLineProp;
        private static System.Reflection.FieldInfo _firstLineField;

        private static System.Type _sbMethodsType;
        private static System.Reflection.MethodInfo _rebuildItemsMethod;
        private static System.Reflection.MethodInfo[] _refreshLayoutMethods;

        private static System.Reflection.MethodInfo _refreshSelMethod;
        private static bool _refreshSelResolved;

        internal static void PatchScrollBehaviour(HarmonyLib.Harmony harmony)
        {
            var flags = InstanceMemberFlags | System.Reflection.BindingFlags.DeclaredOnly;
            var postfix = new HarmonyLib.HarmonyMethod(typeof(ScrollListSync).GetMethod(
                nameof(ScrollBehaviourAnyMethod_Postfix),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));

            foreach (var method in typeof(ScrollBehaviour).GetMethods(flags))
            {
                if (method.IsSpecialName || System.Array.IndexOf(TrackedScrollMethods, method.Name) < 0)
                    continue;

                try
                {
                    harmony.Patch(method, postfix: postfix);
                }
                catch
                {
                    // Skip methods Harmony cannot patch in IL2CPP
                }
            }
        }

        private static int ReadScrollBehaviourFirstLine(ScrollBehaviour sb)
        {
            if (sb == null)
                return -1;

            var type = sb.GetType();
            if (type != _firstLineType)
            {
                _firstLineType = type;
                _firstLineProp = null;
                _firstLineField = null;

                foreach (string memberName in new[] { "FirstLine", "m_FirstLine", "m_TopIndex", "TopIndex", "m_StartIndex", "StartIndex" })
                {
                    var prop = type.GetProperty(memberName, InstanceMemberFlags);
                    if (prop != null && prop.PropertyType == typeof(int) && prop.GetIndexParameters().Length == 0)
                    {
                        _firstLineProp = prop;
                        break;
                    }

                    var field = type.GetField(memberName, InstanceMemberFlags);
                    if (field != null && field.FieldType == typeof(int))
                    {
                        _firstLineField = field;
                        break;
                    }
                }
            }

            try
            {
                if (_firstLineProp != null)
                    return (int)_firstLineProp.GetValue(sb);
                if (_firstLineField != null)
                    return (int)_firstLineField.GetValue(sb);
            }
            catch { }

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

            // don't set m_NeedsUpdate here: our own RefreshItems / SetSelectedIndex calls
            // would retrigger ApplyFilter every frame while a search is active
        }

        internal static void RefreshCraftingScrollList(Panel_Crafting panel, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            if (panel == null)
                return;

            if (!_refreshSelResolved)
            {
                _refreshSelResolved = true;
                _refreshSelMethod = typeof(Panel_Crafting).GetMethod("RefreshSelectedBlueprint", InstanceMemberFlags);
            }

            var refreshSelMethod = _refreshSelMethod;
            if (refreshSelMethod != null)
            {
                var parms = refreshSelMethod.GetParameters();
                if (parms.Length == 1 && filtered != null && filtered.Count > 0)
                {
                    try
                    {
                        if (parms[0].ParameterType == typeof(int))
                        {
                            refreshSelMethod.Invoke(panel, [0]);
                        }
                        else if (parms[0].ParameterType == typeof(BlueprintData))
                        {
                            refreshSelMethod.Invoke(panel, [filtered[0]]);
                        }
                    }
                    catch
                    {
                        // Ignore if signature or state doesn't match
                    }
                }

                SyncBlueprintListRows(panel, filtered);
            }

        }

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
                if (child == null)
                    continue;

                string childName = child.gameObject.name ?? string.Empty;
                if (childName.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    rowTransforms.Add(child);
                }
            }

            if (rowTransforms.Count == 0)
                return;

            if (!hasQuery)
            {
                foreach (var row in rowTransforms)
                {
                    if (row != null)
                    {
                        row.gameObject.SetActive(true);
                    }
                }
                return;
            }

            float topY = rowTransforms[0].localPosition.y;
            float stepY = rowTransforms.Count > 1
                ? UnityEngine.Mathf.Abs(rowTransforms[1].localPosition.y - rowTransforms[0].localPosition.y)
                : 64f;
            if (stepY < 1f)
                stepY = 64f;

            int visibleCount = UnityEngine.Mathf.Min(rowTransforms.Count, filtered.Count);
            for (int i = 0; i < rowTransforms.Count; i++)
            {
                var row = rowTransforms[i];
                if (row == null)
                    continue;

                if (i < visibleCount)
                {
                    BlueprintData bp = filtered[i];
                    string disp = bp != null ? SearchCache.GetDisplayedName(bp) : string.Empty;
                    if (string.IsNullOrEmpty(disp) && bp != null)
                        disp = bp.name ?? string.Empty;
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
                    RowSelection.BindRowIndexClickHandler(panel, row, bp, i, i, rowTransforms.Count, preferPositionalIndex: true);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }

            bool allFitInView = filtered.Count <= rowTransforms.Count;

            foreach (var sb in panel.GetComponentsInChildren<ScrollBehaviour>(true))
            {
                if (sb == null)
                    continue;

                ResolveScrollBehaviourMethods(sb.GetType());

                if (_rebuildItemsMethod != null)
                {
                    try { _rebuildItemsMethod.Invoke(sb, [filtered.Count]); } catch { }
                }

                foreach (var m in _refreshLayoutMethods)
                {
                    try { m.Invoke(sb, null); } catch { }
                }
            }

            foreach (var scrollView in scrollBehaviour.GetComponentsInChildren<UIScrollView>(true))
            {
                if (scrollView == null)
                    continue;

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
        }

        private static void ResolveScrollBehaviourMethods(System.Type type)
        {
            if (type == _sbMethodsType)
                return;

            _rebuildItemsMethod = type.GetMethod("RebuildItems", InstanceMemberFlags, null, [typeof(int)], null);

            var list = new System.Collections.Generic.List<System.Reflection.MethodInfo>(2);
            foreach (var mName in new[] { "RefreshItems", "LayoutItems" })
            {
                var m = type.GetMethod(mName, InstanceMemberFlags);
                if (m != null && m.GetParameters().Length == 0)
                    list.Add(m);
            }
            _refreshLayoutMethods = list.ToArray();
            _sbMethodsType = type;
        }

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

                Texture resolvedTex = null;
                var assetProp = addrRef.GetType().GetProperty("Asset", InstanceMemberFlags);
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
    }
}

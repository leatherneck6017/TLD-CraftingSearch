using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTLD.UI.Scroll;
using MelonLoader;
using UnityEngine;

namespace CraftingSearch
{
    internal static class SearchBarUI
    {
        private const int BoxWidth = 300;
        private const int BoxHeight = 36;
        internal static UIInput SearchInput;
        private static Panel_Crafting _craftingPanel;
        //private static Texture2D _solidTex;

        // delete this method if you don't need to debug the hierarchy path of a transform
        private static UISprite FindSpriteByPathOrName(
            Panel_Crafting panel,
            string requiredPathSubstring,
            string preferredSpriteName = null)
        {
            if (panel == null)
                return null;

            UISprite firstMatch = null;
            string reqLower = (requiredPathSubstring ?? string.Empty).ToLowerInvariant();
            string prefLower = (preferredSpriteName ?? string.Empty).ToLowerInvariant();

            foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
            {
                if (sprite == null || sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
                    continue;

                string path = GetHierarchyPath(sprite.transform);
                string pathLower = path.ToLowerInvariant();
                string spriteLower = sprite.spriteName.ToLowerInvariant();

                if (!string.IsNullOrEmpty(reqLower) && !pathLower.Contains(reqLower))
                    continue;

                if (!string.IsNullOrEmpty(prefLower) && spriteLower == prefLower)
                {
                    MelonLogger.Msg($"Matched exact sprite '{sprite.spriteName}' at '{path}' ({sprite.width}x{sprite.height})");
                    return sprite;
                }

                if (firstMatch == null)
                    firstMatch = sprite;
            }

            if (firstMatch != null)
            {
                MelonLogger.Msg($"Matched path '{requiredPathSubstring}' -> sprite='{firstMatch.spriteName}' at '{GetHierarchyPath(firstMatch.transform)}' ({firstMatch.width}x{firstMatch.height})");
            }

            return firstMatch;
        }

        // use this method to log all the UISprite objects in the crafting panel hierarchy, which can help you find a suitable background sprite for the search box
        //private static void LogBlueprintListSprites(Panel_Crafting panel)
        //{
        //    if (panel == null)
        //        return;

        //    foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
        //    {
        //        if (sprite == null || sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
        //            continue;

        //        string path = GetHierarchyPath(sprite.transform);
        //        if (path.Contains("BlueprintList", System.StringComparison.OrdinalIgnoreCase))
        //        {
        //            MelonLogger.Msg($"BP sprite='{sprite.spriteName}' ({sprite.width}x{sprite.height}, type={sprite.type}) at '{path}'");
        //        }
        //    }
        //}

        //private static Texture2D GetSolidTexture()
        //{
        //    if (_solidTex != null)
        //        return _solidTex;

        //    _solidTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        //    _solidTex.SetPixel(0, 0, Color.white);
        //    _solidTex.Apply();
        //    _solidTex.hideFlags = HideFlags.HideAndDontSave;
        //    return _solidTex;
        //}

        internal static string CurrentQuery =>
            SearchInput != null && SearchInput.value != null ? SearchInput.value.Trim() : string.Empty;

        internal static bool IsTyping =>
            SearchInput != null &&
            (SearchInput.isSelected || UICamera.selectedObject == SearchInput.gameObject);

        private static void TryGetAboveBlueprintsPosition(
                    Panel_Crafting panel,
                    Transform targetParent,
                    ref float x,
                    ref float y)
        {
            if (panel == null || targetParent == null)
                return;

            foreach (var lbl in panel.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl == null)
                    continue;

                string txt = lbl.text ?? string.Empty;
                string objName = lbl.gameObject != null ? lbl.gameObject.name : string.Empty;

                if (txt.Contains("blueprint", System.StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("blueprint", System.StringComparison.OrdinalIgnoreCase))
                {
                    Vector3 localHeaderPos = targetParent.InverseTransformPoint(lbl.transform.position);
                    float yOffset = (lbl.height * 0.5f) + (BoxHeight * 0.5f) + 12f;

                    x = localHeaderPos.x + (BoxWidth * 0.5f) - (lbl.width * 0.5f);
                    y = localHeaderPos.y + yOffset;

                    MelonLogger.Msg($"Positioned above '{txt}' ({objName}) at local ({x:F1}, {y:F1})");
                    return;
                }
            }

            MelonLogger.Msg($"'Blueprint' label not found; using fallback ({x:F1}, {y:F1})");
        }

        internal static void AttachTo(Panel_Crafting panel)
        {
            _craftingPanel = panel;

            if (SearchInput != null)
            {
                SearchInput.gameObject.SetActive(true);
                SearchInput.value = string.Empty;
                if (SearchInput.label != null)
                {
                    SearchInput.label.gameObject.SetActive(true);
                    SearchInput.label.enabled = true;
                    SearchInput.label.MarkAsChanged();
                }
                return;
            }

            UIInput template = FindTemplateInput();
            if (template == null)
            {
                MelonLogger.Warning("[CraftingSearch] Could not find any UIInput template!");
                return;
            }

            Transform targetParent = panel.transform.childCount > 0 ? panel.transform.GetChild(0) : panel.transform;
            MelonLogger.Msg($"Cloning '{template.transform.root.name}/{template.name}' onto '{targetParent.name}' (layer {targetParent.gameObject.layer})");

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, targetParent);
            clone.name = "CraftingSearchInput";
            SetLayerRecursively(clone, targetParent.gameObject.layer);
            clone.SetActive(true);

            clone.transform.localScale = Vector3.one;

            // Strip gameplay/localization/tween scripts across the entire cloned hierarchy so nothing overwrites our text or blocks NGUI rendering
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetIl2CppType().Name;
                if (typeName != nameof(UIInput) &&
                    typeName != nameof(UISprite) &&
                    typeName != nameof(UILabel) &&
                    typeName != nameof(UIWidget) &&
                    typeName != nameof(BoxCollider))
                {
                    UnityEngine.Object.Destroy(mb);
                }
            }

            foreach (var w in clone.GetComponentsInChildren<UIWidget>(true))
            {
                w.alpha = 1f;
                w.gameObject.SetActive(true);
            }

            SearchInput = clone.GetComponent<UIInput>() ?? clone.AddComponent<UIInput>();

            // Borrow a known-good font from an existing label inside Panel_Crafting
            UILabel sourceLabel = FindValidFontSource(panel);

            // Tunable gray background & fallback coordinates (used only if no "Blueprints" label is found)
            float searchBoxX = 0f;
            float searchBoxY = -200f;

            // Destroy any template sprites so no cloned sprite graphics remain
            foreach (var oldSprite in clone.GetComponentsInChildren<UISprite>(true))
            {
                if (oldSprite != null)
                {
                    UnityEngine.Object.Destroy(oldSprite.gameObject == clone ? (UnityEngine.Object)oldSprite : oldSprite.gameObject);
                }
            }

            //LogBlueprintListSprites(panel); // Optional debug log to help find a suitable background sprite for the search box

            UISprite bpBgSource = FindSpriteByPathOrName(panel, "BlueprintListPrefab(Clone)/BG", "inv_statBack");
            if (bpBgSource != null)
            {
                GameObject bgGo = new GameObject("SearchBg");
                bgGo.transform.SetParent(clone.transform, false);
                bgGo.layer = clone.layer;
                bgGo.transform.localPosition = new Vector3(0f, 0f, 0f);
                bgGo.transform.localScale = Vector3.one;

                UISprite bgSprite = bgGo.AddComponent<UISprite>();
                bgSprite.atlas = bpBgSource.atlas;
                bgSprite.spriteName = bpBgSource.spriteName;
                bgSprite.type = bpBgSource.type;
                bgSprite.alpha = 1f;
                // try to match the color of the blueprint list background, but make it slightly darker and more opaque
                bgSprite.color = new Color(0.15f, 0.17f, 0.17f, 0.95f);
                bgSprite.width = BoxWidth;
                bgSprite.height = BoxHeight;
                bgSprite.depth = 499;
                bgSprite.MarkAsChanged();

                MelonLogger.Msg($"Borrowed BP bg sprite='{bpBgSource.spriteName}' from '{GetHierarchyPath(bpBgSource.transform)}'");
            }

            // Destroy the template's old label(s) so no hidden anchors or broken fonts interfere
            foreach (var oldLabel in clone.GetComponentsInChildren<UILabel>(true))
            {
                if (oldLabel != null)
                {
                    UnityEngine.Object.Destroy(oldLabel.gameObject == clone ? (UnityEngine.Object)oldLabel : oldLabel.gameObject);
                }
            }

            // Clone a known-working UILabel directly from Panel_Crafting (already has valid font/material/shader)
            UILabel label = null;
            if (sourceLabel != null)
            {
                GameObject labelGo = UnityEngine.Object.Instantiate(sourceLabel.gameObject, clone.transform);
                labelGo.name = "SearchLabel";
                SetLayerRecursively(labelGo, clone.layer);

                // Strip any localization or helper scripts from the cloned label
                foreach (var mb in labelGo.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (mb.GetIl2CppType().Name != nameof(UILabel))
                    {
                        UnityEngine.Object.Destroy(mb);
                    }
                }

                label = labelGo.GetComponent<UILabel>();
            }

            if (label == null)
            {
                GameObject labelGo = new GameObject("SearchLabel");
                labelGo.transform.SetParent(clone.transform, false);
                labelGo.layer = clone.layer;
                label = labelGo.AddComponent<UILabel>();
            }

            SearchInput.label = label;
            SearchInput.enabled = true;
            SearchInput.onChange.Clear();
            SearchInput.onSubmit.Clear();
            SearchInput.defaultText = "Search blueprints...";
            SearchInput.value = string.Empty;
            SearchInput.activeTextColor = Color.white;
            SearchInput.caretColor = Color.white;
            SearchInput.characterLimit = 40;
            SearchInput.inputType = UIInput.InputType.Standard;
            SearchInput.validation = UIInput.Validation.None;
            SearchInput.hideInput = false;

            label.gameObject.SetActive(true);
            label.enabled = true;
            label.alpha = 1f;
            label.color = Color.white;
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            label.maxLineCount = 1;
            label.width = 296;
            label.height = 34;
            label.depth = 500;
            label.transform.localScale = Vector3.one;
            label.transform.localPosition = new Vector3(-148f, 0f, -2f);
            label.text = "Search blueprints...";
            label.MarkAsChanged();

            BoxCollider collider = clone.GetComponent<BoxCollider>() ?? clone.AddComponent<BoxCollider>();
            collider.size = new Vector3(BoxWidth, BoxHeight, 1f);
            collider.center = Vector3.zero;
            collider.enabled = true;

            EventDelegate.Add(SearchInput.onChange, DelegateSupport.ConvertDelegate<EventDelegate.Callback>(
                new System.Action(OnSearchTextChanged)
            ));

            TryGetAboveBlueprintsPosition(panel, targetParent, ref searchBoxX, ref searchBoxY);
            clone.transform.localPosition = new Vector3(searchBoxX, searchBoxY, -20f);

            //MelonLogger.Msg($"Attached at localPos={clone.transform.localPosition}, labelActive={label.gameObject.activeInHierarchy}, hasFont={(label.bitmapFont != null || label.trueTypeFont != null)}");
        }

        private static UILabel FindValidFontSource(Panel_Crafting panel)
        {
            if (panel == null) return null;

            foreach (var lbl in panel.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl != null && (lbl.bitmapFont != null || lbl.trueTypeFont != null))
                {
                    return lbl;
                }
            }

            foreach (var lbl in Resources.FindObjectsOfTypeAll<UILabel>())
            {
                if (lbl != null && (lbl.bitmapFont != null || lbl.trueTypeFont != null))
                {
                    return lbl;
                }
            }

            return null;
        }

        private static UIInput FindTemplateInput()
        {
            var inputs = Resources.FindObjectsOfTypeAll<UIInput>();
            MelonLogger.Msg($"Found {inputs.Length} total UIInput objects.");
            UIInput fallback = null;

            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input == null) continue;

                string rootName = input.transform.root != null ? input.transform.root.name : "null";
                if (input.label != null && (input.label.bitmapFont != null || input.label.trueTypeFont != null))
                {
                    if (fallback == null) fallback = input;
                    if (rootName.Contains("Custom") || input.name.Contains("Input") || input.name.Contains("Code"))
                        return input;
                }
            }
            return fallback;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            int childCount = obj.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = obj.transform.GetChild(i);
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        internal static void Clear()
        {
            if (SearchInput != null)
            {
                SearchInput.value = string.Empty;
                SearchInput.isSelected = false;
                if (SearchInput.label != null)
                {
                    SearchInput.label.text = SearchInput.defaultText ?? string.Empty;
                    SearchInput.label.MarkAsChanged();
                }
            }
        }

        //private static UISprite FindBlueprintsBackgroundSource(Panel_Crafting panel)
        //{
        //    if (panel == null)
        //        return null;

        //    UISprite bestCandidate = null;
        //    int bestArea = -1;

        //    foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
        //    {
        //        if (sprite == null || sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
        //            continue;

        //        string path = GetHierarchyPath(sprite.transform).ToLowerInvariant();
        //        string nameLower = sprite.gameObject.name.ToLowerInvariant();
        //        string spriteLower = sprite.spriteName.ToLowerInvariant();

        //        // Skip icons, checkmarks, dividers, and scrollbars
        //        if (nameLower.Contains("icon") || nameLower.Contains("check") || nameLower.Contains("arrow") ||
        //            nameLower.Contains("divider") || nameLower.Contains("line") || nameLower.Contains("scroll") ||
        //            spriteLower.Contains("icon") || spriteLower.Contains("check") || spriteLower.Contains("arrow"))
        //        {
        //            continue;
        //        }

        //        // Skip tiny sprites so we don't grab small badges or icons
        //        if (sprite.width < 120 || sprite.height < 24)
        //            continue;

        //        bool inCraftingOrBlueprints = path.Contains("blueprint") || path.Contains("craftingsection") || path.Contains("blueprintlist");
        //        int area = sprite.width * sprite.height + (inCraftingOrBlueprints ? 1_000_000 : 0);

        //        if (area > bestArea)
        //        {
        //            bestArea = area;
        //            bestCandidate = sprite;
        //        }
        //    }

        //    if (bestCandidate != null)
        //        return bestCandidate;

        //    foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
        //    {
        //        if (sprite != null && sprite.atlas != null && !string.IsNullOrEmpty(sprite.spriteName))
        //            return sprite;
        //    }

        //    return null;
        //}

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null)
                return string.Empty;

            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
            Transform curr = t;
            while (curr != null)
            {
                parts.Add(curr.name);
                curr = curr.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static bool _pendingFilterRefresh;

        private static void OnSearchTextChanged()
        {
            string current = SearchInput != null && SearchInput.value != null ? SearchInput.value : string.Empty;

            if (SearchInput != null && SearchInput.label != null)
            {
                UILabel lbl = SearchInput.label;
                lbl.gameObject.SetActive(true);
                lbl.enabled = true;
                lbl.alpha = 1f;
                lbl.color = Color.white;
                lbl.text = string.IsNullOrEmpty(current) ? (SearchInput.defaultText ?? string.Empty) : current;
                lbl.MarkAsChanged();
            }

            if (!_pendingFilterRefresh)
            {
                _pendingFilterRefresh = true;
                MelonCoroutines.Start(RefreshFilterNextFrame());
            }

            MelonLogger.Msg($"OnSearchTextChanged -> '{current}'");

        }

        private static System.Collections.IEnumerator RefreshFilterNextFrame()
        {
            yield return null;
            _pendingFilterRefresh = false;

            if (_craftingPanel == null || !_craftingPanel.IsEnabled())
                yield break;

            _craftingPanel.ApplyFilter();
            _craftingPanel.RefreshAvailableBlueprints();

            foreach (var scrollBehaviour in _craftingPanel.GetComponentsInChildren<ScrollBehaviour>(true))
            {
                if (scrollBehaviour != null)
                {
                    try
                    {
                        scrollBehaviour.enabled = true;
                        scrollBehaviour.SendMessage("Refresh", UnityEngine.SendMessageOptions.DontRequireReceiver);
                        scrollBehaviour.SendMessage("Update", UnityEngine.SendMessageOptions.DontRequireReceiver);
                    }
                    catch
                    {
                        // Safe if IL2CPP method/message isn't exposed
                    }
                }
            }
            
            foreach (var scrollView in _craftingPanel.GetComponentsInChildren<UIScrollView>(true))
            {
                if (scrollView != null)
                {
                    scrollView.InvalidateBounds();
                    scrollView.UpdateScrollbars(true);
                    scrollView.RestrictWithinBounds(true, false, true);
                }
            }

            foreach (var grid in _craftingPanel.GetComponentsInChildren<UIGrid>(true))
            {
                if (grid != null)
                {
                    grid.repositionNow = true;
                    grid.Reposition();
                }
            }

            foreach (var table in _craftingPanel.GetComponentsInChildren<UITable>(true))
            {
                if (table != null)
                {
                    table.repositionNow = true;
                    table.Reposition();
                }
            }

            foreach (var uiPanel in _craftingPanel.GetComponentsInChildren<UIPanel>(true))
            {
                if (uiPanel != null)
                {
                    try
                    {
                        uiPanel.SetDirty();
                    }
                    catch
                    {
                        // Safe if stripped or non-public in this IL2CPP build
                    }
                }
            }
        }
    }
}
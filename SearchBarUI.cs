using Il2Cpp;
using Il2CppInterop.Runtime;
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
        private static Texture2D _solidTex;

        private static Texture2D GetSolidTexture()
        {
            if (_solidTex != null)
                return _solidTex;

            _solidTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _solidTex.SetPixel(0, 0, Color.white);
            _solidTex.Apply();
            _solidTex.hideFlags = HideFlags.HideAndDontSave;
            return _solidTex;
        }

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

                    MelonLogger.Msg($"[CraftingSearch] Positioned above '{txt}' ({objName}) at local ({x:F1}, {y:F1})");
                    return;
                }
            }

            MelonLogger.Msg($"[CraftingSearch] 'Blueprint' label not found; using fallback ({x:F1}, {y:F1})");
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
            MelonLogger.Msg($"[CraftingSearch] Cloning '{template.transform.root.name}/{template.name}' onto '{targetParent.name}' (layer {targetParent.gameObject.layer})");

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

            //Texture2D solidTex = GetSolidTexture();

            //Shader boxShader =
            //                Shader.Find("Unlit/Transparent Colored") ??
            //                Shader.Find("Unlit/Premultiplied Colored") ??
            //                Shader.Find("UI/Default");

            //// Outer border
            //GameObject borderGo = new GameObject("SearchBorder");
            //borderGo.transform.SetParent(clone.transform, false);
            //borderGo.layer = clone.layer;
            //borderGo.transform.localPosition = new Vector3(0f, 0f, 1f);
            //borderGo.transform.localScale = Vector3.one;

            //UITexture border = borderGo.AddComponent<UITexture>();
            //border.mainTexture = solidTex;
            //if (boxShader != null)
            //    border.shader = boxShader;
            //border.color = new Color(0.82f, 0.84f, 0.88f, 1f);
            //border.alpha = 1f;
            //border.width = BoxWidth + 8;
            //border.height = BoxHeight + 8;
            //border.depth = 498;
            //border.MarkAsChanged();

            //// Inner solid slate-gray background fill
            //GameObject bgGo = new GameObject("SearchBg");
            //bgGo.transform.SetParent(clone.transform, false);
            //bgGo.layer = clone.layer;
            //bgGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            //bgGo.transform.localScale = Vector3.one;

            //UITexture bg = bgGo.AddComponent<UITexture>();
            //bg.mainTexture = solidTex;
            //if (boxShader != null)
            //    bg.shader = boxShader;
            //bg.color = new Color(0.38f, 0.40f, 0.43f, 1f);
            //bg.alpha = 1f;
            //bg.width = BoxWidth;
            //bg.height = BoxHeight;
            //bg.depth = 499;
            //bg.MarkAsChanged();

            //var panelForRefresh = UIPanel.Find(clone.transform);
            //if (panelForRefresh != null)
            //{
            //    try
            //    {
            //        panelForRefresh.SetDirty();
            //    }
            //    catch
            //    {
            //        // Safe fallback if SetDirty is stripped or non-public in this build
            //    }
            //}

            //MelonLogger.Msg($"[CraftingSearch] Border shader={(border.shader != null ? border.shader.name : "null")}, Bg shader={(bg.shader != null ? bg.shader.name : "null")}");

            UISprite bpBgSource = FindBlueprintsBackgroundSource(panel);
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
                // Keep a moderate tint so it is distinct from black without washing out white text
                bgSprite.color = new Color(0.55f, 0.58f, 0.62f, 1f);
                bgSprite.width = BoxWidth;
                bgSprite.height = BoxHeight;
                bgSprite.depth = 499;
                bgSprite.MarkAsChanged();

                MelonLogger.Msg($"[CraftingSearch] Borrowed BP bg sprite='{bpBgSource.spriteName}' from '{GetHierarchyPath(bpBgSource.transform)}'");
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

            MelonLogger.Msg($"[CraftingSearch] Attached at localPos={clone.transform.localPosition}, labelActive={label.gameObject.activeInHierarchy}, hasFont={(label.bitmapFont != null || label.trueTypeFont != null)}");
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
            MelonLogger.Msg($"[CraftingSearch] Found {inputs.Length} total UIInput objects.");
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

        private static UISprite FindBlueprintsBackgroundSource(Panel_Crafting panel)
        {
            if (panel == null)
                return null;

            UISprite bestCandidate = null;
            int bestArea = -1;

            foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
            {
                if (sprite == null || sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
                    continue;

                string path = GetHierarchyPath(sprite.transform).ToLowerInvariant();
                string nameLower = sprite.gameObject.name.ToLowerInvariant();
                string spriteLower = sprite.spriteName.ToLowerInvariant();

                // Skip icons, checkmarks, dividers, and scrollbars
                if (nameLower.Contains("icon") || nameLower.Contains("check") || nameLower.Contains("arrow") ||
                    nameLower.Contains("divider") || nameLower.Contains("line") || nameLower.Contains("scroll") ||
                    spriteLower.Contains("icon") || spriteLower.Contains("check") || spriteLower.Contains("arrow"))
                {
                    continue;
                }

                // Skip tiny sprites so we don't grab small badges or icons
                if (sprite.width < 120 || sprite.height < 24)
                    continue;

                bool inCraftingOrBlueprints = path.Contains("blueprint") || path.Contains("craftingsection") || path.Contains("blueprintlist");
                int area = sprite.width * sprite.height + (inCraftingOrBlueprints ? 1_000_000 : 0);

                if (area > bestArea)
                {
                    bestArea = area;
                    bestCandidate = sprite;
                }
            }

            if (bestCandidate != null)
                return bestCandidate;

            foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
            {
                if (sprite != null && sprite.atlas != null && !string.IsNullOrEmpty(sprite.spriteName))
                    return sprite;
            }

            return null;
        }

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

        //private static UILabel CreateBackgroundLabel(
        //    Transform parent,
        //    int layer,
        //    UILabel fontSource,
        //    Color color,
        //    int depth,
        //    float zOffset,
        //    string blockGlyph,
        //    float scaleX,
        //    float scaleY)
        //{
        //    GameObject go = new GameObject("SearchBg");
        //    go.transform.SetParent(parent, false);
        //    go.layer = layer;

        //    UILabel bg = go.AddComponent<UILabel>();
        //    if (fontSource != null)
        //    {
        //        if (fontSource.bitmapFont != null)
        //            bg.bitmapFont = fontSource.bitmapFont;
        //        else if (fontSource.trueTypeFont != null)
        //            bg.trueTypeFont = fontSource.trueTypeFont;
        //    }

        //    bg.overflowMethod = UILabel.Overflow.ResizeFreely;
        //    bg.maxLineCount = 1;
        //    bg.pivot = UIWidget.Pivot.Center;
        //    bg.alignment = NGUIText.Alignment.Center;
        //    bg.text = blockGlyph;
        //    bg.color = color;
        //    bg.alpha = 1f;
        //    bg.depth = depth;
        //    bg.transform.localPosition = new Vector3(0f, 0f, zOffset);
        //    bg.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        //    bg.enabled = true;
        //    go.SetActive(true);
        //    bg.MarkAsChanged();

        //    return bg;
        //}

        private static void OnSearchTextChanged()
        {
            string current = SearchInput != null && SearchInput.value != null ? SearchInput.value : string.Empty;
            MelonLogger.Msg($"[CraftingSearch] OnSearchTextChanged -> '{current}'");

            if (SearchInput != null && SearchInput.label != null)
            {
                UILabel lbl = SearchInput.label;
                var parentPanel = UIPanel.Find(lbl.transform);
                MelonLogger.Msg($"[CraftingSearch] lblPos={lbl.transform.position}, lblLocal={lbl.transform.localPosition}, lblDepth={lbl.depth}, lblAlpha={lbl.finalAlpha}, panel={(parentPanel != null ? parentPanel.name : "null")}, panelDepth={(parentPanel != null ? parentPanel.depth : -1)}");
                lbl.gameObject.SetActive(true);
                lbl.enabled = true;
                lbl.alpha = 1f;
                lbl.color = Color.white;
                lbl.text = string.IsNullOrEmpty(current) ? (SearchInput.defaultText ?? string.Empty) : current;
                lbl.MarkAsChanged();
            }

            if (_craftingPanel != null && _craftingPanel.IsEnabled())
            {
                _craftingPanel.ApplyFilter();
            }
        }
    }
}
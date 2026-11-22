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
        private static bool _pendingFilterRefresh;
        private static GameObject _clearButtonGo;

        internal static string CurrentQuery =>
            SearchInput != null && SearchInput.value != null ? SearchInput.value.Trim() : string.Empty;

        internal static bool IsTyping =>
            SearchInput != null &&
            (SearchInput.isSelected || UICamera.selectedObject == SearchInput.gameObject);

        private static UISprite FindBlueprintRowBackgroundSprite(Panel_Crafting panel)
        {
            if (panel == null)
                return null;

            foreach (var sprite in panel.GetComponentsInChildren<UISprite>(true))
            {
                if (sprite == null || sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
                    continue;

                if (sprite.gameObject.name.Equals("BG", System.StringComparison.OrdinalIgnoreCase) &&
                    sprite.transform.parent != null &&
                    sprite.transform.parent.name.Contains("BlueprintListPrefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }

            return null;
        }

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
                    const float rightNudgeX = 8f; // Adjust ~10f–30f to taste

                    x = localHeaderPos.x + (BoxWidth * 0.5f) - (lbl.width * 0.5f) + rightNudgeX;
                    y = localHeaderPos.y + yOffset;
                    return;
                }
            }
        }

        // Main search bar creation and attachment logic. This is called when the crafting panel is opened.
        internal static void AttachTo(Panel_Crafting panel)
        {
            _craftingPanel = panel;
            // If the search input already exists, just reset it and ensure it's active
            if (SearchInput != null)
            {
                SearchInput.gameObject.SetActive(true);
                SearchInput.value = string.Empty;

                Transform existingParent = SearchInput.transform.parent;
                if (existingParent != null)
                {
                    float rx = SearchInput.transform.localPosition.x;
                    float ry = SearchInput.transform.localPosition.y;
                    TryGetAboveBlueprintsPosition(panel, existingParent, ref rx, ref ry);
                    SearchInput.transform.localPosition = new Vector3(rx, ry, SearchInput.transform.localPosition.z);
                }

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

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, targetParent);
            clone.name = "CraftingSearchInput";
            SetLayerRecursively(clone, targetParent.gameObject.layer);
            clone.SetActive(true);
            clone.transform.localScale = Vector3.one;

            // Strip gameplay/localization/tween scripts so nothing overwrites text or blocks NGUI rendering
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
            UILabel sourceLabel = FindValidFontSource(panel);

            float searchBoxX = 0f;
            float searchBoxY = -200f;

            foreach (var oldSprite in clone.GetComponentsInChildren<UISprite>(true))
            {
                if (oldSprite != null)
                {
                    UnityEngine.Object.Destroy(oldSprite.gameObject == clone ? (UnityEngine.Object)oldSprite : oldSprite.gameObject);
                }
            }

            UISprite bpBgSource = FindBlueprintRowBackgroundSprite(panel);
            if (bpBgSource != null)
            {
                GameObject bgGo = new GameObject("SearchBg");
                bgGo.transform.SetParent(clone.transform, false);
                bgGo.layer = clone.layer;
                bgGo.transform.localPosition = Vector3.zero;
                bgGo.transform.localScale = Vector3.one;

                UISprite bgSprite = bgGo.AddComponent<UISprite>();
                bgSprite.atlas = bpBgSource.atlas;
                bgSprite.spriteName = bpBgSource.spriteName;
                bgSprite.type = bpBgSource.type;
                bgSprite.alpha = 1f;
                bgSprite.color = new Color(0.15f, 0.17f, 0.17f, 0.95f);
                bgSprite.width = BoxWidth;
                bgSprite.height = BoxHeight;
                bgSprite.depth = 499;
                bgSprite.MarkAsChanged();
            }

            foreach (var oldLabel in clone.GetComponentsInChildren<UILabel>(true))
            {
                if (oldLabel != null)
                {
                    UnityEngine.Object.Destroy(oldLabel.gameObject == clone ? (UnityEngine.Object)oldLabel : oldLabel.gameObject);
                }
            }

            UILabel label = null;
            if (sourceLabel != null)
            {
                GameObject labelGo = UnityEngine.Object.Instantiate(sourceLabel.gameObject, clone.transform);
                labelGo.name = "SearchLabel";
                SetLayerRecursively(labelGo, clone.layer);

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
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.maxLineCount = 1;
            label.width = 280; // adjust width to fit within the box, taking into account the 'x' button and padding
            label.height = 34;
            label.depth = 500; // depth should be higher than the background sprite to ensure it's visible
            label.transform.localScale = Vector3.one;
            label.transform.localPosition = new Vector3(-144f, 0f, -2f); // position the label to the left side of the box, with some padding
            label.text = "Search blueprints...";
            label.MarkAsChanged();

            BoxCollider collider = clone.GetComponent<BoxCollider>() ?? clone.AddComponent<BoxCollider>();
            collider.size = new Vector3(BoxWidth, BoxHeight, 1f);
            collider.center = Vector3.zero;
            collider.enabled = true;
            
            // add clearing button to the right side of the search box
            _clearButtonGo = new GameObject("ClearButton");
            _clearButtonGo.transform.SetParent(clone.transform, false);
            _clearButtonGo.layer = clone.layer;
            _clearButtonGo.transform.localPosition = new Vector3(132f, 0f, -3f);
            _clearButtonGo.transform.localScale = Vector3.one;

            UILabel clearLabel = null;
            if (sourceLabel != null)
            {
                GameObject clearLabelGo = UnityEngine.Object.Instantiate(sourceLabel.gameObject, _clearButtonGo.transform);
                clearLabelGo.name = "ClearLabel";
                SetLayerRecursively(clearLabelGo, _clearButtonGo.layer);

                foreach (var mb in clearLabelGo.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (mb.GetIl2CppType().Name != nameof(UILabel))
                    {
                        UnityEngine.Object.Destroy(mb);
                    }
                }

                clearLabel = clearLabelGo.GetComponent<UILabel>();
            }

            if (clearLabel == null)
            {
                clearLabel = _clearButtonGo.AddComponent<UILabel>();
            }

            clearLabel.gameObject.SetActive(true);
            clearLabel.enabled = true;
            clearLabel.alpha = 1f;
            clearLabel.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            clearLabel.pivot = UIWidget.Pivot.Center;
            clearLabel.alignment = NGUIText.Alignment.Center;
            clearLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            clearLabel.maxLineCount = 1;
            clearLabel.width = 24;
            clearLabel.height = 24;
            clearLabel.depth = 505;
            clearLabel.transform.localScale = Vector3.one;
            clearLabel.transform.localPosition = Vector3.zero;
            clearLabel.text = "X";
            clearLabel.MarkAsChanged();

            BoxCollider clearCollider = _clearButtonGo.GetComponent<BoxCollider>() ?? _clearButtonGo.AddComponent<BoxCollider>();
            clearCollider.size = new Vector3(26f, 26f, 1f);
            clearCollider.center = Vector3.zero;
            clearCollider.enabled = true;

            UIEventListener clearListener = _clearButtonGo.GetComponent<UIEventListener>() ?? _clearButtonGo.AddComponent<UIEventListener>();
            clearListener.onClick = DelegateSupport.ConvertDelegate<UIEventListener.VoidDelegate>(
                new System.Action<GameObject>((_) =>
                {
                    Clear();
                    OnSearchTextChanged();
                })
            );

            _clearButtonGo.SetActive(false);


            EventDelegate.Add(SearchInput.onChange, DelegateSupport.ConvertDelegate<EventDelegate.Callback>(
                new System.Action(OnSearchTextChanged)
            ));

            TryGetAboveBlueprintsPosition(panel, targetParent, ref searchBoxX, ref searchBoxY);
            clone.transform.localPosition = new Vector3(searchBoxX, searchBoxY, -20f);
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
            if (_clearButtonGo != null)
            {
                _clearButtonGo.SetActive(false);
            }
        }

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
                //lbl.text = string.IsNullOrEmpty(current) ? (SearchInput.defaultText ?? string.Empty) : current;
                //lbl.MarkAsChanged();
            }
            if (_clearButtonGo != null)
            {
                _clearButtonGo.SetActive(!string.IsNullOrEmpty(current));
            }
            if (!_pendingFilterRefresh)
            {
                _pendingFilterRefresh = true;
                MelonCoroutines.Start(RefreshFilterNextFrame());
            }
        }

        private static System.Collections.IEnumerator RefreshFilterNextFrame()
        {
            yield return null;
            _pendingFilterRefresh = false;

            if (_craftingPanel == null || !_craftingPanel.IsEnabled())
                yield break;

            _craftingPanel.ApplyFilter();
        }
    }
}
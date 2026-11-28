using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTLD.UI.Scroll;
using MelonLoader;
using UnityEngine;

namespace CraftingSearch
{
    internal static class SearchBarUI
    {
        private const int BoxWidth = 380;
        private const int BoxHeight = 36;
        private const int PlaceholderFontSize = 13;
        private const int TypedFontSize = 16;
        private const string SearchTextPlaceholder = "Search blueprints (with or without a prefix)"; // placeholder text to guide users on how to use the search bar

        internal static UIInput SearchInput;
        private static Panel_Crafting _craftingPanel;
        private static bool _pendingFilterRefresh;
        private static GameObject _clearButtonGo;
        private static UIInput _templateInput;
        private static UILabel _headerLabel;

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

            // remember the header label so later opens skip reading every label's text
            if (_headerLabel == null || !_headerLabel.transform.IsChildOf(panel.transform))
                _headerLabel = FindBlueprintsHeader(panel);

            UILabel lbl = _headerLabel;
            if (lbl == null)
                return;

            Vector3 localHeaderPos = targetParent.InverseTransformPoint(lbl.transform.position);
            float yOffset = (lbl.height * 0.5f) + (BoxHeight * 0.5f) + 12f;
            const float rightNudgeX = 8f; // Adjust ~10f–30f to taste

            x = localHeaderPos.x + (BoxWidth * 0.5f) - (lbl.width * 0.5f) + rightNudgeX;
            y = localHeaderPos.y + yOffset;
        }

        private static UILabel FindBlueprintsHeader(Panel_Crafting panel)
        {
            foreach (var lbl in panel.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl == null)
                    continue;

                string txt = lbl.text ?? string.Empty;
                string objName = lbl.gameObject != null ? lbl.gameObject.name : string.Empty;

                if (txt.Contains("blueprint", System.StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("blueprint", System.StringComparison.OrdinalIgnoreCase))
                {
                    return lbl;
                }
            }

            return null;
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
                SearchInput.activeTextColor = Color.white;

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
                    ApplyLabelVisibilityAndColor(SearchInput.label, Color.white);
                    SearchInput.label.MarkAsChanged();
                }

                SyncPlaceholderFontSize();
                return;
            }

            // the template lives outside the crafting panel, so keep it across scenes while it's alive
            if (_templateInput == null)
                _templateInput = FindTemplateInput();

            UIInput template = _templateInput;
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
            StripNonUiMonoBehaviours(clone, nameof(UIInput), nameof(UISprite), nameof(UILabel), nameof(UIWidget), nameof(BoxCollider));

            foreach (var w in clone.GetComponentsInChildren<UIWidget>(true))
            {
                w.alpha = 1f;
                w.gameObject.SetActive(true);
            }

            SearchInput = clone.GetComponent<UIInput>() ?? clone.AddComponent<UIInput>();
            UILabel sourceLabel = FindValidFontSource(panel);

            float searchBoxX = 0f;
            float searchBoxY = -200f;

            DestroyExistingWidgets<UISprite>(clone);
            CreateBackgroundSprite(panel, clone);
            DestroyExistingWidgets<UILabel>(clone);

            UILabel label = CreateClonedOrNewLabel("SearchLabel", clone.transform, sourceLabel);

            SearchInput.label = label;
            SearchInput.enabled = true;
            SearchInput.onChange.Clear();
            SearchInput.onSubmit.Clear();
            SearchInput.defaultText = SearchTextPlaceholder;
            SearchInput.value = string.Empty;
            SearchInput.activeTextColor = Color.white;
            SearchInput.caretColor = Color.white;
            SearchInput.characterLimit = 40;
            SearchInput.inputType = UIInput.InputType.Standard;
            SearchInput.validation = UIInput.Validation.None;
            SearchInput.hideInput = false;

            ApplyLabelVisibilityAndColor(label, Color.white);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.maxLineCount = 1;
            label.fontSize = PlaceholderFontSize; // starts empty showing placeholder
            label.width = 360; // adjust width to fit within the box, taking into account the 'x' button and padding
            label.height = label.fontSize * 2; // height should be at least 2x font size to accommodate the text and padding
            label.depth = 500; // depth should be higher than the background sprite to ensure it's visible
            label.transform.localScale = Vector3.one;
            label.transform.localPosition = new Vector3(-185f, 0f, -2f); // position the text to the left side of the box, with some padding
            label.text = SearchTextPlaceholder; // only used the first time the craft panel is opened, after that it falls back to the SearchInput.value
            label.MarkAsChanged();
            SyncPlaceholderFontSize();

            BoxCollider collider = clone.GetComponent<BoxCollider>() ?? clone.AddComponent<BoxCollider>();
            collider.size = new Vector3(BoxWidth, BoxHeight, 1f);
            collider.center = Vector3.zero;
            collider.enabled = true;

            CreateClearButton(clone, sourceLabel);

            EventDelegate.Add(SearchInput.onChange, DelegateSupport.ConvertDelegate<EventDelegate.Callback>(
                new System.Action(OnSearchTextChanged)
            ));

            TryGetAboveBlueprintsPosition(panel, targetParent, ref searchBoxX, ref searchBoxY);
            clone.transform.localPosition = new Vector3(searchBoxX, searchBoxY, -20f);
        }

        private static void CreateBackgroundSprite(Panel_Crafting panel, GameObject clone)
        {
            UISprite bpBgSource = FindBlueprintRowBackgroundSprite(panel);
            if (bpBgSource == null)
                return;

            GameObject bgGo = new("SearchBg");
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

        private static void CreateClearButton(GameObject clone, UILabel sourceLabel)
        {
            // add clearing button to the right side of the search box
            _clearButtonGo = new GameObject("ClearButton");
            _clearButtonGo.transform.SetParent(clone.transform, false);
            _clearButtonGo.layer = clone.layer;
            _clearButtonGo.transform.localPosition = new Vector3(177f, 0f, -3f); // position the X to the right side of the box, with some padding
            _clearButtonGo.transform.localScale = Vector3.one;

            UILabel clearLabel = CreateClonedOrNewLabel("ClearLabel", _clearButtonGo.transform, sourceLabel);
            ApplyLabelVisibilityAndColor(clearLabel, new Color(0.85f, 0.85f, 0.85f, 1f));
            clearLabel.pivot = UIWidget.Pivot.Center;
            clearLabel.alignment = NGUIText.Alignment.Center;
            clearLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            clearLabel.maxLineCount = 1;
            clearLabel.fontSize = TypedFontSize;
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
                    MelonCoroutines.Start(RefocusAfterClear()); // keep the search box focused after clicking the X
                })
            );

            _clearButtonGo.SetActive(false);
        }

        private static UILabel CreateClonedOrNewLabel(string name, Transform parent, UILabel sourceLabel)
        {
            UILabel createdLabel = null;

            if (sourceLabel != null)
            {
                GameObject labelGo = UnityEngine.Object.Instantiate(sourceLabel.gameObject, parent);
                labelGo.name = name;
                SetLayerRecursively(labelGo, parent.gameObject.layer);
                StripNonUiMonoBehaviours(labelGo, nameof(UILabel));
                createdLabel = labelGo.GetComponent<UILabel>();
            }

            if (createdLabel == null)
            {
                GameObject labelGo = new(name);
                labelGo.transform.SetParent(parent, false);
                labelGo.layer = parent.gameObject.layer;
                createdLabel = labelGo.AddComponent<UILabel>();
            }

            return createdLabel;
        }

        private static void StripNonUiMonoBehaviours(GameObject root, params string[] allowedTypeNames)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetIl2CppType().Name;
                bool allowed = false;
                for (int i = 0; i < allowedTypeNames.Length; i++)
                {
                    if (typeName == allowedTypeNames[i])
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    UnityEngine.Object.Destroy(mb);
                }
            }
        }

        private static void DestroyExistingWidgets<T>(GameObject root) where T : Component
        {
            foreach (var comp in root.GetComponentsInChildren<T>(true))
            {
                if (comp != null)
                {
                    UnityEngine.Object.Destroy(comp.gameObject == root ? (UnityEngine.Object)comp : comp.gameObject);
                }
            }
        }

        private static UILabel FindValidFontSource(Panel_Crafting panel)
        {
            if (panel == null)
                return null;

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
                if (input == null)
                    continue;

                if (input.label != null && (input.label.bitmapFont != null || input.label.trueTypeFont != null))
                {
                    if (fallback == null)
                        fallback = input;

                    string rootName = input.transform.root != null ? input.transform.root.name : "null";
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

        private static void ApplyLabelVisibilityAndColor(UILabel lbl, Color color)
        {
            if (lbl == null)
                return;

            lbl.gameObject.SetActive(true);
            lbl.enabled = true;
            lbl.alpha = 1f;
            lbl.color = color;
        }

        internal static void Clear()
        {
            if (SearchInput != null)
            {
                SearchInput.value = string.Empty;
                SearchInput.isSelected = false;
                if (UICamera.selectedObject == SearchInput.gameObject)
                {
                    UICamera.selectedObject = null;
                }

                if (SearchInput.label != null)
                {
                    SearchInput.label.text = SearchInput.defaultText ?? string.Empty;
                    ApplyLabelVisibilityAndColor(SearchInput.label, Color.white);
                    SearchInput.label.MarkAsChanged();
                }
            }

            _clearButtonGo?.SetActive(false);
            ForcePlaceholderStyle();
            MelonCoroutines.Start(SyncPlaceholderNextFrame());
        }

        private static System.Collections.IEnumerator SyncPlaceholderNextFrame()
        {
            yield return null;
            ForcePlaceholderStyle();
            yield return new WaitForEndOfFrame();
            ForcePlaceholderStyle();
        }

        // the X click selects the clear button (deselecting the input) and Clear() also deselects it,
        // so re-select the input after NGUI and the placeholder sync have finished for this click
        private static System.Collections.IEnumerator RefocusAfterClear()
        {
            yield return null;
            yield return null;

            if (SearchInput == null || _craftingPanel == null || !_craftingPanel.IsEnabled())
                yield break;

            SearchInput.isSelected = true;
            UICamera.selectedObject = SearchInput.gameObject;
            SearchInput.cursorPosition = 0;
            SyncPlaceholderFontSize();
        }

        private static void OnSearchTextChanged()
        {
            string current = SearchInput != null && SearchInput.value != null ? SearchInput.value : string.Empty;

            if (SearchInput != null && SearchInput.label != null)
            {
                ApplyLabelVisibilityAndColor(SearchInput.label, Color.white);
            }

            SyncPlaceholderFontSize();
            _clearButtonGo?.SetActive(!string.IsNullOrEmpty(current));

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

            NameSearch.ApplySearchFromQuery(_craftingPanel);
        }

        internal static void ForcePlaceholderStyle()
        {
            if (SearchInput == null || SearchInput.label == null)
                return;

            UILabel lbl = SearchInput.label;
            lbl.fontSize = PlaceholderFontSize;
            lbl.height = PlaceholderFontSize * 2;
            lbl.color = Color.white;

            if (string.IsNullOrEmpty(SearchInput.value))
            {
                lbl.text = SearchInput.defaultText ?? string.Empty;
            }

            lbl.MarkAsChanged();
            try { lbl.ProcessAndRequest(); } catch { }
        }

        // helper method to adjust the font size of the search input's label based on whether the user is typing or if the placeholder text is showing
        internal static void SyncPlaceholderFontSize()
        {
            if (SearchInput == null || SearchInput.label == null)
                return;

            bool isFocused = false;
            try { isFocused = SearchInput.isSelected; } catch { }

            bool showingPlaceholder = !isFocused && string.IsNullOrEmpty(SearchInput.value);
            int targetSize = showingPlaceholder ? PlaceholderFontSize : TypedFontSize;

            if (SearchInput.label.fontSize != targetSize)
            {
                SearchInput.label.fontSize = targetSize;
                SearchInput.label.height = targetSize * 2;
                SearchInput.label.MarkAsChanged();
            }

            if (showingPlaceholder)
            {
                SearchInput.label.color = Color.white;
                SearchInput.label.MarkAsChanged();
            }
        }
    }
}
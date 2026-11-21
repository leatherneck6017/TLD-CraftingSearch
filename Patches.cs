using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;

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

                    string displayName = bp.GetDisplayedNameWithCount() ?? bp.name ?? string.Empty;
                    if (!displayName.Contains(query, System.StringComparison.OrdinalIgnoreCase))
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

            try
            {
                __instance.RefreshAvailableBlueprints();
            }
            catch
            {
                // Safe fallback if RefreshAvailableBlueprints throws on a particular state
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

        private static void RefreshCraftingScrollList(Panel_Crafting panel, Il2CppSystem.Collections.Generic.List<BlueprintData> filtered)
        {
            if (panel == null)
                return;

            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;

            if (!_loggedScrollList)
            {
                _loggedScrollList = true;
                foreach (var method in typeof(Panel_Crafting).GetMethods(flags))
                {
                    string mName = method.Name;
                    if (mName.Contains("Refresh", System.StringComparison.OrdinalIgnoreCase) ||
                        mName.Contains("Blueprint", System.StringComparison.OrdinalIgnoreCase) ||
                        mName.Contains("List", System.StringComparison.OrdinalIgnoreCase) ||
                        mName.Contains("Select", System.StringComparison.OrdinalIgnoreCase))
                    {
                        MelonLoader.MelonLogger.Msg($"Panel_Crafting method: {mName} (params={method.GetParameters().Length})");
                    }
                }
            }

            // Try common refresh/update method names on Panel_Crafting that take 0 arguments
            string[] candidateMethods =
            [
                "RefreshBlueprintList",
                "RefreshBlueprints",
                "RefreshList",
                "UpdateBlueprintList",
                "PopulateBlueprintList",
                "RefreshSelectedBlueprint"
            ];

            foreach (string candidate in candidateMethods)
            {
                var method = typeof(Panel_Crafting).GetMethod(candidate, flags, null, System.Type.EmptyTypes, null);
                if (method != null)
                {
                    try
                    {
                        method.Invoke(panel, null);
                    }
                    catch
                    {
                        // Ignore if signature or state doesn't match
                    }
                }
            }
        }

        private static bool _loggedScrollList;

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

    [HarmonyPatch(typeof(UIKeyBinding))]
    internal static class UIKeyBinding_Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static bool Update_Prefix()
        {
            return !SearchBarUI.IsTyping;
        }
    }
}
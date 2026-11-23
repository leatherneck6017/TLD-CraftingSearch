using HarmonyLib;
using Il2Cpp;

namespace CraftingSearch.src
{
    [HarmonyPatch(typeof(Panel_Crafting))]
    internal static class Panel_Crafting_Patches
    {
        private static bool _wasTyping;
        private static bool _pendingTabFocus;

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
                NameSearch.EnsureScrollBehaviourPatched();
                SearchBarUI.AttachTo(__instance);
            }
            else
            {
                NameSearch.SetNativeInputLock(false);
                _wasTyping = false;
                SearchBarUI.Clear();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Panel_Crafting.ApplyFilter))]
        private static void ApplyFilter_Postfix(Panel_Crafting __instance)
        {
            NameSearch.ApplySearchFilter(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static bool Update_Prefix(Panel_Crafting __instance)
        {
            var searchInput = SearchBarUI.SearchInput;

            if (_pendingTabFocus)
            {
                _pendingTabFocus = false;
                if (searchInput != null && __instance != null && __instance.IsEnabled())
                {
                    searchInput.isSelected = true;
                    if (UICamera.currentCamera != null || UICamera.mainCamera != null)
                    {
                        UICamera.selectedObject = searchInput.gameObject;
                    }
                }
            }

            if (searchInput != null && __instance != null && __instance.IsEnabled() &&
                UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Tab))
            {
                if (!searchInput.isSelected)
                {
                    searchInput.isSelected = true;
                    _pendingTabFocus = true;
                    if (UICamera.currentCamera != null || UICamera.mainCamera != null)
                    {
                        UICamera.selectedObject = searchInput.gameObject;
                    }
                }
                else
                {
                    searchInput.isSelected = false;
                    _pendingTabFocus = false;
                    if (UICamera.selectedObject == searchInput.gameObject)
                    {
                        UICamera.selectedObject = null;
                    }
                }
            }

            bool isTyping = SearchBarUI.IsTyping;
            if (isTyping != _wasTyping)
            {
                _wasTyping = isTyping;
                NameSearch.SetNativeInputLock(isTyping);
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

            return true;
        }
    }
}
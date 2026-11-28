using MelonLoader;

[assembly: MelonInfo(typeof(CraftingSearch.CraftingSearchMod), "CraftingSearch", "1.1.1", "leatherneck6017")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CraftingSearch
{
    public class CraftingSearchMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ScrollListSync.PatchScrollBehaviour(HarmonyInstance);
            MelonLogger.Msg("CraftingSearch initialized.");
        }
    }
}

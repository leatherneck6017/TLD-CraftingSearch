using MelonLoader;

[assembly: MelonInfo(typeof(CraftingSearch.CraftingSearchMod), "CraftingSearch", "1.0.0", "leatherneck6017")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CraftingSearch
{
    public class CraftingSearchMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("CraftingSearch initialized.");
        }
    }
}

using MelonLoader;

[assembly: MelonInfo(typeof(CraftingSearch.Main), "CraftingSearch", "1.0.0", "leatherneck6017")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CraftingSearch
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("CraftingSearch initialized.");
        }
    }
}

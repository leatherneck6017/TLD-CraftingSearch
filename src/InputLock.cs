namespace CraftingSearch
{
    internal static class InputLock
    {
        internal static void SetNativeInputLock(bool locked)
        {
            try
            {
                var reInputType = System.Type.GetType("Il2CppRewired.ReInput, Il2CppRewired_Core");
                if (reInputType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        reInputType = asm.GetType("Il2CppRewired.ReInput") ?? asm.GetType("Rewired.ReInput");
                        if (reInputType != null)
                            break;
                    }
                }

                if (reInputType == null)
                    return;

                var playersProp = reInputType.GetProperty("players");
                object players = playersProp?.GetValue(null);
                if (players == null)
                    return;

                var getPlayerMethod = players.GetType().GetMethod("GetPlayer", [typeof(int)]);
                object player = getPlayerMethod?.Invoke(players, [0]);
                if (player == null)
                    return;

                var controllersProp = player.GetType().GetProperty("controllers");
                object controllers = controllersProp?.GetValue(player);
                var mapsProp = controllers?.GetType().GetProperty("maps");
                object maps = mapsProp?.GetValue(controllers);

                var setMapsEnabled = maps?.GetType().GetMethod("SetMapsEnabled", [typeof(bool), typeof(int)]);
                if (setMapsEnabled != null)
                {
                    setMapsEnabled.Invoke(maps, [!locked, 0]);
                }
            }
            catch (System.Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[CraftingSearch] Rewired lock error: {ex.Message}");
            }
        }
    }
}
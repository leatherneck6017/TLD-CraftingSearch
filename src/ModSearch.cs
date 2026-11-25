using System;
using System.Reflection;
using Il2CppTLD.Gear;

namespace CraftingSearch
{
    internal static class ModSearch
    {
        private static MethodInfo _cachedInstanceLookup;
        private static MethodInfo _cachedStringLookup;
        private static bool _searchedMethod;

        internal static bool MatchesAnyModField(BlueprintData bp, string term)
        {
            if (bp == null || string.IsNullOrWhiteSpace(term))
                return false;

            if (!TryGetSourceInfo(bp, out string sourceLabel, out string friendlyModName))
                return false;

            return (!string.IsNullOrWhiteSpace(friendlyModName) &&
                    friendlyModName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(sourceLabel) &&
                    sourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetSourceInfo(BlueprintData bp, out string sourceLabel, out string friendlyModName)
        {
            sourceLabel = null;
            friendlyModName = null;

            if (bp == null)
                return false;

            EnsureMethods();

            // Prefer the instance overload so copies that share a vanilla id are identified.
            if (_cachedInstanceLookup != null)
                return TryInvokeLookup(_cachedInstanceLookup, bp, out sourceLabel, out friendlyModName);

            // Fallback if the running utils DLL only has the string overload.
            if (_cachedStringLookup == null)
                return false;

            string displayed = null;
            try
            {
                displayed = bp.GetDisplayedNameWithCount();
            }
            catch
            {
                displayed = null;
            }

            string[] candidates = [bp.name, displayed];
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (TryInvokeLookup(_cachedStringLookup, candidate, out sourceLabel, out friendlyModName))
                    return true;
            }

            return false;
        }

        private static bool TryInvokeLookup(
            MethodInfo method,
            object firstArg,
            out string sourceLabel,
            out string friendlyModName)
        {
            sourceLabel = null;
            friendlyModName = null;

            try
            {
                object[] args = [firstArg, null, null];
                object res = method.Invoke(null, args);
                if (res is bool matched && matched)
                {
                    sourceLabel = args[1] as string;
                    friendlyModName = args[2] as string;
                    return true;
                }
            }
            catch
            {
                // Ignore lookup errors on individual entries
            }

            return false;
        }

        private static void EnsureMethods()
        {
            if (_searchedMethod)
                return;

            _searchedMethod = true;

            Type bpInfoType = FindUtilsType();
            if (bpInfoType == null)
                return;

            MethodInfo[] methods;
            try
            {
                methods = bpInfoType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.Name != "TryGetModSourceInfo")
                    continue;

                ParameterInfo[] parameters;
                try
                {
                    parameters = method.GetParameters();
                }
                catch
                {
                    continue;
                }

                if (parameters.Length != 3 || parameters[0].ParameterType == null)
                    continue;

                if (parameters[0].ParameterType == typeof(string))
                {
                    if (_cachedStringLookup == null)
                        _cachedStringLookup = method;
                }
                else if (_cachedInstanceLookup == null)
                {
                    _cachedInstanceLookup = method;
                }
            }
        }

        private static Type FindUtilsType()
        {
            string[] typeNames =
            [
                "ModdedBlueprintUtils.BlueprintInfoMod",
                "BlueprintInfo.BlueprintInfoMod"
            ];

            string[] assemblyQualifiedNames =
            [
                "ModdedBlueprintUtils.BlueprintInfoMod, ModdedBlueprintUtils",
                "BlueprintInfo.BlueprintInfoMod, BlueprintInfo"
            ];

            for (int i = 0; i < assemblyQualifiedNames.Length; i++)
            {
                try
                {
                    Type type = Type.GetType(assemblyQualifiedNames[i], throwOnError: false);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Continue to assembly scan
                }
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];
                if (asm == null)
                    continue;

                for (int j = 0; j < typeNames.Length; j++)
                {
                    try
                    {
                        Type type = asm.GetType(typeNames[j], throwOnError: false, ignoreCase: false);
                        if (type != null)
                            return type;
                    }
                    catch
                    {
                        // Skip assemblies that cannot be inspected
                    }
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Il2CppTLD.Gear;

namespace CraftingSearch
{
    internal enum SearchMode
    {
        Both,
        NameOnly,
        IngredientOnly
    }

    internal static class IngredientSearch
    {
        private const BindingFlags MemberFlags = BindingFlags.Public |
                                                 BindingFlags.NonPublic |
                                                 BindingFlags.Instance;

        private static readonly string[] IngredientPrefixes =
        [
            "ingredient:",
            "ingredient!",
            "ingredient/",
            "ingredient\\",
            "ing:",
            "ing!",
            "ing/",
            "ing\\"
        ];

        private static readonly string[] NamePrefixes =
        [
            "name:",
            "name!",
            "name/",
            "name\\"
        ];

        private static readonly string[] CandidateMemberNames =
        [
            "m_RequiredGear", "RequiredGear",
            "m_Ingredients", "Ingredients",
            "m_RequiredItems", "RequiredItems",
            "m_CraftMaterials", "CraftMaterials",
            "m_Materials", "Materials",
            "m_GearItems", "GearItems",
            "m_RequiredBlueprints", "RequiredBlueprints"
        ];

        // Only check displayed/localized names on ingredient items—exclude internal/asset "name" fields
        private static readonly string[] NameMembers =
        [
            "m_LocalizedDisplayName", "LocalizedDisplayName",
            "DisplayName", "m_DisplayName"
        ];

        private static readonly string[] NestedItemMembers =
        [
            "m_GearItem", "GearItem",
            "m_Item", "Item",
            "m_GearItemData", "GearItemData"
        ];

        // member lookups only depend on the type, so resolve each one once. values are still read live
        private static readonly Dictionary<(Type, string), PropertyInfo> _propCache = new();
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = new();
        private static readonly Dictionary<Type, (PropertyInfo count, PropertyInfo indexer)> _listAccessCache = new();
        private static readonly Dictionary<Type, FieldInfo[]> _scanFieldCache = new();

        internal static SearchMode ParseQuery(string rawQuery, out string term)
        {
            term = string.Empty;
            if (string.IsNullOrWhiteSpace(rawQuery))
                return SearchMode.Both;

            string trimmed = rawQuery.Trim();

            foreach (string prefix in IngredientPrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    term = trimmed.Substring(prefix.Length).Trim();
                    return SearchMode.IngredientOnly;
                }
            }

            foreach (string prefix in NamePrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    term = trimmed.Substring(prefix.Length).Trim();
                    return SearchMode.NameOnly;
                }
            }

            term = trimmed;
            return SearchMode.Both;
        }

        internal static bool TryParseIngredientQualifier(string rawQuery, out string ingredientTerm)
        {
            ingredientTerm = string.Empty;
            if (string.IsNullOrWhiteSpace(rawQuery))
                return false;

            SearchMode mode = ParseQuery(rawQuery, out string parsedTerm);
            if (mode == SearchMode.IngredientOnly)
            {
                ingredientTerm = parsedTerm;
                return true;
            }

            return false;
        }

        internal static bool MatchesAnyIngredient(BlueprintData bp, string query)
        {
            if (bp == null || string.IsNullOrWhiteSpace(query))
                return false;

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Type type = bp.GetType();

            foreach (string memberName in CandidateMemberNames)
            {
                if (TryGetMemberValue(bp, type, memberName, out object val) &&
                    val != null &&
                    ObjectMatchesQuery(val, query, visited, depth: 0))
                {
                    return true;
                }
            }

            // Fallback: scan any array/list/collection members on BlueprintData that might hold ingredient entries
            foreach (FieldInfo field in GetScanFields(type))
            {
                object val = null;
                try { val = field.GetValue(bp); } catch { }
                if (val != null && ObjectMatchesQuery(val, query, visited, depth: 0))
                    return true;
            }

            return false;
        }

        internal static void CollectNames(BlueprintData bp, List<string> dest)
        {
            if (bp == null || dest == null)
                return;

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Type type = bp.GetType();

            foreach (string memberName in CandidateMemberNames)
            {
                if (TryGetMemberValue(bp, type, memberName, out object val) && val != null)
                    CollectFromObject(val, dest, visited, depth: 0);
            }

            foreach (FieldInfo field in GetScanFields(type))
            {
                object val = null;
                try { val = field.GetValue(bp); } catch { }
                if (val != null)
                    CollectFromObject(val, dest, visited, depth: 0);
            }
        }

        private static FieldInfo[] GetScanFields(Type type)
        {
            if (_scanFieldCache.TryGetValue(type, out FieldInfo[] cached))
                return cached;

            var list = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(MemberFlags))
            {
                if (field.FieldType == typeof(string) || field.FieldType.IsPrimitive)
                    continue;

                string lower = field.Name.ToLowerInvariant();
                if (lower.Contains("gear") ||
                    lower.Contains("item") ||
                    lower.Contains("ingred") ||
                    lower.Contains("mat") ||
                    lower.Contains("req"))
                {
                    list.Add(field);
                }
            }

            cached = list.ToArray();
            _scanFieldCache[type] = cached;
            return cached;
        }

        private static bool ObjectMatchesQuery(
            object obj,
            string query,
            HashSet<object> visited,
            int depth)
        {
            if (obj == null || depth > 4)
                return false;

            if (!obj.GetType().IsValueType && !visited.Add(obj))
                return false;

            if (obj is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item != null && ObjectMatchesQuery(item, query, visited, depth + 1))
                        return true;
                }
            }

            Type type = obj.GetType();
            if (!_listAccessCache.TryGetValue(type, out var access))
            {
                access = (type.GetProperty("Count") ?? type.GetProperty("Length"),
                          type.GetProperty("Item", [typeof(int)]) ?? type.GetProperty("default", [typeof(int)]));
                _listAccessCache[type] = access;
            }

            PropertyInfo countProp = access.count;
            PropertyInfo itemIndexer = access.indexer;
            if (countProp != null && itemIndexer != null)
            {
                try
                {
                    int count = Convert.ToInt32(countProp.GetValue(obj));
                    for (int i = 0; i < count; i++)
                    {
                        object elem = itemIndexer.GetValue(obj, [i]);
                        if (elem != null && ObjectMatchesQuery(elem, query, visited, depth + 1))
                            return true;
                    }
                }
                catch { }
            }

            foreach (string nameMember in NameMembers)
            {
                if (TryGetStringMember(obj, type, nameMember, out string strVal) &&
                    !string.IsNullOrEmpty(strVal) &&
                    strVal.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string subMember in NestedItemMembers)
            {
                if (TryGetMemberValue(obj, type, subMember, out object sub) &&
                    sub != null &&
                    ObjectMatchesQuery(sub, query, visited, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectFromObject(
            object obj,
            List<string> dest,
            HashSet<object> visited,
            int depth)
        {
            if (obj == null || depth > 4)
                return;

            if (!obj.GetType().IsValueType && !visited.Add(obj))
                return;

            if (obj is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item != null)
                        CollectFromObject(item, dest, visited, depth + 1);
                }
            }

            Type type = obj.GetType();
            if (!_listAccessCache.TryGetValue(type, out var access))
            {
                access = (type.GetProperty("Count") ?? type.GetProperty("Length"),
                          type.GetProperty("Item", [typeof(int)]) ?? type.GetProperty("default", [typeof(int)]));
                _listAccessCache[type] = access;
            }

            PropertyInfo countProp = access.count;
            PropertyInfo itemIndexer = access.indexer;
            if (countProp != null && itemIndexer != null)
            {
                try
                {
                    int count = Convert.ToInt32(countProp.GetValue(obj));
                    for (int i = 0; i < count; i++)
                    {
                        object elem = itemIndexer.GetValue(obj, [i]);
                        if (elem != null)
                            CollectFromObject(elem, dest, visited, depth + 1);
                    }
                }
                catch { }
            }

            foreach (string nameMember in NameMembers)
            {
                if (TryGetStringMember(obj, type, nameMember, out string strVal) &&
                    !string.IsNullOrEmpty(strVal) &&
                    !dest.Contains(strVal))
                {
                    dest.Add(strVal);
                }
            }

            foreach (string subMember in NestedItemMembers)
            {
                if (TryGetMemberValue(obj, type, subMember, out object sub) && sub != null)
                    CollectFromObject(sub, dest, visited, depth + 1);
            }
        }

        private static bool TryGetMemberValue(object target, Type type, string memberName, out object value)
        {
            value = null;
            if (target == null || type == null)
                return false;

            if (!_propCache.TryGetValue((type, memberName), out PropertyInfo prop))
            {
                prop = type.GetProperty(memberName, MemberFlags);
                if (prop != null && prop.GetIndexParameters().Length != 0)
                    prop = null;
                _propCache[(type, memberName)] = prop;
            }

            if (prop != null)
            {
                try
                {
                    value = prop.GetValue(target);
                    return true;
                }
                catch { }
            }

            if (!_fieldCache.TryGetValue((type, memberName), out FieldInfo field))
            {
                field = type.GetField(memberName, MemberFlags);
                _fieldCache[(type, memberName)] = field;
            }

            if (field != null)
            {
                try
                {
                    value = field.GetValue(target);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static bool TryGetStringMember(object target, Type type, string memberName, out string value)
        {
            value = null;
            if (!TryGetMemberValue(target, type, memberName, out object raw) || raw is not string str)
                return false;

            value = str;
            return true;
        }
    }
}
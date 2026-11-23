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

        private static readonly string[] NameMembers =
        [
            "m_LocalizedDisplayName", "LocalizedDisplayName",
            "DisplayName", "m_DisplayName",
            "name", "Name"
        ];

        private static readonly string[] NestedItemMembers =
        [
            "m_GearItem", "GearItem",
            "m_Item", "Item",
            "m_GearItemData", "GearItemData"
        ];

        internal static SearchMode ParseQuery(string rawQuery, out string term)
        {
            term = string.Empty;
            if (string.IsNullOrWhiteSpace(rawQuery))
                return SearchMode.Both;

            string trimmed = rawQuery.Trim();

            if (trimmed.StartsWith("ing:", StringComparison.OrdinalIgnoreCase))
            {
                term = trimmed.Substring("ing:".Length).Trim();
                return SearchMode.IngredientOnly;
            }

            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                term = trimmed.Substring("name:".Length).Trim();
                return SearchMode.NameOnly;
            }

            term = trimmed;
            return SearchMode.Both;
        }

        internal static bool TryParseIngredientQualifier(string rawQuery, out string ingredientTerm)
        {
            ingredientTerm = string.Empty;
            if (string.IsNullOrWhiteSpace(rawQuery))
                return false;

            string trimmed = rawQuery.Trim();

            if (trimmed.StartsWith("ingredient:", StringComparison.OrdinalIgnoreCase))
            {
                ingredientTerm = trimmed.Substring("ingredient:".Length).Trim();
                return true;
            }

            if (trimmed.StartsWith("ing:", StringComparison.OrdinalIgnoreCase))
            {
                ingredientTerm = trimmed.Substring("ing:".Length).Trim();
                return true;
            }

            return false;
        }
        
        internal static bool MatchesAnyIngredient(BlueprintData bp, string query)
        {
            if (bp == null || string.IsNullOrWhiteSpace(query))
                return false;

            const BindingFlags flags = BindingFlags.Public |
                                     BindingFlags.NonPublic |
                                     BindingFlags.Instance;

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Type type = bp.GetType();

            foreach (string memberName in CandidateMemberNames)
            {
                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                {
                    object val = null;
                    try { val = field.GetValue(bp); } catch { }
                    if (val != null && ObjectMatchesQuery(val, query, visited, depth: 0))
                        return true;
                }

                PropertyInfo prop = type.GetProperty(memberName, flags);
                if (prop != null && prop.GetIndexParameters().Length == 0)
                {
                    object val = null;
                    try { val = prop.GetValue(bp); } catch { }
                    if (val != null && ObjectMatchesQuery(val, query, visited, depth: 0))
                        return true;
                }
            }

            // Fallback: scan any array/list/collection members on BlueprintData that might hold ingredient entries
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.FieldType == typeof(string) || field.FieldType.IsPrimitive)
                    continue;

                string lower = field.Name.ToLowerInvariant();
                if (!lower.Contains("gear") &&
                    !lower.Contains("item") &&
                    !lower.Contains("ingred") &&
                    !lower.Contains("mat") &&
                    !lower.Contains("req"))
                {
                    continue;
                }

                object val = null;
                try { val = field.GetValue(bp); } catch { }
                if (val != null && ObjectMatchesQuery(val, query, visited, depth: 0))
                    return true;
            }

            return false;
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

            if (obj is string s)
            {
                return s.Contains(query, StringComparison.OrdinalIgnoreCase);
            }

            if (obj is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item != null && ObjectMatchesQuery(item, query, visited, depth + 1))
                        return true;
                }
            }

            Type type = obj.GetType();
            PropertyInfo countProp = type.GetProperty("Count") ?? type.GetProperty("Length");
            PropertyInfo itemIndexer = type.GetProperty("Item", [typeof(int)]) ?? type.GetProperty("default", [typeof(int)]);
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

            const BindingFlags flags = BindingFlags.Public |
                                     BindingFlags.NonPublic |
                                     BindingFlags.Instance;

            foreach (string nameMember in NameMembers)
            {
                PropertyInfo p = type.GetProperty(nameMember, flags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        if (p.GetValue(obj) is string strVal && !string.IsNullOrEmpty(strVal) &&
                            strVal.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch { }
                }

                FieldInfo f = type.GetField(nameMember, flags);
                if (f != null)
                {
                    try
                    {
                        if (f.GetValue(obj) is string strVal && !string.IsNullOrEmpty(strVal) &&
                            strVal.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }

            foreach (string subMember in NestedItemMembers)
            {
                PropertyInfo p = type.GetProperty(subMember, flags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        object sub = p.GetValue(obj);
                        if (sub != null && ObjectMatchesQuery(sub, query, visited, depth + 1))
                            return true;
                    }
                    catch { }
                }

                FieldInfo f = type.GetField(subMember, flags);
                if (f != null)
                {
                    try
                    {
                        object sub = f.GetValue(obj);
                        if (sub != null && ObjectMatchesQuery(sub, query, visited, depth + 1))
                            return true;
                    }
                    catch { }
                }
            }

            return false;
        }
    }
}
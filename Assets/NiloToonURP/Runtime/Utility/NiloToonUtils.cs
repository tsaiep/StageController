using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiloToon.NiloToonURP
{
    /// <summary>
    /// Contains helper functions that you can use.
    /// </summary>
    public static class NiloToonUtils
    {
        public static bool NameHasKeyword(string name, string keyword)
        {
            return name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool NameEqualsKeywordIgnoreCase(string name, string keyword)
        {
            return string.Equals(name, keyword, StringComparison.OrdinalIgnoreCase);
        }
        
        public static Transform DepthSearch(Transform parent, string targetName, string[] banNameList)
        {
            foreach (Transform child in parent)
            {
                if (NiloToonUtils.NameHasKeyword(child.name, targetName))
                {
                    bool isBanned = false;

                    if (banNameList != null)
                    {
                        foreach (string banName in banNameList)
                        {
                            if (NiloToonUtils.NameHasKeyword(child.name, banName))
                            {
                                isBanned = true;
                                break;
                            }
                        }
                    }

                    if (!isBanned)
                    {
                        return child;
                    }
                }

                var result = DepthSearch(child, targetName, banNameList);
                if (result != null)
                {
                    return result;
                }
            }

            // find nothing
            return null;
        }
        
        /// <summary>
        /// Searches all descendants and adds matching transforms to the provided list <b>only if not already present</b>.
        /// This avoids duplicates and new list allocations.
        /// </summary>
        /// <param name="parent">Root transform to search.</param>
        /// <param name="targetName">Keyword to match (case-insensitive substring).</param>
        /// <param name="banNameList">Optional banned keywords (case-insensitive).</param>
        /// <param name="outputList">Existing list to be appended (duplicates avoided, no clearing).</param>
        public static void DepthSearchAllAddUnique(Transform parent, string targetName, string[] banNameList, List<Transform> outputList)
        {
            if (outputList == null)
                throw new ArgumentNullException(nameof(outputList));

            if (parent == null || string.IsNullOrEmpty(targetName))
                return;

            DepthSearchAllAddUniqueRecursive(parent, targetName, banNameList, outputList);
        }

        private static void DepthSearchAllAddUniqueRecursive(Transform current, string targetName, string[] banNameList, List<Transform> results)
        {
            foreach (Transform child in current)
            {
                if (NameHasKeyword(child.name, targetName))
                {
                    bool isBanned = false;

                    if (banNameList != null)
                    {
                        foreach (string banName in banNameList)
                        {
                            if (NameHasKeyword(child.name, banName))
                            {
                                isBanned = true;
                                break;
                            }
                        }
                    }

                    if (!isBanned && !results.Contains(child))
                    {
                        results.Add(child);
                    }
                }

                DepthSearchAllAddUniqueRecursive(child, targetName, banNameList, results);
            }
        }
    }
}
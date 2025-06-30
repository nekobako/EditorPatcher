#if UNITY_2022_2_OR_NEWER
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class RebasePrefab
    {
        private const string k_KeepAllMenuPath = "Assets/Prefab/Rebase and Keep All...";
        private const string k_KeepOverridesMenuPath = "Assets/Prefab/Rebase and Keep Overrides...";

        [MenuItem(k_KeepAllMenuPath, true, 50)]
        [MenuItem(k_KeepOverridesMenuPath, true, 50)]
        private static bool Validate()
        {
            return Selection.gameObjects.Any() && Selection.gameObjects.All(EditorUtility.IsPersistent);
        }

        [MenuItem(k_KeepAllMenuPath, false, 50)]
        private static void RebaseKeepAll()
        {
            Rebase(Selection.gameObjects, true);
        }

        [MenuItem(k_KeepOverridesMenuPath, false, 50)]
        private static void RebaseKeepOverrides()
        {
            Rebase(Selection.gameObjects, false);
        }

        private static void Rebase(GameObject[] targetPrefabAssets, bool keepAll)
        {
            SearchService.ShowObjectPicker((obj, canceled) =>
            {
                if (canceled || obj is not GameObject basePrefabAsset || !EditorUtility.IsPersistent(obj))
                {
                    return;
                }
                foreach (var targetPrefabAsset in targetPrefabAssets)
                {
                    if (basePrefabAsset == targetPrefabAsset || GetAncestors(basePrefabAsset).Contains(targetPrefabAsset))
                    {
                        continue;
                    }
                    Rebase(basePrefabAsset, targetPrefabAsset, keepAll);
                }
            }, null, "p: t:[Prefab, Model]", null, typeof(GameObject));
        }

        private static void Rebase(GameObject basePrefabAsset, GameObject targetPrefabAsset, bool keepAll)
        {
            var targetPrefabPath = AssetDatabase.GetAssetPath(targetPrefabAsset);
            var targetPrefabInstance = PrefabUtility.InstantiatePrefab(targetPrefabAsset) as GameObject;

            PrefabUtility.UnpackPrefabInstance(targetPrefabInstance, keepAll ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            if (PrefabUtility.IsOutermostPrefabInstanceRoot(targetPrefabInstance))
            {
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(targetPrefabInstance, basePrefabAsset, new()
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                    changeRootNameToAssetName = true,
                }, InteractionMode.AutomatedAction);
            }
            else
            {
                PrefabUtility.ConvertToPrefabInstance(targetPrefabInstance, basePrefabAsset, new()
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    componentsNotMatchedBecomesOverride = keepAll,
                    gameObjectsNotMatchedBecomesOverride = keepAll,
                    recordPropertyOverridesOfMatches = keepAll,
                    changeRootNameToAssetName = true,
                }, InteractionMode.AutomatedAction);
            }

            var rebasedPrefabPath = AssetDatabase.GenerateUniqueAssetPath(targetPrefabPath.Replace(".prefab", " Rebased.prefab"));
            var rebasedPrefabAsset = PrefabUtility.SaveAsPrefabAsset(targetPrefabInstance, rebasedPrefabPath);

            Object.DestroyImmediate(targetPrefabInstance);

            foreach (var childPrefabAsset in GetChildren(targetPrefabAsset))
            {
                Rebase(rebasedPrefabAsset, childPrefabAsset, keepAll);
            }
        }

        private static IEnumerable<GameObject> GetAncestors(GameObject prefab)
        {
            while (prefab != null)
            {
                yield return prefab = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            }
        }

        private static IEnumerable<GameObject> GetChildren(GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var getVariantParentGuid = typeof(PrefabUtility).GetMethod("GetVariantParentGUID", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
            var getMainAssetInstanceId = typeof(AssetDatabase).GetMethod("GetMainAssetInstanceID", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            return AssetDatabase.FindAssets("t:Prefab")
                .Where(x => getVariantParentGuid.Invoke(null, new[] { getMainAssetInstanceId.Invoke(null, new[] { AssetDatabase.GUIDToAssetPath(x) }) }) as string == guid)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>);
        }
    }
}
#endif

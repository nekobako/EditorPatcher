#if UNITY_2022_2_OR_NEWER
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class RebaseAndKeepOverrides
    {
        private const string k_MenuPath = "Assets/Prefab/Rebase and Keep Overrides...";

        [MenuItem(k_MenuPath, true)]
        private static bool Validate()
        {
            return Selection.gameObjects.Length > 0 && Selection.gameObjects.Length == Selection.assetGUIDs.Length;
        }

        [MenuItem(k_MenuPath, false)]
        private static void Execute()
        {
            var context = SearchService.CreateContext("asset", "t:prefab");
            var state = SearchViewState.CreatePickerState(null, context, (obj, canceled) =>
            {
                if (obj is not GameObject basePrefabAsset || canceled)
                {
                    return;
                }
                foreach (var targetPrefabAsset in Selection.gameObjects)
                {
                    if (basePrefabAsset == targetPrefabAsset || GetAncestors(basePrefabAsset).Contains(targetPrefabAsset))
                    {
                        continue;
                    }
                    Rebase(basePrefabAsset, targetPrefabAsset);
                }
            }, null, null, typeof(GameObject));

            SearchService.ShowPicker(state);
        }

        private static void Rebase(GameObject basePrefabAsset, GameObject targetPrefabAsset)
        {
            var targetPrefabPath = AssetDatabase.GetAssetPath(targetPrefabAsset);
            var targetPrefabInstance = PrefabUtility.InstantiatePrefab(targetPrefabAsset) as GameObject;

            PrefabUtility.UnpackPrefabInstance(targetPrefabInstance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

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
                    componentsNotMatchedBecomesOverride = true,
                    gameObjectsNotMatchedBecomesOverride = true,
                    recordPropertyOverridesOfMatches = true,
                    changeRootNameToAssetName = true,
                }, InteractionMode.AutomatedAction);
            }

            var rebasedPrefabPath = AssetDatabase.GenerateUniqueAssetPath(targetPrefabPath.Replace(".prefab", " Rebased.prefab"));
            var rebasedPrefabAsset = PrefabUtility.SaveAsPrefabAsset(targetPrefabInstance, rebasedPrefabPath);

            Object.DestroyImmediate(targetPrefabInstance);

            foreach (var childPrefabAsset in GetChildren(targetPrefabAsset))
            {
                Rebase(rebasedPrefabAsset, childPrefabAsset);
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

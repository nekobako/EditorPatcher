using System.Linq;
using UnityEngine;
using UnityEditor;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class FixOverrideMenuItems
    {
        private const int k_MenuPriority = 22;
        private const string k_FixPrefabPath = "GameObject/Editor Patcher/Fix Override";
        private const string k_FixProjectPath = "Tools/Editor Patcher/Fix Override/Fix All in Project";
        private const string k_AutoFixPath = "Tools/Editor Patcher/Fix Override/Enable Auto Fix";
        private const string k_AutoFixKey = "net.nekobako.editor-patcher.EnableAutoFixOverride";

#if UNITY_2021_3_OR_NEWER
        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(k_AutoFixKey);
            set => EditorPrefs.SetBool(k_AutoFixKey, value);
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += () =>
            {
                ToggleAutoFix(IsEnabled);
            };
        }

        [MenuItem(k_AutoFixPath, true)]
        private static bool ValidateEnabled()
        {
            Menu.SetChecked(k_AutoFixPath, IsEnabled);
            return true;
        }

        [MenuItem(k_AutoFixPath, false)]
        private static void ToggleAutoFix()
        {
            IsEnabled = !IsEnabled;
            ToggleAutoFix(IsEnabled);
        }

        private static void ToggleAutoFix(bool isEnabled)
        {
            if (isEnabled) {
                ObjectChangeEvents.changesPublished += OnObjectChange;
            }
            else {
                ObjectChangeEvents.changesPublished -= OnObjectChange;
            }
        }

        private static void OnObjectChange(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var data);
                    var obj = EditorUtility.InstanceIDToObject(data.instanceId);
                    if (obj == null) continue;
                    if (!PrefabUtility.IsPartOfAnyPrefab(obj)) continue;
                    FixOverride.RevertSameOverride(obj);
                }
            }
        }
#endif

        [MenuItem(k_FixPrefabPath, true, k_MenuPriority)]
        private static bool ValidateRunForPrefab()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(k_FixPrefabPath, false, k_MenuPriority)]
        private static void RunForPrefab()
        {
            FixPrefab(Selection.activeGameObject);
        }

        private static void FixPrefab(GameObject prefab)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(prefab)) return;

            var objs = prefab.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.gameObject as Object)
                .Concat(prefab.GetComponentsInChildren<Component>(true));

            int totalReverts = 0;
            foreach (var obj in objs)
            {
                totalReverts += FixOverride.RevertSameOverride(obj);
            }
            Debug.Log($"Revert {totalReverts} overrides in '{prefab.name}'.");
        }

        [MenuItem(k_FixProjectPath)]
        private static void RunForProject()
        {
            FixPrefabsInProject();
        }

        private static void FixPrefabsInProject()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    FixPrefab(prefab);
                }
            }
        }
    }
}
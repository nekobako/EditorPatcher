using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class ExecuteUnityConstraintsInEditMode
    {
        private struct DummyUpdate
        {
        }

        private const string k_PatchId = "net.nekobako.editor-patcher.execute-unity-constraints-in-edit-mode";
        private const string k_MenuPath = "Tools/Editor Patcher/Execute Unity Constraints in Edit Mode";

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(k_MenuPath, true);
            set => EditorPrefs.SetBool(k_MenuPath, value);
        }

        [MenuItem(k_MenuPath, true)]
        private static bool ValidateEnabled()
        {
            Menu.SetChecked(k_MenuPath, IsEnabled);
            return true;
        }

        [MenuItem(k_MenuPath, false)]
        private static void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
            InternalEditorUtility.RepaintAllViews();

            UpdatePlayerLoop();
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var harmony = new Harmony(k_PatchId);

            var baseType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ConstraintEditorBase");
            foreach (var method in typeof(UnityEditor.Editor).Assembly.GetTypes()
                .Where(x => !x.IsAbstract && !x.IsInterface && baseType.IsAssignableFrom(x))
                .Select(x => AccessTools.Method(x, "OnInspectorGUI"))
                .Where(x => x != null))
            {
                harmony.Patch(method, new HarmonyMethod(typeof(ExecuteUnityConstraintsInEditMode), nameof(OnInspectorGUI)));
            }

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(k_PatchId);

            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;

            UpdatePlayerLoop();
        }

        private static void OnInspectorGUI()
        {
            if (IsEnabled)
            {
                return;
            }

            EditorGUILayout.HelpBox("\"Execute Unity Constraints in Edit Mode\" is disabled in \"Tools/Editor Patcher\". Constraints will only run during Play Mode.", MessageType.Warning);
        }

        private static void PlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                UpdatePlayerLoop();
            }
        }

        private static void UpdatePlayerLoop()
        {
            var current = PlayerLoop.GetCurrentPlayerLoop();

            if (!IsEnabled && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ReplacePlayerLoopSystem<PreLateUpdate.ConstraintManagerUpdate>(ref current, new PlayerLoopSystem { type = typeof(DummyUpdate) });
            }
            else if (FindPlayerLoopSystem<PreLateUpdate.ConstraintManagerUpdate>(PlayerLoop.GetDefaultPlayerLoop(), out var original))
            {
                ReplacePlayerLoopSystem<DummyUpdate>(ref current, original);
            }

            PlayerLoop.SetPlayerLoop(current);
        }

        private static bool FindPlayerLoopSystem<T>(PlayerLoopSystem target, out PlayerLoopSystem result)
        {
            if (target.type == typeof(T))
            {
                result = target;
                return true;
            }

            if (target.subSystemList == null)
            {
                result = default;
                return false;
            }

            foreach (var system in target.subSystemList)
            {
                if (FindPlayerLoopSystem<T>(system, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        private static bool ReplacePlayerLoopSystem<T>(ref PlayerLoopSystem target, PlayerLoopSystem replacement)
        {
            if (target.type == typeof(T))
            {
                target = replacement;
                return true;
            }

            if (target.subSystemList == null)
            {
                return false;
            }

            for (var i = 0; i < target.subSystemList.Length; i++)
            {
                if (ReplacePlayerLoopSystem<T>(ref target.subSystemList[i], replacement))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

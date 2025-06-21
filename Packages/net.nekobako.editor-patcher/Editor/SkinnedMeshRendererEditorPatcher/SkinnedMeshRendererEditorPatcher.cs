using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class SkinnedMeshRendererEditorPatcher
    {
        private const string k_PatchId = "net.nekobako.editor-patcher.skinned-mesh-renderer-editor-patcher";
        private const string k_MenuPath = "Tools/Editor Patcher/Skinned Mesh Renderer Editor";

        private static Dictionary<UnityEditor.Editor, BlendShapesDrawer> s_BlendShapesDrawers = new Dictionary<UnityEditor.Editor, BlendShapesDrawer>();

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(k_MenuPath);
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
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var harmony = new Harmony(k_PatchId);

            harmony.Patch(AccessTools.PropertyGetter(AccessTools.TypeByName("UnityEditor.SerializedProperty"), "hasVisibleChildren"),
                new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(GetHasVisibleChildren)));

            harmony.Patch(AccessTools.Method("UnityEditor.SerializedProperty:NextVisible"),
                new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(NextVisible)));

#if UNITY_2020_2_OR_NEWER
            harmony.Patch(AccessTools.Method("UnityEditor.PropertyHandler:IsArrayReorderable"),
                new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(IsArrayReorderable)));
#endif

            harmony.Patch(AccessTools.Method("UnityEditor.SkinnedMeshRendererEditor:OnBlendShapeUI"),
                new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(OnBlendShapeUI)));

            harmony.Patch(AccessTools.Method("UnityEditor.RendererEditorBase:DrawMaterials"),
                postfix: new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(DrawMaterials_Postfix)));

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(k_PatchId);
        }

        private static bool GetHasVisibleChildren(SerializedProperty __instance, ref bool __result)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (__instance.serializedObject.targetObject is SkinnedMeshRenderer)
            {
                if (__instance.propertyPath == "m_Bones" || __instance.propertyPath == "m_Bones.Array")
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        private static bool NextVisible(SerializedProperty __instance, bool enterChildren, ref bool __result)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (__instance.serializedObject.targetObject is SkinnedMeshRenderer)
            {
                if (__instance.propertyPath == "m_Bones" && enterChildren)
                {
                    __result = __instance.Next(true);
                    __result = __instance.Next(true);
                    return false;
                }
                if (__instance.propertyPath == "m_Bones.Array" && enterChildren)
                {
                    __result = __instance.Next(true);
                    return false;
                }
                if (__instance.propertyPath.StartsWith("m_Bones.Array"))
                {
                    __result = __instance.Next(false);
                    return false;
                }
            }

            return true;
        }

        private static bool IsArrayReorderable(SerializedProperty property, ref bool __result)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (property.serializedObject.targetObject is SkinnedMeshRenderer)
            {
                if (property.propertyPath == "m_Bones" || property.propertyPath.StartsWith("m_Bones.Array"))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        private static bool OnBlendShapeUI(UnityEditor.Editor __instance, SerializedProperty ___m_BlendShapeWeights)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (s_BlendShapesDrawers.Any(x => x.Key == null))
            {
                s_BlendShapesDrawers = s_BlendShapesDrawers
                    .Where(x => x.Key != null)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            if (!s_BlendShapesDrawers.TryGetValue(__instance, out var drawer))
            {
                s_BlendShapesDrawers.Add(__instance, drawer = new BlendShapesDrawer());
            }

            drawer.Draw(___m_BlendShapeWeights);

            return false;
        }

        private static void DrawMaterials_Postfix(UnityEditor.Editor __instance)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (__instance.serializedObject.targetObject is SkinnedMeshRenderer)
            {
                EditorGUILayout.PropertyField(__instance.serializedObject.FindProperty("m_Bones"));
            }
        }
    }
}

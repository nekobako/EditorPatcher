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

#if UNITY_2020_2_OR_NEWER
        private static bool IsArrayReorderable(SerializedProperty property, ref bool __result)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (property.serializedObject.targetObject is SkinnedMeshRenderer && property.propertyPath == "m_Bones")
            {
                __result = true;
                return false;
            }

            return true;
        }
#endif

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
                var bonesProperty = __instance.serializedObject.FindProperty("m_Bones");
#if UNITY_2020_2_OR_NEWER
                EditorGUILayout.PropertyField(bonesProperty);
#else
                bonesProperty.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(bonesProperty.isExpanded, bonesProperty.displayName);
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (!bonesProperty.isExpanded)
                {
                    return;
                }

                if (bonesProperty.serializedObject.targetObjects.Length > 1)
                {
                    EditorGUILayout.HelpBox("Multi-object editing not supported.", MessageType.None);
                    EditorGUILayout.Space();
                    return;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    var bonesSizeProperty = bonesProperty.FindPropertyRelative("Array.size");
                    EditorGUILayout.PropertyField(bonesSizeProperty);
                    for (var i = 0; i < bonesSizeProperty.intValue; i++)
                    {
                        EditorGUILayout.PropertyField(bonesProperty.GetArrayElementAtIndex(i));
                    }
                }
#endif
            }
        }
    }
}

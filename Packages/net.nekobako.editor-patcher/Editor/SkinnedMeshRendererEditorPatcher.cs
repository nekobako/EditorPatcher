using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class SkinnedMeshRendererEditorPatcher
    {
        private const string k_PatchId = "net.nekobako.editor-patcher.skinned-mesh-renderer-editor-patcher";
        private const string k_MenuPath = "Tools/Editor Patcher/Skinned Mesh Renderer Editor";

        private static readonly Type s_TargetType = AccessTools.TypeByName("UnityEditor.SkinnedMeshRendererEditor");
        private static Dictionary<UnityEditor.Editor, BlendShapesDrawer> s_BlendShapesDrawers = new Dictionary<UnityEditor.Editor, BlendShapesDrawer>();

        private class BlendShape
        {
            public readonly int Index = 0;
            public readonly string Name = string.Empty;
            public readonly GUIContent Content = null;
            public readonly float MinWeight = 0.0f;
            public readonly float MaxWeight = 0.0f;

            public BlendShape(Mesh mesh, int index)
            {
                Index = index;
                Name = mesh.GetBlendShapeName(index);
                Content = new GUIContent(Name);
                MinWeight = 0.0f;
                MaxWeight = 0.0f;
                for (var i = 0; i < mesh.GetBlendShapeFrameCount(index); i++)
                {
                    var weight = mesh.GetBlendShapeFrameWeight(index, i);
                    MinWeight = Mathf.Min(weight, MinWeight);
                    MaxWeight = Mathf.Max(weight, MaxWeight);
                }
            }
        }

        private class BlendShapeGroup
        {
            public readonly string Name = string.Empty;
            public readonly List<BlendShape> BlendShapes = new List<BlendShape>();

            public BlendShapeGroup(string name)
            {
                Name = name;
            }
        }

        private class BlendShapesDrawer
        {
            private const string k_DefaultGroupName = "Default";
            private const string k_GroupNamePattern = @"^(?:\W|\p{Pc}){3,}(.*?)(?:\W|\p{Pc}){3,}$";

            private static readonly GUIContent s_PropertyContent = new GUIContent("BlendShapes");
            private static readonly GUIContent s_ClampWeightsInfoContent = Traverse.Create(s_TargetType)
                .Type("Styles")
                .Field("legacyClampBlendShapeWeightsInfo")
                .GetValue<GUIContent>();
            private static readonly GUIStyle s_SearchFieldStyle = new GUIStyle("SearchTextField")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonStyle = new GUIStyle("SearchCancelButton")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonEmptyStyle = new GUIStyle("SearchCancelButtonEmpty")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_PopupStyle = new GUIStyle("MiniPopup")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_ReorderableListHeaderStyle = new GUIStyle("RL Header")
            {
                fixedHeight = 0.0f,
            };

            private readonly SearchField m_SearchField = new SearchField();
            private readonly ReorderableList m_ReorderableList = new ReorderableList(null, typeof(BlendShape), false, true, false, false)
            {
                headerHeight = 30.0f,
            };

            private SkinnedMeshRenderer m_Renderer = null;
            private Mesh m_Mesh = null;
            private List<BlendShapeGroup> m_BlendShapeGroups = new List<BlendShapeGroup>();
            private string m_SearchText = string.Empty;
            private string[] m_GroupNames = Array.Empty<string>();
            private int m_GroupMask = ~0;
            private bool m_ShowZero = true;

            public void Draw(SerializedProperty property)
            {
                var renderer = property.serializedObject.targetObject as SkinnedMeshRenderer;
                if (renderer == null)
                {
                    return;
                }

                var mesh = renderer.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0)
                {
                    return;
                }

                EditorGUILayout.PropertyField(property, s_PropertyContent, false);
                if (!property.isExpanded)
                {
                    return;
                }

                if (property.serializedObject.targetObjects.Length > 1)
                {
                    GUILayout.Label("Multi-object editing not supported.", EditorStyles.helpBox);
                    return;
                }

                if (PlayerSettings.legacyClampBlendShapeWeights)
                {
                    EditorGUILayout.HelpBox(s_ClampWeightsInfoContent.text, MessageType.Info);
                }

                if (renderer != m_Renderer || mesh != m_Mesh)
                {
                    m_Renderer = renderer;
                    m_Mesh = mesh;

                    UpdateBlendShapes();
                    UpdateReorderableList();
                }

                m_ReorderableList.drawHeaderCallback = rect =>
                {
                    rect.min -= new Vector2(6.0f, 1.0f);
                    rect.max += new Vector2(6.0f, 1.0f);

                    if (Event.current.type == EventType.Repaint)
                    {
                        s_ReorderableListHeaderStyle.Draw(rect, false, false, false, false);
                    }

                    rect.min += new Vector2(6.0f, 4.0f);
                    rect.max -= new Vector2(6.0f, 4.0f);

                    EditorGUI.BeginChangeCheck();

                    rect.width -= 126.0f;
                    m_SearchText = m_SearchField.OnGUI(rect, m_SearchText, s_SearchFieldStyle, s_SearchFieldCancelButtonStyle, s_SearchFieldCancelButtonEmptyStyle);

                    rect.xMin = rect.xMax + 2.0f;
                    rect.xMax = rect.xMin + 100.0f;
                    m_GroupMask = EditorGUI.MaskField(rect, m_GroupMask, m_GroupNames, s_PopupStyle);

                    rect.xMin = rect.xMax + 2.0f;
                    rect.xMax = rect.xMin + 22.0f;
                    m_ShowZero = GUI.Toggle(rect, m_ShowZero, "0", GUI.skin.button);

                    if (EditorGUI.EndChangeCheck())
                    {
                        UpdateReorderableList();
                    }
                };

                m_ReorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var shape = (BlendShape)m_ReorderableList.list[index];
                    if (shape.Index < property.arraySize)
                    {
                        var prop = property.GetArrayElementAtIndex(shape.Index);
                        Traverse.Create<EditorGUI>()
                            .Method(nameof(EditorGUI.Slider), rect, prop, shape.MinWeight, shape.MaxWeight, float.MinValue, float.MaxValue, shape.Content)
                            .GetValue();
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();

                        var value = Traverse.Create<EditorGUI>()
                            .Method(nameof(EditorGUI.Slider), rect, shape.Content, 0.0f, shape.MinWeight, shape.MaxWeight, float.MinValue, float.MaxValue)
                            .GetValue<float>();

                        if (EditorGUI.EndChangeCheck())
                        {
                            property.arraySize = mesh.blendShapeCount;
                            property.GetArrayElementAtIndex(shape.Index).floatValue = value;
                        }
                    }
                };

                m_ReorderableList.DoLayoutList();
            }

            private void UpdateBlendShapes()
            {
                m_BlendShapeGroups.Clear();
                m_BlendShapeGroups.Add(new BlendShapeGroup(k_DefaultGroupName));

                for (var i = 0; i < m_Mesh.blendShapeCount; i++)
                {
                    var shape = new BlendShape(m_Mesh, i);
                    var match = Regex.Match(shape.Name, k_GroupNamePattern);
                    if (match.Success)
                    {
                        m_BlendShapeGroups.Add(new BlendShapeGroup(match.Groups[1].Value));
                    }

                    m_BlendShapeGroups.Last().BlendShapes.Add(shape);
                }

                m_GroupNames = m_BlendShapeGroups
                    .Select(x => x.Name)
                    .ToArray();
                m_GroupMask = ~0;
            }

            private void UpdateReorderableList()
            {
                m_ReorderableList.list = m_BlendShapeGroups
                    .Where((x, i) => (m_GroupMask & 1 << i) != 0)
                    .SelectMany(x => x.BlendShapes)
                    .Where(x => m_ShowZero || x.Index < m_Mesh.blendShapeCount && m_Renderer.GetBlendShapeWeight(x.Index) != 0.0f)
                    .Where(x => m_SearchText.Split().All(y => x.Name.IndexOf(y, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
            }
        }

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

            var onBlendShapeUI = AccessTools.Method(s_TargetType, "OnBlendShapeUI");
            harmony.Patch(onBlendShapeUI, new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(OnBlendShapeUI)));

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll();
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
    }
}

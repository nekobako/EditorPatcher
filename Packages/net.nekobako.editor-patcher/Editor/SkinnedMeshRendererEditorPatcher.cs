using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Pool;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class SkinnedMeshRendererEditorPatcher
    {
        private const string k_PatchId = "net.nekobako.editor-patcher.skinned-mesh-renderer-editor-patcher";
        private const string k_MenuPath = "Tools/Editor Patcher/Skinned Mesh Renderer Editor";

        private static readonly Type s_TargetType = AccessTools.TypeByName("UnityEditor.SkinnedMeshRendererEditor");
        private static readonly Dictionary<UnityEditor.Editor, BlendShapesDrawer> s_BlendShapesDrawers = new();

        private readonly struct BlendShape
        {
            public readonly int Index;
            public readonly string Name;
            public readonly GUIContent Content;
            public readonly float MinValue;
            public readonly float MaxValue;

            public BlendShape(Mesh mesh, int index)
            {
                Index = index;
                Name = mesh.GetBlendShapeName(index);
                Content = new(Name);
                MinValue = 0.0f;
                MaxValue = 0.0f;
                for (var i = 0; i < mesh.GetBlendShapeFrameCount(index); i++)
                {
                    var weight = mesh.GetBlendShapeFrameWeight(index, i);
                    MinValue = Mathf.Min(weight, MinValue);
                    MaxValue = Mathf.Max(weight, MaxValue);
                }
            }
        }

        private class BlendShapesDrawer
        {
            private const string k_DefaultGroupName = "Default";
            private const string k_GroupNamePattern = @"^(?:\W|\p{Pc}){3,}(.*?)(?:\W|\p{Pc}){3,}$";

            private static readonly GUIContent s_PropertyContent = new("BlendShapes");
            private static readonly GUIContent s_ClampWeightsInfoContent = Traverse.Create(s_TargetType)
                .Type("Styles")
                .Field("legacyClampBlendShapeWeightsInfo")
                .GetValue<GUIContent>();
            private static readonly GUIStyle s_SearchFieldStyle = new("SearchTextField")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonStyle = new("SearchCancelButton")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonEmptyStyle = new("SearchCancelButtonEmpty")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_PopupStyle = new("MiniPopup")
            {
                fixedHeight = 0.0f,
            };
            private static readonly GUIStyle s_ReorderableListHeaderStyle = new("RL Header")
            {
                fixedHeight = 0.0f,
            };

            private readonly UnityEditor.Editor m_Editor = null;
            private readonly SearchField m_SearchField = new();
            private readonly ReorderableList m_ReorderableList = new(null, typeof(BlendShape), false, true, false, false)
            {
                headerHeight = 30.0f,
            };

            private Mesh m_Mesh = null;
            private Dictionary<string, List<BlendShape>> m_BlendShapes = new();
            private string m_SearchText = string.Empty;
            private string[] m_GroupNames = Array.Empty<string>();
            private int m_GroupMask = ~0;

            public BlendShapesDrawer(UnityEditor.Editor editor)
            {
                m_Editor = editor;
            }

            public void Draw(SerializedProperty property)
            {
                var mesh = (m_Editor.target as SkinnedMeshRenderer).sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0)
                {
                    return;
                }

                EditorGUILayout.PropertyField(property, s_PropertyContent, false);
                if (!property.isExpanded)
                {
                    return;
                }

                if (PlayerSettings.legacyClampBlendShapeWeights)
                {
                    EditorGUILayout.HelpBox(s_ClampWeightsInfoContent.text, MessageType.Info);
                }

                if (mesh != m_Mesh)
                {
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

                    rect.xMin -= 0.0f;
                    rect.xMax -= 102.0f;
                    m_SearchText = m_SearchField.OnGUI(rect, m_SearchText, s_SearchFieldStyle, s_SearchFieldCancelButtonStyle, s_SearchFieldCancelButtonEmptyStyle);

                    rect.xMin += rect.width + 2.0f;
                    rect.xMax += 102.0f;
                    m_GroupMask = EditorGUI.MaskField(rect, m_GroupMask, m_GroupNames, s_PopupStyle);

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
                            .Method(nameof(EditorGUI.Slider), new object[] { rect, prop, shape.MinValue, shape.MaxValue, float.MinValue, float.MaxValue, shape.Content })
                            .GetValue();
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();

                        var value = Traverse.Create<EditorGUI>()
                            .Method(nameof(EditorGUI.Slider), new object[] { rect, shape.Content, 0.0f, shape.MinValue, shape.MaxValue, float.MinValue, float.MaxValue })
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
                m_BlendShapes.Clear();

                var groups = new List<string>() { k_DefaultGroupName };
                for (var i = 0; i < m_Mesh.blendShapeCount; i++)
                {
                    var shape = new BlendShape(m_Mesh, i);
                    var match = Regex.Match(shape.Name, k_GroupNamePattern);
                    if (match.Success)
                    {
                        groups.Add(match.Groups[1].Value);
                    }

                    if (!m_BlendShapes.TryGetValue(groups[^1], out var shapes))
                    {
                        m_BlendShapes.Add(groups[^1], shapes = new());
                    }

                    shapes.Add(shape);
                }

                m_GroupNames = groups.ToArray();
                m_GroupMask = ~0;
            }

            private void UpdateReorderableList()
            {
                m_ReorderableList.list = m_BlendShapes
                    .Where(x => (m_GroupMask & 1 << Array.IndexOf(m_GroupNames, x.Key)) != 0)
                    .SelectMany(x => x.Value)
                    .Where(x => m_SearchText.Split().All(y => x.Name.Contains(y, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(x => x.Index)
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
            harmony.Patch(onBlendShapeUI, new(typeof(SkinnedMeshRendererEditorPatcher), nameof(OnBlendShapeUI)));

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll();
        }

        private static bool OnBlendShapeUI(UnityEditor.Editor __instance, SerializedProperty ___m_BlendShapeWeights)
        {
            if (!IsEnabled)
            {
                return true;
            }

            using (ListPool<UnityEditor.Editor>.Get(out var outdated))
            {
                foreach (var editor in s_BlendShapesDrawers.Keys)
                {
                    if (editor == null)
                    {
                        outdated.Add(editor);
                    }
                }
                foreach (var editor in outdated)
                {
                    s_BlendShapesDrawers.Remove(editor);
                }
            }

            if (!s_BlendShapesDrawers.TryGetValue(__instance, out var drawer))
            {
                s_BlendShapesDrawers.Add(__instance, drawer = new(__instance));
            }

            drawer.Draw(___m_BlendShapeWeights);

            return false;
        }
    }
}

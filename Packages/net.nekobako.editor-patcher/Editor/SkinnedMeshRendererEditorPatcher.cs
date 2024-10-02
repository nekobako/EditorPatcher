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

        private static Dictionary<UnityEditor.Editor, BlendShapesDrawer> s_BlendShapesDrawers = new Dictionary<UnityEditor.Editor, BlendShapesDrawer>();

        private class BlendShape : ITreeData
        {
            public readonly int Index = 0;
            public readonly string Name = string.Empty;
            public readonly float MinWeight = 0.0f;
            public readonly float MaxWeight = 0.0f;

            int ITreeData.Id => Index;
            int ITreeData.Depth => 0;
            string ITreeData.DisplayName => Name;

            public BlendShape(Mesh mesh, int index)
            {
                Index = index;
                Name = mesh.GetBlendShapeName(index);
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

        private class BlendShapeGroup : ISelectionData
        {
            public readonly string Name = string.Empty;
            public readonly List<BlendShape> BlendShapes = new List<BlendShape>();

            string ISelectionData.DisplayName => Name;
            bool ISelectionData.IsSelected { get; set; } = true;

            public BlendShapeGroup(string name)
            {
                Name = name;
            }
        }

        private class BlendShapesDrawer : TreeView
        {
            private const string k_DefaultGroupName = "Default";
            private const string k_GroupNamePattern = @"^(?:\W|\p{Pc}){3,}(.*?)(?:\W|\p{Pc}){3,}$";
            private const int k_RowHeight = 24;
            private const int k_LineHeight = 22;

            private static readonly Traverse s_GUIViewCurrent = Traverse.CreateWithType("UnityEditor.GUIView")
                .Property("current");
            private static readonly Traverse s_EditorGUILineHeight = Traverse.Create<EditorGUI>()
                .Property("lineHeight");
            private static readonly Traverse s_EditorGUISlider = Traverse.Create<EditorGUI>()
                .Method(nameof(EditorGUI.Slider), new[]
                {
                    typeof(Rect),
                    typeof(GUIContent),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(GUIStyle),
                    typeof(GUIStyle),
                    typeof(GUIStyle),
                    typeof(Texture2D),
                    typeof(GUIStyle),
                });
            private static readonly GUIStyle s_HeaderStyle = new GUIStyle("RL Header")
            {
                fixedHeight = k_LineHeight + 8,
                padding = new RectOffset(6, 6, 4, 4),
            };
            private static readonly GUIStyle s_BackgroundStyle = new GUIStyle("RL Background")
            {
                fixedHeight = 0,
                padding = new RectOffset(6, 6, 4, 4),
            };
            private static readonly GUIStyle s_SearchFieldStyle = new GUIStyle("SearchTextField")
            {
                fixedHeight = 0,
                margin = new RectOffset(2, 2, 2, 2),
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonStyle = new GUIStyle("SearchCancelButton")
            {
                fixedHeight = 0,
                margin = new RectOffset(2, 2, 2, 2),
            };
            private static readonly GUIStyle s_SearchFieldCancelButtonEmptyStyle = new GUIStyle("SearchCancelButtonEmpty")
            {
                fixedHeight = 0,
                margin = new RectOffset(2, 2, 2, 2),
            };
            private static readonly GUIStyle s_PopupStyle = new GUIStyle("MiniPopup")
            {
                fixedHeight = 0,
                margin = new RectOffset(2, 2, 2, 2),
            };
            private static readonly GUIStyle s_ToggleStyle = new GUIStyle("LargeButton")
            {
                fixedHeight = 0,
                margin = new RectOffset(2, 2, 2, 2),
            };
            private static readonly GUIStyle s_SliderStyle = new GUIStyle()
            {
                overflow = new RectOffset(0, 0, (2 - k_RowHeight) / 2, (2 - k_RowHeight) / 2),
            };
            private static readonly GUIStyle s_SliderThumbStyle = new GUIStyle("HorizontalSliderThumb")
            {
                margin = new RectOffset(0, 0, (k_RowHeight - 10) / 2, (k_RowHeight - 10) / 2),
            };
            private static readonly GUIStyle s_SliderThumbExtentStyle = new GUIStyle("HorizontalSliderThumbExtent")
            {
                margin = new RectOffset(0, 0, (k_RowHeight - 10) / 2, (k_RowHeight - 10) / 2),
            };
            private static readonly GUIStyle s_SliderNumberFieldStyle = new GUIStyle("TextField")
            {
                fixedHeight = k_LineHeight,
                alignment = TextAnchor.MiddleLeft,
            };

            private readonly List<BlendShapeGroup> m_Groups = new List<BlendShapeGroup>();
            private readonly SearchField m_SearchField = new SearchField();
            private readonly MultiSelectionPopup m_FilterPopup = new MultiSelectionPopup();

            private SerializedProperty m_Property = null;
            private Mesh m_Mesh = null;
            private string m_SearchText = string.Empty;
            private bool m_ShowZero = true;

            public BlendShapesDrawer() : base(new TreeViewState())
            {
                rowHeight = k_RowHeight;
                useScrollView = false;
#if UNITY_2022_1_OR_NEWER
                enableItemHovering = true;
#endif
            }

            public void Draw(SerializedProperty property)
            {
                var rect = EditorGUILayout.GetControlRect();
                var content = EditorGUI.BeginProperty(rect, TempContent.Text("BlendShapes"), property);

                property.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(rect, property.isExpanded, content);
                EditorGUI.EndFoldoutHeaderGroup();

                EditorGUI.EndProperty();

                if (!property.isExpanded)
                {
                    return;
                }

                if (property.serializedObject.targetObjects.Length > 1)
                {
                    EditorGUILayout.HelpBox("Multi-object editing not supported.", MessageType.None);
                    EditorGUILayout.Space();
                    return;
                }

                // Workaround for errors caused by TreeView.enableItemHovering = true
                if (s_GUIViewCurrent.GetValue() == null)
                {
                    return;
                }

                if (PlayerSettings.legacyClampBlendShapeWeights)
                {
                    EditorGUILayout.HelpBox("Note that BlendShape weight range is clamped. This can be disabled in Player Settings.", MessageType.Info);
                }

                m_Property = property;

                var mesh = (property.serializedObject.targetObject as SkinnedMeshRenderer).sharedMesh;
                if (mesh != m_Mesh)
                {
                    m_Mesh = mesh;

                    UpdateGroups();
                    Reload();
                }

                using (new EditorGUILayout.HorizontalScope(s_HeaderStyle))
                {
                    EditorGUI.BeginChangeCheck();

                    rect = EditorGUILayout.GetControlRect(GUILayout.MinWidth(k_LineHeight), GUILayout.ExpandHeight(true));
                    m_SearchField.searchFieldControlID = GUIUtility.GetControlID(FocusType.Keyboard, rect);
                    m_SearchText = m_SearchField.OnGUI(rect, m_SearchText, s_SearchFieldStyle, s_SearchFieldCancelButtonStyle, s_SearchFieldCancelButtonEmptyStyle);

                    rect = EditorGUILayout.GetControlRect(GUILayout.Width(100), GUILayout.ExpandHeight(true));
                    m_FilterPopup.OnGUI(rect, m_Groups, s_PopupStyle);

                    rect = EditorGUILayout.GetControlRect(GUILayout.Width(k_LineHeight), GUILayout.ExpandHeight(true));
                    m_ShowZero = GUI.Toggle(rect, GUIUtility.GetControlID(FocusType.Keyboard, rect), m_ShowZero, TempContent.Text("0"), s_ToggleStyle);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Reload();
                    }
                }

                using (new EditorGUILayout.HorizontalScope(s_BackgroundStyle))
                {
                    if (rootItem != null && rootItem.children.Count > 0)
                    {
                        // EditorGUI.Slider to be aligned vertically
                        s_EditorGUILineHeight.SetValue(k_RowHeight);

                        rect = EditorGUILayout.GetControlRect(false, totalHeight - 3);
                        rect.min -= new Vector2(5, 2);
                        rect.max += new Vector2(5, 1);
                        OnGUI(rect);

                        // Restore to original line height
                        s_EditorGUILineHeight.SetValue(EditorGUIUtility.singleLineHeight);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("List is Empty", GUILayout.Height(k_RowHeight - 3));
                    }
                }

                EditorGUILayout.Space();
            }

            private void UpdateGroups()
            {
                m_Groups.Clear();
                m_Groups.Add(new BlendShapeGroup(k_DefaultGroupName));

                for (var i = 0; m_Mesh != null && i < m_Mesh.blendShapeCount; i++)
                {
                    var shape = new BlendShape(m_Mesh, i);
                    var match = Regex.Match(shape.Name, k_GroupNamePattern);
                    if (match.Success)
                    {
                        m_Groups.Add(new BlendShapeGroup(match.Groups[1].Value));
                    }

                    m_Groups.Last().BlendShapes.Add(shape);
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                var renderer = m_Property.serializedObject.targetObject as SkinnedMeshRenderer;

                return new TreeViewItem(-1, -1)
                {
                    children = m_Groups
                        .Where(x => (x as ISelectionData).IsSelected)
                        .SelectMany(x => x.BlendShapes)
                        .Where(x => m_ShowZero || m_Mesh != null && x.Index < m_Mesh.blendShapeCount && renderer.GetBlendShapeWeight(x.Index) != 0.0f)
                        .Where(x => m_SearchText.Split().All(y => x.Name.IndexOf(y, StringComparison.OrdinalIgnoreCase) >= 0))
                        .Select(x => new TreeViewItem<BlendShape>(x))
                        .ToList<TreeViewItem>(),
                };
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                var shape = (args.item as TreeViewItem<BlendShape>).Data;
                var content = TempContent.Text(shape.Name);

                var rect = args.rowRect;
                rect.min += new Vector2(5, 0);
                rect.max -= new Vector2(5, 0);

                if (shape.Index < m_Property.arraySize)
                {
                    var prop = m_Property.GetArrayElementAtIndex(shape.Index);
                    content = EditorGUI.BeginProperty(rect, content, prop);

                    EditorGUI.BeginChangeCheck();

                    var value = s_EditorGUISlider.GetValue<float>(
                        rect, content, prop.floatValue, shape.MinWeight, shape.MaxWeight, float.MinValue, float.MaxValue,
                        s_SliderNumberFieldStyle, s_SliderStyle, s_SliderThumbStyle, Texture2D.linearGrayTexture, s_SliderThumbExtentStyle);

                    if (EditorGUI.EndChangeCheck())
                    {
                        prop.floatValue = value;
                    }

                    EditorGUI.EndProperty();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();

                    var value = s_EditorGUISlider.GetValue<float>(
                        rect, content, 0.0f, shape.MinWeight, shape.MaxWeight, float.MinValue, float.MaxValue,
                        s_SliderNumberFieldStyle, s_SliderStyle, s_SliderThumbStyle, Texture2D.linearGrayTexture, s_SliderThumbExtentStyle);

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_Property.arraySize = m_Mesh != null ? m_Mesh.blendShapeCount : 0;
                        m_Property.GetArrayElementAtIndex(shape.Index).floatValue = value;
                    }
                }

                if (Event.current.type == EventType.MouseDown && args.rowRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                }
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

            harmony.Patch(AccessTools.Method("UnityEditor.SkinnedMeshRendererEditor:OnBlendShapeUI"),
                new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher), nameof(OnBlendShapeUI)));

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(k_PatchId);
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

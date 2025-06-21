using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal class BlendShapesDrawer : TreeView
    {
        private const string k_DefaultGroupName = "Default";

        private static readonly string s_GroupNameSymbolPattern = string.Join("|", new[]
        {
            @"\W",     // Non-Word Characters
            @"\p{Pc}", // Connector Punctuations
            @"ー",     // Katakana-Hiragana Prolonged Sound Mark
            @"ｰ",      // Halfwidth Katakana-Hiragana Prolonged Sound Mark
        });
        private static readonly string s_GroupNamePattern = string.Join("|", new[]
        {
            $"^(?:(?:{s_GroupNameSymbolPattern}){{4,}})(.*?)(?:(?:{s_GroupNameSymbolPattern}){{4,}})?$",
            $"^(?:(?:{s_GroupNameSymbolPattern}){{4,}})?(.*?)(?:(?:{s_GroupNameSymbolPattern}){{4,}})$",
        });

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

        private readonly List<BlendShapeGroup> m_Groups = new List<BlendShapeGroup>();
        private readonly SearchField m_SearchField = new SearchField();
        private readonly MultiSelectionPopup m_FilterPopup = new MultiSelectionPopup();

        private SerializedProperty m_Property = null;
        private SkinnedMeshRenderer m_Renderer = null;
        private Mesh m_Mesh = null;
        private Hash128 m_MeshAssetHash = default;
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

            // Prevent checkboxes from appearing in FoldoutHeaderGroup
            EditorGUI.showMixedValue = false;

            property.isExpanded = EditorGUI.BeginFoldoutHeaderGroup(rect, property.isExpanded, content);
            EditorGUI.EndFoldoutHeaderGroup();

            // Restore to actual mixed value
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

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
            m_Renderer = property.serializedObject.targetObject as SkinnedMeshRenderer;

            var mesh = m_Renderer != null ? m_Renderer.sharedMesh : null;
            var meshAssetHash = mesh != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(mesh)) : default;
            if (mesh != m_Mesh || meshAssetHash != m_MeshAssetHash)
            {
                m_Mesh = mesh;
                m_MeshAssetHash = meshAssetHash;

                UpdateGroups();
                LoadStates();
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
                    SaveStates();
                    Reload();
                }
            }

            using (new EditorGUILayout.HorizontalScope(s_BackgroundStyle))
            {
                if (rootItem != null && rootItem.children.Count > 0)
                {
                    // Align texts in EditorGUI.Slider vertically
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
                var match = Regex.Match(shape.Name, s_GroupNamePattern);
                if (match.Success)
                {
                    m_Groups.Add(new BlendShapeGroup(match.Groups.Cast<Group>().Skip(1).First(x => x.Success).Value));
                }

                m_Groups.Last().BlendShapes.Add(shape);
            }

            m_SearchText = string.Empty;
            m_ShowZero = true;
        }

        private void LoadStates()
        {
            var prefix = $"{typeof(BlendShapesDrawer).FullName}_{(m_Renderer != null ? m_Renderer.GetInstanceID() : 0)}_{(m_Mesh != null ? m_Mesh.GetInstanceID() : 0)}_{m_MeshAssetHash}";

            var unselectedGroupIndices = SessionState.GetIntArray($"{prefix}_UnselectedGroupIndices", Array.Empty<int>());
            for (var i = 0; i < m_Groups.Count; i++)
            {
                (m_Groups[i] as ISelectionData).IsSelected = !unselectedGroupIndices.Contains(i);
            }

            m_SearchText = SessionState.GetString($"{prefix}_SearchText", m_SearchText);
            m_ShowZero = SessionState.GetBool($"{prefix}_ShowZero", m_ShowZero);
        }

        private void SaveStates()
        {
            var prefix = $"{typeof(BlendShapesDrawer).FullName}_{(m_Renderer != null ? m_Renderer.GetInstanceID() : 0)}_{(m_Mesh != null ? m_Mesh.GetInstanceID() : 0)}_{m_MeshAssetHash}";

            SessionState.SetIntArray($"{prefix}_UnselectedGroupIndices", m_Groups
                .Select((x, i) => (Index: i, (x as ISelectionData).IsSelected))
                .Where(x => !x.IsSelected)
                .Select(x => x.Index)
                .ToArray());

            SessionState.SetString($"{prefix}_SearchText", m_SearchText);
            SessionState.SetBool($"{prefix}_ShowZero", m_ShowZero);
        }

        protected override TreeViewItem BuildRoot()
        {
            return new TreeViewItem(-1, -1)
            {
                children = m_Groups
                    .Where(x => (x as ISelectionData).IsSelected)
                    .SelectMany(x => x.BlendShapes)
                    .Where(x => m_ShowZero || m_Mesh != null && x.Index < m_Mesh.blendShapeCount && m_Renderer != null && m_Renderer.GetBlendShapeWeight(x.Index) != 0.0f)
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
}

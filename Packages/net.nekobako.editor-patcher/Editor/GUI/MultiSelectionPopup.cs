using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal interface ISelectionData
    {
        string DisplayName { get; }
        bool IsSelected { get; set; }
    }

    internal class MultiSelectionPopup
    {
        private const string k_SelectionChangedEventName = "MultiSelectionPopupSelectionChanged";
        private const string k_NothingText = "Nothing";
        private const string k_MixedText = "Mixed...";
        private const string k_EverythingText = "Everything";

        private static readonly Traverse s_GUIViewCurrent = Traverse.CreateWithType("UnityEditor.GUIView")
            .Property("current");
        private static readonly Traverse s_EventMainActionKeyForControl = Traverse.CreateWithType("UnityEditor.EditorExtensionMethods")
            .Method("MainActionKeyForControl", new[]
            {
                typeof(Event),
                typeof(int),
            });
        private static readonly Traverse s_PopupWindowWithoutFocusShow = Traverse.CreateWithType("UnityEditor.PopupWindowWithoutFocus")
            .Method("Show", new[]
            {
                typeof(Rect),
                typeof(PopupWindowContent),
            });

        private static int s_SelectionChangedControl = 0;

        private class WindowContent : PopupWindowContent
        {
            private const int k_LineHeight = 22;

            private static readonly GUIStyle s_SelectionStyle = new GUIStyle("MenuItem")
            {
                fixedHeight = k_LineHeight,
                padding = new RectOffset(20, 0, 0, 0),
                overflow = new RectOffset(0, 0, (16 - k_LineHeight) / 2, (16 - k_LineHeight) / 2),
            };
            private static readonly GUIStyle s_SelectionBackgroundOddStyle = new GUIStyle("ObjectPickerResultsOdd");
            private static readonly GUIStyle s_SelectionBackgroundEvenStyle = new GUIStyle("ObjectPickerResultsEven");

            private readonly IEnumerable<ISelectionData> m_Source = null;
            private readonly object m_View = null;
            private readonly int m_Control = 0;

            private Vector2 m_ScrollPosition = Vector2.zero;

            public WindowContent(IEnumerable<ISelectionData> source, object view, int control)
            {
                m_Source = source;
                m_View = view;
                m_Control = control;
            }

            public override Vector2 GetWindowSize()
            {
                return m_Source
                    .Select(x => x.DisplayName)
                    .Prepend(k_EverythingText)
                    .Prepend(k_NothingText)
                    .Select(x => s_SelectionStyle.CalcSize(TempContent.Text(x)))
                    .Aggregate(new Vector2(100, 0), (result, next) => new Vector2(Math.Max(result.x, next.x + 12), result.y + next.y));
            }

            public override void OnGUI(Rect rect)
            {
                if (Event.current.type == EventType.MouseMove)
                {
                    Event.current.Use();
                }

                var even = true;
                var nothing = true;
                var everything = true;
                foreach (var data in m_Source)
                {
                    if (data.IsSelected)
                    {
                        nothing = false;
                    }
                    else
                    {
                        everything = false;
                    }
                }

                m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

                EditorGUI.BeginChangeCheck();

                DrawResetSelection(nothing, k_NothingText, false, even ^= true);
                DrawResetSelection(everything, k_EverythingText, true, even ^= true);

                foreach (var data in m_Source)
                {
                    data.IsSelected = DrawSelection(data.IsSelected, data.DisplayName, even ^= true);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    s_SelectionChangedControl = m_Control;
                    Traverse.Create(m_View)
                        .Method("SendEvent", EditorGUIUtility.CommandEvent(k_SelectionChangedEventName))
                        .GetValue();
                }

                EditorGUILayout.EndScrollView();
            }

            private bool DrawSelection(bool selected, string text, bool even)
            {
                var rect = EditorGUILayout.GetControlRect(false, k_LineHeight, s_SelectionStyle);

                if (Event.current.type == EventType.Repaint)
                {
                    var style = even ? s_SelectionBackgroundEvenStyle : s_SelectionBackgroundOddStyle;
                    style.Draw(rect, false, false, false, false);
                }

                return GUI.Toggle(rect, selected, text, s_SelectionStyle);
            }

            private void DrawResetSelection(bool selected, string text, bool value, bool even)
            {
                EditorGUI.BeginChangeCheck();

                selected = DrawSelection(selected, text, even);

                if (EditorGUI.EndChangeCheck() && selected)
                {
                    foreach (var data in m_Source)
                    {
                        data.IsSelected = value;
                    }
                }
            }
        }

        public void OnGUI(Rect rect, IEnumerable<ISelectionData> source, GUIStyle style)
        {
            var control = GUIUtility.GetControlID(FocusType.Keyboard, rect);

            switch (Event.current.type)
            {
                case EventType.Repaint:
                    var content = TempContent.Text(GetSelectionText(source));
                    style.Draw(rect, content, control, false, rect.Contains(Event.current.mousePosition));
                    break;

                case EventType.MouseDown when rect.Contains(Event.current.mousePosition):
                case EventType.KeyDown when s_EventMainActionKeyForControl.GetValue<bool>(Event.current, control):
                    s_PopupWindowWithoutFocusShow.GetValue(rect, new WindowContent(source, s_GUIViewCurrent.GetValue(), control));
                    break;

                case EventType.ExecuteCommand when Event.current.commandName == k_SelectionChangedEventName && s_SelectionChangedControl == control:
                    GUI.changed = true;
                    break;
            }
        }

        private string GetSelectionText(IEnumerable<ISelectionData> source)
        {
            var total = 0;
            var selected = 0;
            var text = string.Empty;
            foreach (var data in source)
            {
                total++;
                if (data.IsSelected)
                {
                    selected++;
                    text = data.DisplayName;
                }
            }
            return
                selected == 0 ? k_NothingText :
                selected == 1 ? text :
                selected < total ? k_MixedText :
                k_EverythingText;
        }
    }
}

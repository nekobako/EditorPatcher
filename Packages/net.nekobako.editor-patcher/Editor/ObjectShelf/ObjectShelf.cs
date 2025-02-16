using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace net.nekobako.EditorPatcher.Editor
{
    internal class ObjectShelf : EditorWindow
    {
        private const string k_AutoSpawnMenuPath = "Tools/Editor Patcher/Object Shelf/Auto Spawn";
        private const string k_ManualSpawnMenuPath = "Tools/Editor Patcher/Object Shelf/Manual Spawn";
        private const string k_WindowTitle = "Object Shelf";
        private const string k_SpawnButtonIcon = "Toolbar Plus";
        private const string k_FindSelectedMenuTitle = "Find Selected";
        private const string k_RemoveSelectedMenuTitle = "Remove Selected";
        private const string k_MissingObjectName = "<Missing>";
        private const string k_MissingObjectIcon = "DefaultAsset Icon";
        private const string k_MultipleDragTitle = "<Multiple>";
        private const string k_DropAreaName = "drop-area";
        private const string k_ListAreaName = "list-area";
        private const string k_ListViewName = "list-view";
        private const string k_EmptyClassName = "empty";
        private const string k_DraggingInClassName = "dragging-in";
        private const string k_DraggingOutClassName = "dragging-out";
        private const float k_DragThreshold = 10.0f;
        private const int k_ObserveDraggingIntervalMs = 100;

        private static readonly Lazy<GUIStyle> s_LockToggleStyle = new Lazy<GUIStyle>(() => new GUIStyle("IN LockButton"));
        private static readonly Lazy<GUIStyle> s_SpawnButtonStyle = new Lazy<GUIStyle>(() => new GUIStyle("IconButton"));

        private static bool IsAutoSpawn
        {
            get => EditorPrefs.GetBool(k_AutoSpawnMenuPath);
            set => EditorPrefs.SetBool(k_AutoSpawnMenuPath, value);
        }

        [MenuItem(k_AutoSpawnMenuPath, true)]
        private static bool ValidateAutoSpawn()
        {
            Menu.SetChecked(k_AutoSpawnMenuPath, IsAutoSpawn);
            return true;
        }

        [MenuItem(k_AutoSpawnMenuPath, false)]
        private static void ToggleAutoSpawn()
        {
            IsAutoSpawn = !IsAutoSpawn;
        }

        [MenuItem(k_ManualSpawnMenuPath, true)]
        private static bool ValidateManualSpawn()
        {
            return true;
        }

        [MenuItem(k_ManualSpawnMenuPath, false)]
        private static void ExecuteManualSpawn()
        {
            Spawn(false);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (IsAutoSpawn && DragAndDrop.objectReferences.Length > 0)
            {
                if (HasOpenInstances<ObjectShelf>())
                {
                    GetWindow<ObjectShelf>();
                }
                else
                {
                    Spawn(true);
                }
            }
        }

        private static void Spawn(bool isAutoClose)
        {
            var rect = default(Rect);
            if (HasOpenInstances<ObjectShelf>())
            {
                rect = GetWindow<ObjectShelf>(null, false).position;
                rect = new Rect(rect.xMax + 20, rect.yMin, 200, 200);
            }
            else
            {
#if UNITY_2020_1_OR_NEWER
                rect = EditorGUIUtility.GetMainWindowPosition();
#else
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");
                foreach (var container in type.GetProperty("windows").GetValue(null) as object[])
                {
                    if ((bool)type.GetMethod("IsMainWindow").Invoke(container, null))
                    {
                        rect = (Rect)type.GetProperty("position").GetValue(container);
                        continue;
                    }
                }
#endif
                rect = new Rect(rect.xMin + 20, rect.yMax - 240, 200, 200);
            }

            var instance = CreateInstance<ObjectShelf>();
            instance.Show();
            instance.position = rect;
            instance.titleContent = new GUIContent(k_WindowTitle);
            instance.m_IsAutoClose = isAutoClose;
        }

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = null;

        [SerializeField]
        private VisualTreeAsset m_ListItemVisualTreeAsset = null;

#pragma warning disable 0414
        [SerializeField]
        private StyleSheet m_StyleSheet = null;

        [SerializeField]
        private StyleSheet m_LegacyStyleSheet = null;
#pragma warning restore 0414

        [SerializeField]
        private bool m_IsAutoClose = true;

        [SerializeField]
        private List<Object> m_ObjectReferences = new List<Object>();

        [SerializeField]
        private List<Object> m_LockedObjectReferences = new List<Object>();

        [NonSerialized]
        private Object[] m_SelectedObjectReferences = Array.Empty<Object>();

        [NonSerialized]
        private Object[] m_DraggingObjectReferences = Array.Empty<Object>();

        [NonSerialized]
        private Vector3? m_PointerDownPosition = null;

        [NonSerialized]
        private ListView m_ListView = null;

        private bool IsEmpty
        {
            get => rootVisualElement.ClassListContains(k_EmptyClassName);
            set => rootVisualElement.EnableInClassList(k_EmptyClassName, value);
        }

        private bool IsDraggingIn
        {
            get => rootVisualElement.ClassListContains(k_DraggingInClassName);
            set => rootVisualElement.EnableInClassList(k_DraggingInClassName, value);
        }

        private bool IsDraggingOut
        {
            get => rootVisualElement.ClassListContains(k_DraggingOutClassName);
            set => rootVisualElement.EnableInClassList(k_DraggingOutClassName, value);
        }

        private void ShowButton(Rect position)
        {
            m_IsAutoClose = !GUI.Toggle(position, !m_IsAutoClose, GUIContent.none, s_LockToggleStyle.Value);

            position.x -= 17;

            if (GUI.Button(position, EditorGUIUtility.IconContent(k_SpawnButtonIcon), s_SpawnButtonStyle.Value))
            {
                Spawn(false);
            }
        }

        private void CreateGUI()
        {
            m_VisualTreeAsset.CloneTree(rootVisualElement);
#if UNITY_2021_3_OR_NEWER
            rootVisualElement.styleSheets.Add(m_StyleSheet);
#else
            rootVisualElement.styleSheets.Add(m_LegacyStyleSheet);
            rootVisualElement.AddToClassList("unity-theme-env-variables");
#endif

            rootVisualElement.schedule
                .Execute(ObserveDragging)
                .Every(k_ObserveDraggingIntervalMs);

            var dropArea = rootVisualElement.Q<VisualElement>(k_DropAreaName);
            dropArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropArea.RegisterCallback<DragPerformEvent>(OnDragPerform);

            var listArea = rootVisualElement.Q<VisualElement>(k_ListAreaName);
            listArea.RegisterCallback<PointerDownEvent>(OnPointerDown);
            listArea.RegisterCallback<PointerUpEvent>(OnPointerUp);
            listArea.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            listArea.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            listArea.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            listArea.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            listArea.RegisterCallback<KeyDownEvent>(OnKeyDown);

            m_ListView = rootVisualElement.Q<ListView>(k_ListViewName);
            m_ListView.makeItem = () =>
            {
                var item = m_ListItemVisualTreeAsset.CloneTree();
                item.Q<Toggle>().RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        LockObjectReference(item.userData as Object);
                    }
                    else
                    {
                        UnlockObjectReference(item.userData as Object);
                    }
                });
                return item;
            };
            m_ListView.bindItem = (item, index) =>
            {
                item.userData = m_ObjectReferences[index];
                item.Q<Toggle>().SetValueWithoutNotify(IsLockedObjectReference(m_ObjectReferences[index]));
                if (m_ObjectReferences[index] != null)
                {
                    item.Q<Image>().image = AssetPreview.GetMiniThumbnail(m_ObjectReferences[index]);
                    item.Q<Label>().text = m_ObjectReferences[index].name;
                }
                else
                {
                    item.Q<Image>().image = EditorGUIUtility.IconContent(k_MissingObjectIcon).image;
                    item.Q<Label>().text = k_MissingObjectName;
                }
            };
            m_ListView.itemsSource = m_ObjectReferences;
            m_ListView.selectionChanged += x => m_SelectedObjectReferences = x.Cast<Object>().ToArray();

            IsEmpty = m_ObjectReferences.Count == 0;
        }

        private void ObserveDragging()
        {
            IsDraggingIn = m_DraggingObjectReferences.Length == 0 && DragAndDrop.objectReferences.Length > 0;
            IsDraggingOut = m_DraggingObjectReferences.Length > 0 && DragAndDrop.objectReferences.Length > 0;

            if (m_DraggingObjectReferences.Length > 0 && DragAndDrop.objectReferences.Length == 0)
            {
                RemoveObjectReferences(m_DraggingObjectReferences.Where(x => !IsLockedObjectReference(x)));

                m_DraggingObjectReferences = Array.Empty<Object>();
            }
            else
            {
                TryAutoClose();
            }
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            AcceptDrag();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (m_SelectedObjectReferences.Length == 0)
            {
                return;
            }

            switch (evt.button)
            {
                case (int)MouseButton.LeftMouse:
                    if (evt.propagationPhase != PropagationPhase.AtTarget)
                    {
                        m_PointerDownPosition = evt.position;
                    }
                    else
                    {
                        m_ListView.ClearSelection();
                    }
                    break;

                case (int)MouseButton.RightMouse:
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(k_FindSelectedMenuTitle), false, () =>
                    {
                        Selection.objects = m_SelectedObjectReferences;
                        foreach (var reference in m_SelectedObjectReferences)
                        {
                            EditorGUIUtility.PingObject(reference);
                        }
                    });
                    menu.AddItem(new GUIContent(k_RemoveSelectedMenuTitle), false, () =>
                    {
                        RemoveObjectReferences(m_SelectedObjectReferences);
                    });
                    menu.ShowAsContext();
                    break;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            m_PointerDownPosition = null;

            m_ListView.Focus();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_PointerDownPosition == null || (m_PointerDownPosition.Value - evt.position).sqrMagnitude < k_DragThreshold * k_DragThreshold)
            {
                return;
            }

            if (m_SelectedObjectReferences.Length > 0)
            {
                StartDrag();
            }

            m_PointerDownPosition = null;
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (m_PointerDownPosition == null)
            {
                return;
            }

            if (m_SelectedObjectReferences.Length > 0)
            {
                StartDrag();
            }

            m_PointerDownPosition = null;
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            m_PointerDownPosition = null;
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            m_PointerDownPosition = null;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                RemoveObjectReferences(m_SelectedObjectReferences);
            }
        }

        private void AcceptDrag()
        {
            DragAndDrop.AcceptDrag();

            AddObjectReferences(DragAndDrop.objectReferences.Where(x => x != null));

            IsDraggingIn = false;
            IsDraggingOut = false;

            m_DraggingObjectReferences = Array.Empty<Object>();

            m_ListView.Focus();
        }

        private void StartDrag()
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = m_SelectedObjectReferences
                .Where(x => x != null)
                .ToArray();
            DragAndDrop.paths = m_SelectedObjectReferences
                .Where(x => x != null)
                .Where(AssetDatabase.IsMainAsset)
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();
            DragAndDrop.StartDrag(
                DragAndDrop.objectReferences.Length > 1 ? k_MultipleDragTitle :
                DragAndDrop.objectReferences.SingleOrDefault() == null ? k_MissingObjectName :
                ObjectNames.GetDragAndDropTitle(DragAndDrop.objectReferences.Single()));

            IsDraggingIn = false;
            IsDraggingOut = true;

            m_DraggingObjectReferences = m_SelectedObjectReferences;
        }

        private void AddObjectReferences(IEnumerable<Object> objectReferences)
        {
            foreach (var reference in objectReferences)
            {
                if (!m_ObjectReferences.Contains(reference))
                {
                    m_ObjectReferences.Add(reference);
                }
            }

            IsEmpty = m_ObjectReferences.Count == 0;

            m_ListView.RefreshItems();

            m_ListView.ClearSelection();
            m_ListView.AddToSelection(objectReferences);
        }

        private void RemoveObjectReferences(IEnumerable<Object> objectReferences)
        {
            m_ListView.RemoveFromSelection(objectReferences);

            foreach (var reference in objectReferences)
            {
                if (m_ObjectReferences.Contains(reference))
                {
                    m_ObjectReferences.Remove(reference);
                }

                UnlockObjectReference(reference);
            }

            IsEmpty = m_ObjectReferences.Count == 0;

            m_ListView.RefreshItems();

            TryAutoClose();
        }

        private void LockObjectReference(Object objectReference)
        {
            if (!m_LockedObjectReferences.Contains(objectReference))
            {
                m_LockedObjectReferences.Add(objectReference);
            }
        }

        private void UnlockObjectReference(Object objectReference)
        {
            if (m_LockedObjectReferences.Contains(objectReference))
            {
                m_LockedObjectReferences.Remove(objectReference);
            }
        }

        private bool IsLockedObjectReference(Object objectReference)
        {
            return m_LockedObjectReferences.Contains(objectReference);
        }

        private void TryAutoClose()
        {
            if (m_IsAutoClose && DragAndDrop.objectReferences.Length == 0 && m_ObjectReferences.Count == 0)
            {
                Close();
            }
        }
    }
}

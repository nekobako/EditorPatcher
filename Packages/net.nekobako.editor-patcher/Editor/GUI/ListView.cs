using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace net.nekobako.EditorPatcher.Editor
{
    public class ListView : UnityEngine.UIElements.ListView
    {
#if !UNITY_2022_2_OR_NEWER
        public event Action<IEnumerable<object>> selectionChanged
        {
            add => onSelectionChanged += value;
            remove => onSelectionChanged -= value;
        }
#endif

#if !UNITY_2021_2_OR_NEWER
        public new void AddToSelection(int index)
        {
            base.AddToSelection(index);
        }

        public new void RemoveFromSelection(int index)
        {
            base.RemoveFromSelection(index);
        }

        public new void ClearSelection()
        {
            base.ClearSelection();
        }

        public void RefreshItems()
        {
            Refresh();
        }
#endif

        public void AddToSelection(IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                var index = itemsSource.IndexOf(item);
                if (index >= 0)
                {
                    AddToSelection(index);
                }
            }
        }

        public void RemoveFromSelection(IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                var index = itemsSource.IndexOf(item);
                if (index >= 0)
                {
                    RemoveFromSelection(index);
                }
            }
        }

        public new class UxmlFactory : UxmlFactory<ListView, UxmlTraits>
        {
        }

        public new class UxmlTraits : UnityEngine.UIElements.ListView.UxmlTraits
        {
#if !UNITY_2021_3_OR_NEWER
            private readonly UxmlEnumAttributeDescription<SelectionType> m_SelectionType = new UxmlEnumAttributeDescription<SelectionType>
            {
                name = "selection-type",
                defaultValue = SelectionType.Single,
            };

            public override void Init(VisualElement visualElement, IUxmlAttributes uxmlAttributes, CreationContext creationContext)
            {
                base.Init(visualElement, uxmlAttributes, creationContext);

                var listView = visualElement as ListView;
                listView.selectionType = m_SelectionType.GetValueFromBag(uxmlAttributes, creationContext);
            }
#endif
        }
    }
}

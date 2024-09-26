using UnityEditor.IMGUI.Controls;

namespace net.nekobako.EditorPatcher.Editor
{
    internal interface ITreeData
    {
        int Id { get; }
        int Depth { get; }
        string DisplayName { get; }
    }

    internal class TreeViewItem<T> : TreeViewItem where T : ITreeData
    {
        public readonly T Data = default;

        public TreeViewItem(T data) : base(data.Id, data.Depth, data.DisplayName)
        {
            Data = data;
        }
    }
}

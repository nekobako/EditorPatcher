using UnityEngine;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class TempContent
    {
        private static readonly GUIContent s_Content = new GUIContent();

        public static GUIContent Text(string text)
        {
            s_Content.text = text;
            s_Content.image = null;
            s_Content.tooltip = string.Empty;
            return s_Content;
        }
    }
}

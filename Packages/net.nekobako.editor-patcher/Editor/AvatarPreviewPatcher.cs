using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using HarmonyLib;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class AvatarPreviewPatcher
    {
        private const string k_PatchId = "net.nekobako.editor-patcher.avatar-preview-patcher";
        private const string k_MenuPath = "Tools/Editor Patcher/Avatar Preview";
        private const string k_2DPref = "Avatarpreview2D";

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

            harmony.Patch(AccessTools.PropertySetter(AccessTools.TypeByName("UnityEditor.AvatarPreview"), "is2D"),
                new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(SetIs2D)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:ResetPreviewFocus"),
                new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(ResetPreviewFocus)));

            harmony.CreateReversePatcher(AccessTools.Method("UnityEditor.AvatarPreview:ResetPreviewFocus"),
                new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(ResetPreviewFocus_Reverse))).Patch();

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:SetPreview"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(SetPreview_Postfix)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:ResetPreviewInstance"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(ResetPreviewInstance_Postfix)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:DoAvatarPreviewPan"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(DoAvatarPreviewPan_Postfix)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:DoAvatarPreviewOrbit"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(DoAvatarPreviewOrbit_Postfix)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:DoAvatarPreviewZoom"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(DoAvatarPreviewZoom_Postfix)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:DoAvatarPreviewFrame"),
                new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(DoAvatarPreviewFrame)));

            harmony.Patch(AccessTools.Method("UnityEditor.AvatarPreview:DoPreviewSettings"),
                postfix: new HarmonyMethod(typeof(AvatarPreviewPatcher), nameof(DoPreviewSettings_Postfix)));

            AssemblyReloadEvents.beforeAssemblyReload += () => harmony.UnpatchAll(k_PatchId);
        }

        private static bool SetIs2D(bool value, ref bool ___m_2D)
        {
            if (!IsEnabled)
            {
                return true;
            }

            ___m_2D = value;

            // Save 2D mode properly when rotating the view
            EditorPrefs.SetBool(k_2DPref, value);

            return false;
        }

        private static bool ResetPreviewFocus(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, bool ___m_2D, float ___m_AvatarScale, Motion ___m_SourcePreviewMotion)
        {
            if (!IsEnabled)
            {
                return true;
            }

            ResetView(__instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_2D, ___m_AvatarScale, ___m_SourcePreviewMotion);
            LoadStates(ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor);

            return false;
        }

        private static void ResetPreviewFocus_Reverse(object __instance)
        {
            throw new NotImplementedException();
        }

        private static void SetPreview_Postfix(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, bool ___m_2D, float ___m_AvatarScale, Motion ___m_SourcePreviewMotion)
        {
            if (!IsEnabled)
            {
                return;
            }

            ResetView(__instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_2D, ___m_AvatarScale, ___m_SourcePreviewMotion);
            SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
        }

        private static void ResetPreviewInstance_Postfix(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, bool ___m_2D, float ___m_AvatarScale, Motion ___m_SourcePreviewMotion)
        {
            if (!IsEnabled)
            {
                return;
            }

            ResetView(__instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_2D, ___m_AvatarScale, ___m_SourcePreviewMotion);
            SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
        }

        private static void DoAvatarPreviewPan_Postfix(Vector3 ___m_PivotPositionOffset, Vector2 ___m_PreviewDir, float ___m_ZoomFactor)
        {
            if (!IsEnabled)
            {
                return;
            }

            SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
        }

        private static void DoAvatarPreviewOrbit_Postfix(Vector3 ___m_PivotPositionOffset, Vector2 ___m_PreviewDir, float ___m_ZoomFactor)
        {
            if (!IsEnabled)
            {
                return;
            }

            SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
        }

        private static void DoAvatarPreviewZoom_Postfix(Vector3 ___m_PivotPositionOffset, Vector2 ___m_PreviewDir, float ___m_ZoomFactor)
        {
            if (!IsEnabled)
            {
                return;
            }

            SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
        }

        private static bool DoAvatarPreviewFrame(Event evt, EventType type, object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, float ___m_AvatarScale)
        {
            if (!IsEnabled)
            {
                return true;
            }

            if (type == EventType.KeyDown && evt.keyCode == KeyCode.F)
            {
                FocusView(__instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
                evt.Use();
            }

            return false;
        }

        private static void DoPreviewSettings_Postfix(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, float ___m_AvatarScale)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (GUILayout.Button("-X", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(-90.0f, 0.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button("+X", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(+90.0f, 0.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button("-Y", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(0.0f, +90.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button("+Y", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(0.0f, -90.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button("-Z", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(0.0f, 0.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button("+Z", EditorStyles.toolbarButton))
            {
                AlignView(new Vector2(180.0f, 0.0f), __instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh"), EditorStyles.toolbarButton))
            {
                FocusView(__instance, ref ___m_PivotPositionOffset, ref ___m_PreviewDir, ref ___m_ZoomFactor, ___m_AvatarScale);
                SaveStates(___m_PivotPositionOffset, ___m_PreviewDir, ___m_ZoomFactor);
            }
        }

        private static void ResetView(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, bool ___m_2D, float ___m_AvatarScale, Motion ___m_SourcePreviewMotion)
        {
            var direction = ___m_2D ? Vector2.zero : new Vector2(120.0f, -20.0f);

#if UNITY_2020_1_OR_NEWER
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(___m_SourcePreviewMotion)) as ModelImporter;
            if (importer != null && importer.bakeAxisConversion)
            {
                direction += new Vector2(180.0f, 0.0f);
            }
#endif

            ResetPreviewFocus_Reverse(__instance);
            ___m_PreviewDir = direction;
            ___m_ZoomFactor = ___m_AvatarScale;
        }

        private static void AlignView(Vector2 direction, object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, float ___m_AvatarScale)
        {
            ResetPreviewFocus_Reverse(__instance);
            ___m_PreviewDir = direction;
            ___m_ZoomFactor = ___m_AvatarScale;
        }

        private static void FocusView(object __instance, ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor, float ___m_AvatarScale)
        {
            ResetPreviewFocus_Reverse(__instance);
            ___m_ZoomFactor = ___m_AvatarScale;
        }

        private static void LoadStates(ref Vector3 ___m_PivotPositionOffset, ref Vector2 ___m_PreviewDir, ref float ___m_ZoomFactor)
        {
            ___m_PivotPositionOffset = SessionState.GetVector3($"{k_PatchId}_PivotPositionOffset", ___m_PivotPositionOffset);
            ___m_PreviewDir = SessionState.GetVector3($"{k_PatchId}_PreviewDir", ___m_PreviewDir);
            ___m_ZoomFactor = SessionState.GetFloat($"{k_PatchId}_ZoomFactor", ___m_ZoomFactor);
        }

        private static void SaveStates(Vector3 ___m_PivotPositionOffset, Vector2 ___m_PreviewDir, float ___m_ZoomFactor)
        {
            SessionState.SetVector3($"{k_PatchId}_PivotPositionOffset", ___m_PivotPositionOffset);
            SessionState.SetVector3($"{k_PatchId}_PreviewDir", ___m_PreviewDir);
            SessionState.SetFloat($"{k_PatchId}_ZoomFactor", ___m_ZoomFactor);
        }
    }
}

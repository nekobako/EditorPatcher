#if UNITY_2021_2_OR_NEWER
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace net.nekobako.EditorPatcher.Editor
{
    internal class PlatformSwitcher
    {
        private const string k_MenuPath = "Tools/Editor Patcher/Platform Switcher";
        private const string k_ToolbarZoneName = "ToolbarZonePlayMode";

        private static VisualElement s_Container = null;

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
            s_Container.style.display = IsEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                var toolbarType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Toolbar");
                var toolbar = toolbarType.GetField("get").GetValue(null);
                if (toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(toolbar) is not VisualElement root)
                {
                    return;
                }

                // Root element will be detached after editor layout changes
                root.RegisterCallback<DetachFromPanelEvent>(evt =>
                {
                    Initialize();
                });

                var zone = root.Q(k_ToolbarZoneName);
                s_Container?.RemoveFromHierarchy();
                s_Container = new()
                {
                    style =
                    {
                        display = IsEnabled ? DisplayStyle.Flex : DisplayStyle.None,
                        flexDirection = FlexDirection.Row,
                    },
                };
                zone.Add(s_Container);

                var buildTargetDiscoveryType = typeof(EditorWindow).Assembly.GetType("UnityEditor.BuildTargetDiscovery");
                var discoveredTargetInfoType = typeof(EditorWindow).Assembly.GetType("UnityEditor.BuildTargetDiscovery+DiscoveredTargetInfo");
                var discoveredTargetInfoNiceNameField = discoveredTargetInfoType.GetField("niceName");
                var discoveredTargetInfoIconNameField = discoveredTargetInfoType.GetField("iconName");
                var discoveredTargetInfoBuildTargetPlatformValField = discoveredTargetInfoType.GetField("buildTargetPlatformVal");
                var moduleManagerType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Modules.ModuleManager");
                var moduleManagerIsPlatformSupportLoadedByBuildTargetMethod = moduleManagerType.GetMethod("IsPlatformSupportLoadedByBuildTarget", BindingFlags.Static | BindingFlags.NonPublic);
                var targets = (buildTargetDiscoveryType.GetMethod("GetBuildTargetInfoList").Invoke(null, null) as Array)
                    .Cast<object>()
                    .Select(x => new
                    {
                        Name = (string)discoveredTargetInfoNiceNameField.GetValue(x),
                        Icon = (string)discoveredTargetInfoIconNameField.GetValue(x),
                        BuildTarget = (BuildTarget)discoveredTargetInfoBuildTargetPlatformValField.GetValue(x),
                    })
                    .Where(x => (bool)moduleManagerIsPlatformSupportLoadedByBuildTargetMethod.Invoke(null, new object[] { x.BuildTarget }))
                    .ToArray();

                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    var toggle = new EditorToolbarToggle(EditorGUIUtility.IconContent(target.Icon).image as Texture2D)
                    {
                        userData = target.BuildTarget,
                        tooltip = target.Name,
                        style =
                        {
                            marginLeft = i == 0 ? 20 : 0,
                            marginRight = 0,
                            borderLeftWidth = i == 0 ? 0 : 1,
                            borderRightWidth = 0,
                            borderTopLeftRadius = i == 0 ? 2 : 0,
                            borderBottomLeftRadius = i == 0 ? 2 : 0,
                            borderTopRightRadius = i == targets.Length - 1 ? 2 : 0,
                            borderBottomRightRadius = i == targets.Length - 1 ? 2 : 0,
                        },
                    };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue)
                        {
                            EditorUserBuildSettings.selectedBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(target.BuildTarget);
                            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(target.BuildTarget), target.BuildTarget);
                        }

                        UpdateToggles();
                    });
                    s_Container.Add(toggle);
                }

                UpdateToggles();

                static void UpdateToggles()
                {
                    foreach (var toggle in s_Container.Children().OfType<EditorToolbarToggle>())
                    {
                        toggle.SetValueWithoutNotify(EditorUserBuildSettings.activeBuildTarget == (BuildTarget)toggle.userData);
                    }
                }
            };
        }
    }
}
#endif

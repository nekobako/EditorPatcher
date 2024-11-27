using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class FixPrefabOverride
    {
        public static int RevertSameOverride(Object targetObject)
        {
            var sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
            if (sourceObject == null) return 0;

            var serializedTarget = new SerializedObject(targetObject);
            var serializedSource = new SerializedObject(sourceObject);

            serializedTarget.ApplyModifiedProperties();
            serializedTarget.Update();
            serializedSource.ApplyModifiedProperties();
            serializedSource.Update();
            
            int totalReverts = 0;
            var targetProperty = serializedTarget.GetIterator();
            while (targetProperty.Next(true))
            {
                if (!targetProperty.prefabOverride || targetProperty.isDefaultOverride) continue;

                var propertyPath = targetProperty.propertyPath;
                var sourceProperty = serializedSource.FindProperty(propertyPath);

                if (sourceProperty == null)
                {
                    // nullな場合配列が読み込まれていない可能性がある
                    // 配列長の変更による読み込みを試す
                    if (!TryLoadArray(ref sourceProperty, serializedSource, propertyPath))
                    {
                        continue;
                    }
                }

                if (!ArePropertiesApproximatelyEqual(sourceProperty, targetProperty)) continue;

                PrefabUtility.RevertPropertyOverride(targetProperty, InteractionMode.AutomatedAction);
                totalReverts++;
            }
            return totalReverts;
        }
        
        // 浮動小数点誤差を許容した比較を行う
        // 内部では-0、inspector上では0のような場合に0を入力してRevertされるようにしたい
        private static bool ArePropertiesApproximatelyEqual(SerializedProperty prop1, SerializedProperty prop2)
        {
            if (prop1.propertyType != prop2.propertyType) return false;

            switch (prop1.propertyType)
            {
                case SerializedPropertyType.Float:
                    return prop1.floatValue == prop2.floatValue;
                case SerializedPropertyType.Vector2:
                    return prop1.vector2Value == prop2.vector2Value;
                case SerializedPropertyType.Vector3:
                    return prop1.vector3Value == prop2.vector3Value;
                case SerializedPropertyType.Vector4:
                    return prop1.vector4Value == prop2.vector4Value;
                case SerializedPropertyType.Quaternion:
                    return prop1.quaternionValue == prop2.quaternionValue;
                default:
                    return SerializedProperty.DataEquals(prop1, prop2);
            }
        }

        private static bool TryLoadArray(ref SerializedProperty sourceProp, SerializedObject serializedSource, string path)
        {
            // _elements.Array.data[x]のような形であることを確認
            var match = Regex.Match(path, @"^(.+?\.Array)\.data\[(\d+)\]$");
            if (match.Success)
            {
                var arrayPath = match.Groups[1].Value;
                if (!int.TryParse(match.Groups[2].Value, out var index))
                {
                    return false;
                }
                
                var arrayProp = serializedSource.FindProperty(arrayPath);
                if (arrayProp != null && arrayProp.isArray && index + 1 > arrayProp.arraySize)
                {
                    // 配列長の変更前にOverrideがあるか確認
                    bool beforeModifications = arrayProp.prefabOverride;

                    // 配列長を変更
                    arrayProp.arraySize += index + 1;
                    serializedSource.ApplyModifiedProperties();
                    serializedSource.Update();

                    // 変更後に再度取得を試す
                    sourceProp = serializedSource.FindProperty(path);
                    
                    if (sourceProp != null)
                    {
                        return true;
                    }
                    // 再度取得できなかった場合
                    else
                    {
                        // 配列長を戻す
                        arrayProp.arraySize -= index + 1;
                        serializedSource.ApplyModifiedProperties();
                        serializedSource.Update();

                        // 変更前に存在しなかったOverrideが配列の変更により生じた場合はRevert
                        if (!beforeModifications && arrayProp.prefabOverride)
                        {
                            PrefabUtility.RevertPropertyOverride(arrayProp, InteractionMode.AutomatedAction);
                        }
                    }
                }
            }
            return false;
        }
    }
}
using System;
using UnityEditor;

namespace net.nekobako.EditorPatcher.Editor
{
    internal static class FixPrefabOverride
    {
        public static int RevertSameOverride(UnityEngine.Object targetObject)
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
                
                var shouldRevert = sourceProperty == null
                    ? IsDefaultValue(targetProperty)
                    : ArePropertiesApproximatelyEqual(sourceProperty, targetProperty);

                if (shouldRevert)
                {
                    PrefabUtility.RevertPropertyOverride(targetProperty, InteractionMode.AutomatedAction);
                    totalReverts++;
                }
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

        private static bool IsDefaultValue(SerializedProperty prop)
        {
#if UNITY_2022_1_OR_NEWER
            var value = prop.boxedValue;
            if (value == null) return false;
            var type = value.GetType();
            if (!type.IsValueType) return false;
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
#else
            return false;
#endif 
        }
    }
}
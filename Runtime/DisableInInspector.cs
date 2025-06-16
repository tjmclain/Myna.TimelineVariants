using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Myna.Assets
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class DisableInInspectorAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DisableInInspectorAttribute))]
    internal class DisableInInspectorPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }
#endif
}

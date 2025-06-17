#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    [Serializable]
    internal struct ObjectMapping
    {
        public Object Source;
        public Object Target;

        public ObjectMapping(Object source, Object target)
        {
            Source = source;
            Target = target;
        }
    }

    internal static class ObjectMapExtensions
    {
        internal static Object GetCorrespondingObject(this List<ObjectMapping> map, Object obj)
        {
            if (obj != null)
            {
                foreach (var mapping in map)
                {
                    if (mapping.Source == obj)
                    {
                        return mapping.Target;
                    }

                    if (mapping.Target == obj)
                    {
                        return mapping.Source;
                    }
                }
            }

            return obj;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal class ObjectMappingListItemAttribute : PropertyAttribute { }

    [CustomPropertyDrawer(typeof(ObjectMappingListItemAttribute))]
    internal class ObjectMappingListItemPropertyDrawer : PropertyDrawer
    {
        private static float Space => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var sourceProp = property.FindPropertyRelative(nameof(ObjectMapping.Source));
            var targetProp = property.FindPropertyRelative(nameof(ObjectMapping.Target));

            float w = (position.width - Space) / 2f;
            var rect = new Rect(position.x, position.y, w, position.height);
            EditorGUI.PropertyField(rect, sourceProp, GUIContent.none);

            rect = new Rect(position.x + w + Space, position.y, w, position.height);
            EditorGUI.PropertyField(rect, targetProp, GUIContent.none);
        }
    }
}
#endif
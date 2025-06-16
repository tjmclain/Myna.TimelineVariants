using System;
using UnityEditor;
using UnityEngine;

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    [Serializable]
    public partial struct ObjectMapping { }

#if UNITY_EDITOR
    partial struct ObjectMapping
    {
        public ObjectReference Source;
        public ObjectReference Target;

        public ObjectMapping(Object source, Object target)
        {
            Source = ObjectReference.Get(source);
            Target = ObjectReference.Get(target);
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
#endif
}

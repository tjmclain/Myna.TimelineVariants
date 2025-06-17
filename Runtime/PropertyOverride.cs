#if UNITY_EDITOR
using System;
using Newtonsoft.Json;
using UnityEditor;

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    [Serializable]
    public struct PropertyOverride
    {
        public string PropertyPath;
        public Object Source;
        public string Value;
        public Object ObjectReference;

        public PropertyOverride(Object source, SerializedProperty property)
            : this(source, property.propertyPath, property.boxedValue) { }

        public PropertyOverride(Object source, string propertyPath, object value)
        {
            Source = source;
            PropertyPath = propertyPath;
            if (value is Object objectReference)
            {
                ObjectReference = objectReference;
                Value = string.Empty;
            }
            else
            {
                ObjectReference = null;
                Value = JsonConvert.SerializeObject(value);
            }
        }
    }
}
#endif

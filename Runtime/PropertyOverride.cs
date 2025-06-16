using System;
using Newtonsoft.Json;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    [Serializable]
    public partial struct PropertyOverride { }

#if UNITY_EDITOR
    partial struct PropertyOverride
    {
        public string PropertyPath;
        public ObjectReference Source;
        [TextArea]
        public string Value;

        public PropertyOverride(Object source, SerializedProperty property)
        {
            Source = ObjectReference.Get(source);
            PropertyPath = property.propertyPath;
            Value = JsonConvert.SerializeObject(property.boxedValue);
        }

        public PropertyOverride(Object source, string propertyPath, object value)
        {
            Source = ObjectReference.Get(source);
            PropertyPath = propertyPath;
            Value = JsonConvert.SerializeObject(value);
        }
    }
#endif
}

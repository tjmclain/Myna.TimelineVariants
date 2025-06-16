using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    [Serializable]
    public partial struct ObjectReference { }

#if UNITY_EDITOR
    partial struct ObjectReference
    {
        public string Guid;
        public long LocalId;

        private Object _object;

        internal static readonly ObjectReference Null = new(null);

        internal Object Object => GetObject();

        private ObjectReference(Object obj)
        {
            if (obj != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long id))
            {
                Guid = guid;
                LocalId = id;
                _object = obj;
            }
            else
            {
                Guid = string.Empty;
                LocalId = 0;
                _object = null;
            }
        }

        internal Object GetObject()
        {
            if (_object == null)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(Guid);
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var asset in assets)
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id) && id == LocalId)
                    {
                        _object = asset;
                        break;
                    }
                }
            }

            return _object;
        }

        internal static ObjectReference Get(Object obj)
        {
            return new ObjectReference(obj);
        }

        internal static ObjectReference GetMapped(ObjectReference obj, IEnumerable<ObjectMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.Source == obj)
                {
                    obj = mapping.Target;
                    break;
                }

                if (mapping.Target == obj)
                {
                    obj = mapping.Source;
                    break;
                }
            }

            return obj;
        }

        internal static ObjectReference GetMappedFromJson(string json, IEnumerable<ObjectMapping> mappings)
        {
            var obj = JsonConvert.DeserializeObject<ObjectReference>(json);
            return GetMapped(obj, mappings);
        }

        public static bool operator ==(ObjectReference a, ObjectReference b)
        {
            return a.Guid == b.Guid && a.LocalId == b.LocalId;
        }

        public static bool operator !=(ObjectReference a, ObjectReference b)
        {
            return a.Guid != b.Guid || a.LocalId != b.LocalId;
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is ObjectReference other)
            {
                return this == other;
            }
            else if (obj is Object unityObject)
            {
                return this == Get(unityObject);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Object, Guid, LocalId);
        }

        public override string ToString()
        {
            return Object != null ? Object.ToString() : "Null";
        }
    }

    [CustomPropertyDrawer(typeof(ObjectReference))]
    internal class ObjectReferencePropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var target = (ObjectReference)property.boxedValue;
            EditorGUI.ObjectField(position, label, target.Object, typeof(Object), false);
        }
    }
#endif
}

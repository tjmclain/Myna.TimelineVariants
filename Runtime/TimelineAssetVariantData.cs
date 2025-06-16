using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Myna.Assets
{
    using Object = UnityEngine.Object;

    public partial class TimelineAssetVariantData : ScriptableObject { }

#if UNITY_EDITOR
    partial class TimelineAssetVariantData
    {
        [SerializeField, DisableInInspector]
        private TimelineAsset _source;

        [SerializeField, DisableInInspector, ObjectMappingListItem]
        private List<ObjectMapping> _objectMappings = new();

        [SerializeField, DisableInInspector]
        private List<ObjectReference> _addedObjects = new();

        [SerializeField, DisableInInspector]
        private List<ObjectReference> _removedObjects = new();

        [SerializeField, DisableInInspector]
        private List<PropertyOverride> _propertyOverrides = new();

        internal TimelineAsset Source
        {
            get => _source;
            set => _source = value;
        }

        internal List<ObjectMapping> ObjectMappings
        {
            get => _objectMappings;
            set => _objectMappings = value;
        }

        internal List<ObjectReference> AddedObjects
        {
            get => _addedObjects;
            set => _addedObjects = value;
        }

        internal List<ObjectReference> RemovedObjects
        {
            get => _removedObjects;
            set => _removedObjects = value;
        }

        internal List<PropertyOverride> PropertyOverrides
        {
            get => _propertyOverrides;
            set => _propertyOverrides = value;
        }

        private static readonly string[] SkippedPropertyNames = new[]
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier",
            "m_Version"
        };

        private static ILogger Logger => TimelineAssetVariant.Logger;

        internal void MapSourceToTargetObjects(Object target)
        {
            Assert.IsNotNull(Source, "'Source' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(Source), "'Source' is not a project asset");
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            // Map source root object to target root object
            if (!ObjectMappings.Any(x => x.Source.Object == Source))
            {
                ObjectMappings.Add(new(Source, target));
            }

            string assetPath = AssetDatabase.GetAssetPath(Source);
            var sourceObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(x => !AssetDatabase.IsMainAsset(x));

            Logger.Log(
                nameof(MapSourceToTargetObjects),
                $"Copying {sourceObjects.Count()} assets from {assetPath}"
            );

            foreach (var sourceObject in sourceObjects)
            {
                int index = ObjectMappings.FindIndex(x => x.Source.Object == sourceObject);
                var mapping = index >= 0 ? ObjectMappings[index] : new ObjectMapping();
                mapping.Source = ObjectReference.Get(sourceObject);

                if (!RemovedObjects.Contains(mapping.Source))
                {
                    var targetObject = mapping.Target.Object;
                    if (targetObject == null)
                    {
                        targetObject = Instantiate(sourceObject);
                        targetObject.name = sourceObject.name;
                        targetObject.hideFlags = sourceObject.hideFlags;
                        AssetDatabase.AddObjectToAsset(targetObject, target);
                    }
                    mapping.Target = ObjectReference.Get(targetObject);
                }
                else
                {
                    mapping.Target = ObjectReference.Null;
                }

                if (index >= 0)
                {
                    ObjectMappings[index] = mapping;
                }
                else
                {
                    ObjectMappings.Add(mapping);
                }
            }
        }

        internal void RemoveInvalidTargetObjects(Object target)
        {
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            string assetPath = AssetDatabase.GetAssetPath(target);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(x => !AssetDatabase.IsMainAsset(x))
                .Where(x => x is not TimelineAssetVariantData);

            foreach (var obj in objects)
            {
                var key = ObjectReference.Get(obj);
                if (!AddedObjects.Contains(key) && !ObjectMappings.Any(x => x.Target == key))
                {
                    AssetDatabase.RemoveObjectFromAsset(obj);
                    DestroyImmediate(obj);
                }
            }
        }

        internal void CopySourcePropertiesToTarget()
        {
            foreach (var mapping in ObjectMappings)
            {
                var source = mapping.Source.Object;
                if (source == null)
                {
                    continue;
                }

                var target = mapping.Target.Object;
                if (target == null)
                {
                    continue;
                }

                using var serializedSource = new SerializedObject(source);
                using var serializedTarget = new SerializedObject(target);

                Logger.Log(nameof(CopySourcePropertiesToTarget), $"{mapping.Source} --> {mapping.Target}");

                var iterator = serializedSource.GetIterator();
                iterator.Next(true);

                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;

                    if (Array.Exists(SkippedPropertyNames, x => x == iterator.name))
                    {
                        enterChildren = false;
                        continue;
                    }

                    var sourceProp = iterator.Copy();
                    var targetProp = serializedTarget.FindProperty(sourceProp.propertyPath);

                    Logger.Log(nameof(CopySourcePropertiesToTarget), $"-- {sourceProp.propertyPath} ({sourceProp.propertyType})");
                    switch (sourceProp.propertyType)
                    {
                        case SerializedPropertyType.Generic:
                            // Skip Generic properties...
                            break;

                        case SerializedPropertyType.ManagedReference:
                        case SerializedPropertyType.ExposedReference:
                            Logger.LogWarning(nameof(CopySourcePropertiesToTarget), $"---- Skipping {sourceProp.propertyType} at path {sourceProp.propertyPath}");
                            break;

                        case SerializedPropertyType.String:
                            enterChildren = false;
                            targetProp.stringValue = sourceProp.stringValue;
                            break;

                        case SerializedPropertyType.ObjectReference:
                            enterChildren = false;
                            var obj = ObjectReference.Get(sourceProp.objectReferenceValue);
                            targetProp.objectReferenceValue = ObjectReference.GetMapped(obj, ObjectMappings).Object;
                            break;

                        case SerializedPropertyType.ArraySize:
                            targetProp.intValue = sourceProp.intValue;
                            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                            break;

                        default:
                            targetProp.boxedValue = sourceProp.boxedValue;
                            break;
                    }
                }

                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal void ApplyPropertyOverrides()
        {
            foreach (var propertyOverride in PropertyOverrides)
            {
                var target = ObjectReference.GetMapped(propertyOverride.Source, ObjectMappings).Object;
                if (target == null)
                {
                    Logger.LogError(nameof(ApplyPropertyOverrides), "target == null for source " + propertyOverride.Source);
                    continue;
                }

                using var serializedObject = new SerializedObject(target);
                var property = serializedObject.FindProperty(propertyOverride.PropertyPath);
                if (property == null)
                {
                    Logger.LogError(
                        nameof(ApplyPropertyOverrides),
                        $"property == null; target = {target}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                    continue;
                }

                string json = propertyOverride.Value;
                object value = property.propertyType switch
                {
                    SerializedPropertyType.ObjectReference => ObjectReference.GetMappedFromJson(json, ObjectMappings).Object,
                    _ => JsonConvert.DeserializeObject(json)
                };

                property.boxedValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                if (value != null)
                {
                    Logger.Log(
                        nameof(ApplyPropertyOverrides),
                        $"Set value = {value} ({value.GetType()}); target = {target}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                }
                else
                {
                    Logger.Log(
                        nameof(ApplyPropertyOverrides),
                        $"Set value = null; target = {target}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                }
            }
        }

        internal void FindAddedObjects(Object target)
        {
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            var keys = new List<ObjectReference>();
            string assetPath = AssetDatabase.GetAssetPath(target);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(x => !AssetDatabase.IsMainAsset(x) && x is not TimelineAssetVariantData);
            foreach (var obj in objects)
            {
                var key = ObjectReference.Get(obj);
                var mapping = ObjectMappings.FirstOrDefault(x => x.Target == key);
                if (mapping.Source.Object == null)
                {
                    Logger.Log(nameof(FindAddedObjects), "Add " + obj);
                    keys.Add(key);
                }
            }

            AddedObjects = keys;
        }

        internal void FindRemovedObjects()
        {
            Assert.IsNotNull(Source, "'Source' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(Source), "'Source' is not a project asset");

            var keys = new List<ObjectReference>();
            string assetPath = AssetDatabase.GetAssetPath(Source);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(x => !AssetDatabase.IsMainAsset(x));
            foreach (var obj in objects)
            {
                var key = ObjectReference.Get(obj);
                var mapping = ObjectMappings.FirstOrDefault(x => x.Source == key);
                if (mapping.Target.Object == null)
                {
                    Logger.Log(nameof(FindRemovedObjects), "Removed " + obj);
                    keys.Add(key);
                }
            }

            RemovedObjects = keys;
        }

        internal void FindPropertyOverrides()
        {
            int count = 0;
            PropertyOverrides.Clear();

            foreach (var mapping in ObjectMappings)
            {
                var source = mapping.Source.Object;
                if (source == null)
                {
                    continue;
                }

                var target = mapping.Target.Object;
                if (target == null)
                {
                    continue;
                }

                using var serializedSource = new SerializedObject(source);
                using var serializedTarget = new SerializedObject(target);

                Logger.Log(source);

                var iterator = serializedTarget.GetIterator();
                iterator.Next(true);

                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;

                    if (Array.Exists(SkippedPropertyNames, x => x == iterator.name))
                    {
                        enterChildren = false;
                        continue;
                    }

                    var targetProp = iterator.Copy();
                    var sourceProp = serializedSource.FindProperty(targetProp.propertyPath);

                    switch (targetProp.propertyType)
                    {
                        case SerializedPropertyType.Generic:
                            // Skip generic properties...
                            break;

                        case SerializedPropertyType.ManagedReference:
                        case SerializedPropertyType.ExposedReference:
                            Logger.LogWarning(nameof(FindPropertyOverrides), $"-- Skipping {targetProp.propertyType} at path {targetProp.propertyPath}");
                            break;

                        case SerializedPropertyType.String:
                            enterChildren = false;
                            if (sourceProp == null || targetProp.stringValue != sourceProp.stringValue)
                            {
                                PropertyOverrides.Add(new(source, targetProp));
                            }
                            break;

                        case SerializedPropertyType.ObjectReference:
                            enterChildren = false;
                            var targetObj = ObjectReference.Get(targetProp.objectReferenceValue);
                            var correspondingObj = ObjectReference.GetMapped(targetObj, ObjectMappings);
                            var sourceObj = sourceProp != null ? ObjectReference.Get(sourceProp.objectReferenceValue) : ObjectReference.Null;
                            if (sourceProp == null || correspondingObj != sourceObj)
                            {
                                PropertyOverrides.Add(new(source, targetProp.propertyPath, correspondingObj));
                            }
                            break;

                        default:
                            if (!targetProp.hasChildren && (sourceProp == null || !targetProp.boxedValue.Equals(sourceProp.boxedValue)))
                            {
                                PropertyOverrides.Add(new(source, targetProp));
                            }
                            break;
                    }

                    if (count < PropertyOverrides.Count)
                    {
                        count = PropertyOverrides.Count;
                        Logger.Log(
                            nameof(FindPropertyOverrides),
                            $"-- {targetProp.propertyPath} ({targetProp.propertyType}): {PropertyOverrides.LastOrDefault().Value}"
                            );
                    }
                }

                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
#endif
}

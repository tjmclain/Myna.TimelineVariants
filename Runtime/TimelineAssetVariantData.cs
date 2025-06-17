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
        private List<ObjectMapping> _objectMap = new();

        [SerializeField, DisableInInspector]
        private List<Object> _addedObjects = new();

        [SerializeField, DisableInInspector]
        private List<Object> _removedObjects = new();

        [SerializeField, DisableInInspector]
        private List<PropertyOverride> _propertyOverrides = new();

        internal TimelineAsset Source
        {
            get => _source;
            set => _source = value;
        }

        internal List<ObjectMapping> ObjectMap
        {
            get => _objectMap;
            set => _objectMap = value;
        }

        internal List<Object> AddedObjects
        {
            get => _addedObjects;
            set => _addedObjects = value;
        }

        internal List<Object> RemovedObjects
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

        public void ApplyOverrides(Object target)
        {
            MapSourceToTargetObjects(target);
            RemoveInvalidTargetObjects(target);
            CopySourcePropertiesToTarget();
            ApplyPropertyOverrides(target);
        }

        public void RecordOverrides(Object target)
        {
            FindAddedObjects(target);
            FindRemovedObjects();
            FindPropertyOverrides();
        }

        public void ResetOverrides()
        {
            _addedObjects.Clear();
            _removedObjects.Clear();
            _propertyOverrides.Clear();
        }

        private void MapSourceToTargetObjects(Object target)
        {
            Assert.IsNotNull(Source, "'Source' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(Source), "'Source' is not a project asset");
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            // Map source root object to target root object
            if (!ObjectMap.Any(x => x.Source == Source))
            {
                ObjectMap.Add(new(Source, target));
            }

            string assetPath = AssetDatabase.GetAssetPath(Source);
            var sourceObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(x => !AssetDatabase.IsMainAsset(x) && x is not TimelineAssetVariantData);

            Logger.Log(
                nameof(MapSourceToTargetObjects),
                $"Copying {sourceObjects.Count()} assets from {assetPath}"
            );

            foreach (var sourceObject in sourceObjects)
            {
                int index = ObjectMap.FindIndex(x => x.Source == sourceObject);
                var mapping = index >= 0 ? ObjectMap[index] : new ObjectMapping();
                mapping.Source = sourceObject;

                var targetObject = mapping.Target;
                if (targetObject == null && !RemovedObjects.Contains(sourceObject))
                {
                    targetObject = Instantiate(sourceObject);
                    targetObject.hideFlags = sourceObject.hideFlags;
                    AssetDatabase.AddObjectToAsset(targetObject, target);
                }
                mapping.Target = targetObject;

                if (index >= 0)
                {
                    ObjectMap[index] = mapping;
                }
                else
                {
                    ObjectMap.Add(mapping);
                }
            }
        }

        private void RemoveInvalidTargetObjects(Object target)
        {
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            string assetPath = AssetDatabase.GetAssetPath(target);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(x => !AssetDatabase.IsMainAsset(x) && x is not TimelineAssetVariantData);

            foreach (var obj in objects)
            {
                if (!AddedObjects.Contains(obj) && !ObjectMap.Any(x => x.Target == obj))
                {
                    AssetDatabase.RemoveObjectFromAsset(obj);
                    DestroyImmediate(obj);
                }
            }
        }

        private void CopySourcePropertiesToTarget()
        {
            foreach (var mapping in ObjectMap)
            {
                if (mapping.Source == null || mapping.Target == null)
                {
                    continue;
                }

                using var serializedSource = new SerializedObject(mapping.Source);
                using var serializedTarget = new SerializedObject(mapping.Target);

                Logger.Log(nameof(CopySourcePropertiesToTarget), $"{mapping.Source} --> {mapping.Target}");

                var iterator = serializedSource.GetIterator();
                iterator.Next(true);

                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    if (Array.Exists(SkippedPropertyNames, x => x == iterator.name))
                    {
                        enterChildren = false;
                        continue;
                    }

                    enterChildren = CanEnterChildProperties(iterator);

                    var sourceProp = iterator.Copy();
                    var targetProp = serializedTarget.FindProperty(sourceProp.propertyPath);
                    if (targetProp == null)
                    {
                        continue;
                    }

                    switch (sourceProp.propertyType)
                    {
                        case SerializedPropertyType.Generic:
                            // Skip Generic properties...
                            break;

                        case SerializedPropertyType.String:
                            targetProp.stringValue = sourceProp.stringValue;
                            Logger.Log(
                                nameof(CopySourcePropertiesToTarget),
                                $"-- {sourceProp.propertyPath} ({sourceProp.propertyType}): {targetProp.stringValue}");
                            break;

                        case SerializedPropertyType.ObjectReference:
                            targetProp.objectReferenceValue = ObjectMap.GetCorrespondingObject(sourceProp.objectReferenceValue);
                            Logger.Log(
                                nameof(CopySourcePropertiesToTarget),
                                $"-- {sourceProp.propertyPath} ({sourceProp.propertyType}): {targetProp.objectReferenceValue}"
                                );
                            break;

                        case SerializedPropertyType.ArraySize:
                            targetProp.intValue = sourceProp.intValue;
                            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                            Logger.Log(
                                nameof(CopySourcePropertiesToTarget),
                                $"-- {sourceProp.propertyPath} ({sourceProp.propertyType}): {targetProp.intValue}"
                                );
                            break;

                        default:
                            targetProp.boxedValue = sourceProp.boxedValue;
                            Logger.Log(
                                nameof(CopySourcePropertiesToTarget),
                                $"-- {sourceProp.propertyPath} ({sourceProp.propertyType}): {targetProp.boxedValue}"
                                );
                            break;
                    }
                }

                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private void ApplyPropertyOverrides(Object target)
        {
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            string assetPath = AssetDatabase.GetAssetPath(target);
            var targetObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (var propertyOverride in PropertyOverrides)
            {
                var targetObject = ObjectMap.GetCorrespondingObject(propertyOverride.Source);
                if (targetObject == null)
                {
                    Logger.LogError(nameof(ApplyPropertyOverrides), "target == null for source " + propertyOverride.Source);
                    continue;
                }

                if (!targetObjects.Any(x => x == targetObject))
                {
                    Logger.LogError(
                        nameof(ApplyPropertyOverrides),
                        $"targetObject '{targetObject}' not found at target asset path '{assetPath}'", targetObject);
                    continue;
                }

                using var serializedObject = new SerializedObject(targetObject);
                var property = serializedObject.FindProperty(propertyOverride.PropertyPath);
                if (property == null)
                {
                    Logger.LogError(
                        nameof(ApplyPropertyOverrides),
                        $"property == null; targetObject = {targetObject}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                    continue;
                }

                object value = property.propertyType switch
                {
                    SerializedPropertyType.ObjectReference => ObjectMap.GetCorrespondingObject(propertyOverride.ObjectReference),
                    _ => JsonConvert.DeserializeObject(propertyOverride.Value)
                };

                property.boxedValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                if (value != null)
                {
                    Logger.Log(
                        nameof(ApplyPropertyOverrides),
                        $"Set value = {value} ({value.GetType()}); targetObject = {targetObject}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                }
                else
                {
                    Logger.Log(
                        nameof(ApplyPropertyOverrides),
                        $"Set value = null; targetObject = {targetObject}; propertyPath = {propertyOverride.PropertyPath}"
                        );
                }
            }
        }

        private void FindAddedObjects(Object target)
        {
            Assert.IsNotNull(target, "'target' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(target), "'target' is not a project asset");

            string assetPath = AssetDatabase.GetAssetPath(target);
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(x => !AssetDatabase.IsMainAsset(x) && x is not TimelineAssetVariantData);
            var added = new List<Object>(assets.Count());
            foreach (var obj in assets)
            {
                var mapping = ObjectMap.FirstOrDefault(x => x.Target == obj);
                if (mapping.Source == null)
                {
                    Logger.Log(nameof(FindAddedObjects), "Add " + obj);
                    added.Add(obj);
                }
            }

            AddedObjects = added;
        }

        private void FindRemovedObjects()
        {
            Assert.IsNotNull(Source, "'Source' is null");
            Assert.IsTrue(EditorUtility.IsPersistent(Source), "'Source' is not a project asset");

            string assetPath = AssetDatabase.GetAssetPath(Source);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(x => !AssetDatabase.IsMainAsset(x) && x is not TimelineAssetVariantData);
            var removed = new List<Object>(objects.Count());
            foreach (var obj in objects)
            {
                var mapping = ObjectMap.FirstOrDefault(x => x.Source == obj);
                if (mapping.Target == null)
                {
                    Logger.Log(nameof(FindRemovedObjects), "Removed " + obj);
                    removed.Add(obj);
                }
            }

            RemovedObjects = removed;
        }

        private void FindPropertyOverrides()
        {
            int count = 0;
            PropertyOverrides.Clear();

            foreach (var mapping in ObjectMap)
            {
                if (mapping.Source == null || mapping.Target == null)
                {
                    continue;
                }

                using var serializedSource = new SerializedObject(mapping.Source);
                using var serializedTarget = new SerializedObject(mapping.Target);

                var iterator = serializedTarget.GetIterator();
                iterator.Next(true);

                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    if (Array.Exists(SkippedPropertyNames, x => x == iterator.name))
                    {
                        enterChildren = false;
                        continue;
                    }

                    enterChildren = CanEnterChildProperties(iterator);

                    var targetProp = iterator.Copy();
                    var sourceProp = serializedSource.FindProperty(targetProp.propertyPath);

                    switch (targetProp.propertyType)
                    {
                        case SerializedPropertyType.Generic:
                            // Skip generic properties...
                            break;

                        case SerializedPropertyType.ManagedReference:
                            Logger.LogWarning(nameof(FindPropertyOverrides), $"-- Skipping {targetProp.propertyType} at path {targetProp.propertyPath}");
                            break;

                        case SerializedPropertyType.String:
                            if (sourceProp == null || targetProp.stringValue != sourceProp.stringValue)
                            {
                                PropertyOverrides.Add(new(mapping.Source, targetProp));
                            }
                            break;

                        case SerializedPropertyType.ObjectReference:
                            var targetObj = targetProp.objectReferenceValue;
                            var correspondingObj = ObjectMap.GetCorrespondingObject(targetObj);
                            var sourceObj = sourceProp?.objectReferenceValue;
                            if (sourceProp == null || correspondingObj != sourceObj)
                            {
                                PropertyOverrides.Add(new(mapping.Source, targetProp.propertyPath, correspondingObj));
                            }
                            break;

                        default:
                            if (!targetProp.hasChildren && (sourceProp == null || !targetProp.boxedValue.Equals(sourceProp.boxedValue)))
                            {
                                PropertyOverrides.Add(new(mapping.Source, targetProp));
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

        private static bool CanEnterChildProperties(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.String => false,
                SerializedPropertyType.ObjectReference => false,
                _ => true
            };
        }
    }
#endif
}

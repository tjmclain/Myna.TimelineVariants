using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Timeline;
#endif

namespace Myna.Assets
{
    public partial class TimelineAssetVariant : TimelineAsset { }

#if UNITY_EDITOR
    partial class TimelineAssetVariant
    {
#if MYNA_CONFIGURABLE_LOGGER
        internal static readonly ILogger Logger = new DebugUtilities.ConfigurableLogger(nameof(TimelineAssetVariant));
#else
        internal static readonly ILogger Logger = new Logger(Debug.unityLogger);
#endif

        public void ApplyOverrides()
        {
            Logger.Log(nameof(ApplyOverrides), this, this);

            bool wasOpenInEditor = TimelineEditor.inspectedAsset == this;
            if (wasOpenInEditor)
            {
                var window = TimelineEditor.GetOrCreateWindow();
                window.SaveChanges();
                window.ClearTimeline();
            }

            var data = GetData();

            AssetDatabase.SaveAssetIfDirty(data.Source);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();

            data.ApplyOverrides(this);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();

            if (wasOpenInEditor)
            {
                var window = TimelineEditor.GetOrCreateWindow();
                window.SetTimeline(this);
            }
        }

        public void RecordOverrides()
        {
            Logger.Log(nameof(RecordOverrides), this, this);

            bool wasOpenInEditor = TimelineEditor.inspectedAsset == this;
            if (wasOpenInEditor)
            {
                var window = TimelineEditor.GetOrCreateWindow();
                window.SaveChanges();
                window.ClearTimeline();
            }

            var data = GetData();

            AssetDatabase.SaveAssetIfDirty(data.Source);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();

            data.RecordOverrides(this);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();

            if (wasOpenInEditor)
            {
                var window = TimelineEditor.GetOrCreateWindow();
                window.SetTimeline(this);
            }
        }

        public TimelineAssetVariantData GetData()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            var data = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<TimelineAssetVariantData>().FirstOrDefault();
            Assert.IsNotNull(data, "VariantTimelineAssetData is null at path " + assetPath);
            Assert.IsNotNull(data.Source, "Source is null at path " + assetPath);
            return data;
        }

        public static void CreateTimelineAssetVariant(TimelineAsset source)
        {
            if (source == null)
            {
                Logger.LogError(nameof(CreateTimelineAssetVariant), "'source' is null");
                return;
            }

            var variant = CreateInstance<TimelineAssetVariant>();
            variant.name = source.name + " Variant";

            string assetPath = AssetDatabase.GetAssetPath(source);
            assetPath = $"{Path.GetDirectoryName(assetPath)}/{variant.name}.playable";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(variant, assetPath);

            var data = CreateInstance<TimelineAssetVariantData>();
            data.name = nameof(TimelineAssetVariantData);
            data.hideFlags = HideFlags.HideInHierarchy;
            data.Source = source;
            AssetDatabase.AddObjectToAsset(data, assetPath);

            variant = AssetDatabase.LoadAssetAtPath<TimelineAssetVariant>(assetPath);
            variant.ApplyOverrides();
        }

        [MenuItem("Assets/Create/Timeline/Timeline Variant")]
        private static void CreateTimelineAssetVariant()
        {
            if (Selection.activeObject is not TimelineAsset timeline)
            {
                Logger.LogError(nameof(CreateTimelineAssetVariant), "Selection.activeObject is not TimelineAsset");
                return;
            }

            CreateTimelineAssetVariant(timeline);
        }

        [MenuItem("Assets/Create/Timeline/Timeline Variant", true)]
        private static bool CanCreateVariantTimelineAsset()
        {
            return Selection.activeObject is TimelineAsset;
        }
    }

    [CustomEditor(typeof(TimelineAssetVariant))]
    public class VariantTimelineAssetEditor : Editor
    {
        private static readonly string AllowEditsPrefsKey = $"{nameof(VariantTimelineAssetEditor)}.{nameof(AllowEdits)}";
        private static bool AllowEdits
        {
            get => SessionState.GetBool(AllowEditsPrefsKey, false);
            set => SessionState.SetBool(AllowEditsPrefsKey, value);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var target = (TimelineAssetVariant)this.target;
            DrawAssetData(target);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            TimelineAssetVariant.Logger.filterLogType = (LogType)EditorGUILayout.EnumPopup("Log Level", TimelineAssetVariant.Logger.filterLogType);

            GUI.color = AllowEdits ? Color.yellow : Color.white;
            AllowEdits = EditorGUILayout.ToggleLeft("Allow Manual Edits to Overrides", AllowEdits);
            GUI.color = Color.white;

            if (FriendlyButton(nameof(TimelineAssetVariant.ApplyOverrides)))
            {
                target.ApplyOverrides();
            }

            if (FriendlyButton(nameof(TimelineAssetVariant.RecordOverrides)))
            {
                target.RecordOverrides();
            }

            
            if (FriendlyButton(nameof(TimelineAssetVariantData.ResetOverrides)))
            {
                var data = target.GetData();
                data.ResetOverrides();
            }
        }

        private static void DrawAssetData(TimelineAssetVariant target)
        {
            using (new EditorGUI.DisabledScope(!AllowEdits))
            {
                var data = target.GetData();
                using var serializedObject = new SerializedObject(data);
                var iterator = serializedObject.GetIterator();
                iterator.NextVisible(true);

                while (iterator.NextVisible(false))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        private static bool FriendlyButton(string methodName, params GUILayoutOption[] options)
        {
            string text = ObjectNames.NicifyVariableName(methodName);
            return GUILayout.Button(text, options);
        }
    }
#endif
}

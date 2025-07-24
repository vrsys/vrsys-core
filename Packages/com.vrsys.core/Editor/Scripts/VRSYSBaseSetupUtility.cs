using UnityEditor;
using VRSYS.Core.Logging;

namespace VRSYS.Core.Editor
{
    public static class VRSYSBaseSetupUtility
    {
        #region Variables

        private static string _logTag = "VRSYSBaseSetupUtility";

        #endregion

        #region MenuItems

        [MenuItem("VRSYS/Core/Setup project")]
        public static void SetupProject(MenuCommand menuCommand)
        {
            // Create CameraIgnore layer (Idx: 3)
            CreateLayer(3, "CameraIgnore");

            // Create Interactable layer (Idx: 6)
            CreateLayer(6, "Interactable");
        }

        #endregion

        #region Private Methdos

        private static void CreateLayer(int layerIdx, string layerName)
        {
            // Open tag manager
            SerializedObject tagManager =
                new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            
            // Layer property
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            if (!PropertyExists(layersProp, layerIdx, layerName))
            {
                SerializedProperty newLayer = layersProp.GetArrayElementAtIndex(layerIdx);
                newLayer.stringValue = layerName;
                
                ExtendedLogger.LogInfo(_logTag, $"Layer {layerName} successfully created as layer {layerIdx}.");

                tagManager.ApplyModifiedProperties();
            }
            else
            {
                ExtendedLogger.LogError(_logTag, $"Layer {layerName} could not be created, since layer {layerIdx} is already used.");
            }
        }

        private static bool PropertyExists(SerializedProperty property, int idx, string value)
        {
            SerializedProperty p = property.GetArrayElementAtIndex(idx);

            return p.stringValue.Equals(value);
        }

        #endregion
    }
}

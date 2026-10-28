using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRSYS.Recording
{
#if UNITY_EDITOR
    /// <summary>
    /// The "Networked Recording Test" sample scene is based on VRSYS Core's "Sample Scene Setups" sample and uses
    /// its network menu, keyboard, network prefab list and lobby settings. When that scene is opened without the
    /// core sample imported, this offers to import it (samples keep their asset GUIDs, so the references resolve).
    /// </summary>
    [InitializeOnLoad]
    internal static class RecordingSampleDependencies
    {
        private const string NetworkedSampleScenePrefix = "VRSYS-Recording-NetworkedTest";
        private const string CorePackage = "com.vrsys.core";
        private const string CoreSampleName = "Sample Scene Setups";
        // GUID of "VRSYS Samples-NetworkPrefabsList.asset" in the core sample, used as the "is it imported" marker.
        private const string CoreSampleMarkerGuid = "76105f6ee29606d4484245a2757e5ebf";

        static RecordingSampleDependencies()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!scene.name.StartsWith(NetworkedSampleScenePrefix) || IsCoreSampleImported())
                return;

            bool import = EditorUtility.DisplayDialog("VRSYS Recording sample",
                "This scene uses the network menu, keyboard, network prefab list and lobby settings from the " +
                "VRSYS Core sample \"" + CoreSampleName + "\", which is not imported in this project.\n\n" +
                "Import it now?", "Import", "Not now");
            if (import && ImportCoreSample())
                EditorSceneManager.OpenScene(scene.path, mode); // reload so the references resolve
        }

        private static bool IsCoreSampleImported()
        {
            return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(CoreSampleMarkerGuid));
        }

        [MenuItem("VRSYS/Recording/Import VRSYS Core \"Sample Scene Setups\"")]
        private static void ImportFromMenu()
        {
            if (IsCoreSampleImported())
                EditorUtility.DisplayDialog("VRSYS Recording sample", "The VRSYS Core sample \"" + CoreSampleName + "\" is already imported.", "OK");
            else
                ImportCoreSample();
        }

        private static bool ImportCoreSample()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(CorePackage);
            if (package == null)
            {
                RecordingLogger.LogError(nameof(RecordingSampleDependencies), "VRSYS Recording: package " + CorePackage + " not found.");
                return false;
            }

            Sample sample = Sample.FindByPackage(CorePackage, package.version)
                .FirstOrDefault(s => s.displayName == CoreSampleName);
            if (string.IsNullOrEmpty(sample.resolvedPath))
            {
                RecordingLogger.LogError(nameof(RecordingSampleDependencies), "VRSYS Recording: sample \"" + CoreSampleName + "\" not found in " + CorePackage + ".");
                return false;
            }

            bool imported = sample.Import(Sample.ImportOptions.HideImportWindow);
            AssetDatabase.Refresh();
            return imported;
        }
    }
#endif
}

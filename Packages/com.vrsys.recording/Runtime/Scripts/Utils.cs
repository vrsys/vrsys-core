using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using VRSYS.Core.Avatar;
using VRSYS.Core.Logging;
using VRSYS.Core.Navigation;
using VRSYS.Core.Networking;
using VRSYS.Core.Utility;
using VRSYS.Recording.Scripts;
using VRSYS.Scripts.Recording;
using Random = System.Random;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace Vrsys.Scripts.Recording
{
    public enum BlendMode
    {
        Opaque,
        Cutout,
        Fade, // Old school alpha-blending mode, fresnel does not affect amount of transparency
        Transparent // Physically plausible transparency mode, implemented as alpha pre-multiply
    }

    public class GenericUtils
    {
        public static T ToEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }

    public class Utils
    {
        private static Dictionary<int, Material> _UUIDMaterial = new Dictionary<int, Material>();
        private static Dictionary<int, Color> _UUIDMaterialColor = new Dictionary<int, Color>();
        
        private static Dictionary<Tuple<int, string>, GameObject> _UUIDRootChild =
            new Dictionary<Tuple<int, string>, GameObject>();

        private static Dictionary<string, GameObject> _substringGo = new Dictionary<string, GameObject>();

        // see: https://stackoverflow.com/a/36845864
        public static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for(int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i+1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i+1];
                }

                return hash1 + (hash2*1566083941);
            }
        }
        
        // see: https://stackoverflow.com/questions/7470997/replace-german-characters-umlauts-accents-with-english-equivalents
        public static String RemoveDiacritics(String s)
        {
            String normalizedString = s.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < normalizedString.Length; i++)
            {
                Char c = normalizedString[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    stringBuilder.Append(c);
            }

            return stringBuilder.ToString();
        }
        
        public static void DisableUserInput(bool isHmd, GameObject localViewingSetup)
        {
            if (!isHmd)
            {
                DesktopNavigation navigation = null;
                if (localViewingSetup != null)
                    navigation = localViewingSetup.GetComponentInChildren<DesktopNavigation>();
                if (navigation != null)
                    navigation.enabled = false;
            }
            else
            {
                UnityEngine.SpatialTracking.TrackedPoseDriver[] drivers =
                    localViewingSetup.GetComponentsInChildren<UnityEngine.SpatialTracking.TrackedPoseDriver>(true);
                foreach (var driver in drivers)
                {
                    driver.enabled = false;
                }
            }
        }

        public static void EnableUserInput(bool isHmd, GameObject localViewingSetup)
        {
            if (!isHmd)
            {
                DesktopNavigation navigation = localViewingSetup.GetComponent<DesktopNavigation>();
                if (navigation != null)
                    navigation.enabled = true;
            }
            else
            {
                UnityEngine.SpatialTracking.TrackedPoseDriver[] drivers =
                    localViewingSetup.GetComponentsInChildren<UnityEngine.SpatialTracking.TrackedPoseDriver>(true);
                foreach (var driver in drivers)
                {
                    driver.enabled = true;
                }
            }
        }

        public static Vector3 IntersectionRayPlane(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planeOrigin,
            Vector3 planeNormal)
        {
            planeNormal = Vector3.Normalize(planeNormal);
            rayDirection = Vector3.Normalize(rayDirection);

            float s = Vector3.Dot(planeOrigin - rayOrigin, planeNormal) / Vector3.Dot(rayDirection, planeNormal);
            return rayOrigin + s * rayDirection;
        }

        public static GameObject IsPartOfUser(GameObject potentialChild)
        {
            GameObject parent = potentialChild;
            while (parent != null)
            {
                if ((parent.name.Contains("[Local User]") || parent.name.Contains("[External User]")) &&
                    !parent.name.Contains("[Rec]"))
                {
                    return parent;
                }

                parent = parent.transform.parent != null ? parent.transform.parent.gameObject : null;
            }

            return null;
        }

        public static GameObject RayCastSelection(Vector3 origin, Vector3 direction)
        {
            RaycastHit hit;

            if (Physics.Raycast(origin, direction, out hit, 100, Physics.AllLayers))
            {
                return hit.collider.gameObject;
            }

            return null;
        }

        public static GameObject RayCastAllSelection(Vector3 origin, Vector3 direction)
        {
            RaycastHit[] hits;
            RaycastHit closestHit = new RaycastHit();
            float curDist = -1.0f;

            hits = Physics.RaycastAll(origin, direction, 100, Physics.AllLayers);
            for (int i = 0; i < hits.Length; ++i)
            {
                if (curDist < 0.0f)
                    closestHit = hits[i];
                else if (hits[i].distance < curDist)
                {
                    closestHit = hits[i];
                    curDist = closestHit.distance;
                }
            }

            return closestHit.transform != null ? closestHit.transform.gameObject : null;
        }

        public static void ActivateXRInteractor(GameObject go)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual interactorLineVisual = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
            if (interactorLineVisual != null)
                interactorLineVisual.enabled = true;

            LineRenderer lineRenderer = go.GetComponent<LineRenderer>();
            if (lineRenderer != null)
                lineRenderer.enabled = true;
        }

        public static void DeactivateXRInteractor(GameObject go)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual interactorLineVisual = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
            if (interactorLineVisual != null)
                interactorLineVisual.enabled = false;

            LineRenderer lineRenderer = go.GetComponent<LineRenderer>();
            if (lineRenderer != null)
                lineRenderer.enabled = false;
        }

        public static void ActivateAllChildren(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                ActivateAllChildren(child.gameObject);
            }

            root.SetActive(true);
        }

        public static void DeactivateAllChildren(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                DeactivateAllChildren(child.gameObject);
            }

            root.SetActive(false);
        }

        // see: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/StandardShaderGUI.cs
        private static void SetupMaterialWithBlendMode(Material material, BlendMode blendMode, bool overrideRenderQueue)
        {
            int minRenderQueue = -1;
            int maxRenderQueue = 5000;
            int defaultRenderQueue = -1;
            switch (blendMode)
            {
                case BlendMode.Opaque:
                    material.SetOverrideTag("RenderType", "");
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1.0f);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    minRenderQueue = -1;
                    maxRenderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest - 1;
                    defaultRenderQueue = -1;
                    break;
                case BlendMode.Cutout:
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1.0f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    minRenderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    maxRenderQueue = (int)UnityEngine.Rendering.RenderQueue.GeometryLast;
                    defaultRenderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case BlendMode.Fade:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0.0f);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    minRenderQueue = (int)UnityEngine.Rendering.RenderQueue.GeometryLast + 1;
                    maxRenderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay - 1;
                    defaultRenderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                case BlendMode.Transparent:
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0.0f);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    minRenderQueue = (int)UnityEngine.Rendering.RenderQueue.GeometryLast + 1;
                    maxRenderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay - 1;
                    defaultRenderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }

            if (overrideRenderQueue || material.renderQueue < minRenderQueue || material.renderQueue > maxRenderQueue)
            {
                if (!overrideRenderQueue)
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                        "Render queue value outside of the allowed range ({0} - {1}) for selected Blend mode, resetting render queue to default",
                        minRenderQueue, maxRenderQueue);
                material.renderQueue = defaultRenderQueue;
            }
        }

        public static void MakeAllChildrenTransparent(GameObject root, float alpha)
        {
            if (root == null)
                return;

            foreach (Transform child in root.transform)
            {
                MakeAllChildrenTransparent(child.gameObject, alpha);
            }

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Material transMat = new Material(renderer.material);
                if (transMat != null && renderer.material.HasProperty("_Color"))
                {
                    if (_UUIDMaterial != null && !_UUIDMaterial.ContainsKey(root.GetInstanceID()))
                    {
                        _UUIDMaterial[root.GetInstanceID()] = renderer.material;
                    }

                    SetupMaterialWithBlendMode(transMat, BlendMode.Fade, true);
                    renderer.material = transMat;

                    if (transMat.HasProperty("_Color"))
                    {
                        Color color = transMat.color;
                        renderer.material.color = new Color(color.r, color.g, color.b, alpha);
                    }
                }
            }
        }

        public static void SetLocalScale(Transform transform, Vector3 lossyScale)
        {
            Vector3 newLocalScale = lossyScale;
            if (transform.parent != null)
            {
                Vector3 scaleFactor = transform.parent.lossyScale;
                newLocalScale = new Vector3(
                    lossyScale.x / scaleFactor.x,
                    lossyScale.y / scaleFactor.y,
                    lossyScale.z / scaleFactor.z
                );
            }

            transform.transform.localScale = newLocalScale;
        }

        public static void MarkPortalRecorder(GameObject root)
        {
            foreach (Transform child in root.transform)
                MarkPortalRecorder(child.gameObject);

            Recorder recorder = root.GetComponent<Recorder>();
            if (recorder != null)
                recorder.MarkAsPortalRecorder();
        }

        public static void MarkPreviewRecorder(GameObject root)
        {
            foreach (Transform child in root.transform)
                MarkPreviewRecorder(child.gameObject);

            Recorder recorder = root.GetComponent<Recorder>();
            if (recorder != null)
                recorder.MarkAsPreviewRecorder();
        }

        public static void MakeAllChildrenNonTransparent(GameObject root)
        {
            foreach (Transform child in root.transform)
                MakeAllChildrenNonTransparent(child.gameObject);

            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null && _UUIDMaterial != null)
            {
                if (_UUIDMaterial.ContainsKey(root.GetInstanceID()))
                    renderer.material = _UUIDMaterial[root.GetInstanceID()];
                else
                {
                    Material opaqueMat = new Material(renderer.material);
                    SetupMaterialWithBlendMode(opaqueMat, BlendMode.Opaque, true);
                    opaqueMat.name = opaqueMat.name.Replace("(Instance)", "") + "[Opaque]";
                    renderer.material = opaqueMat;
                    if (opaqueMat.HasProperty("_Color"))
                    {
                        Color color = opaqueMat.color;
                        renderer.material.color = new Color(color.r, color.g, color.b, 1.0f);
                    }
                }
            }

            Outline outline = root.GetComponent<Outline>();
            if (outline != null)
                GameObject.Destroy(outline);
        }

        public static GameObject GetChildByName(GameObject root, string name)
        {
            Tuple<int, string> key = new Tuple<int, string>(root.GetInstanceID(), name);
            if (_UUIDRootChild.ContainsKey(key))
                return _UUIDRootChild[key];

            if (root != null)
            {
                foreach (Transform child in root.transform)
                {
                    if (child.gameObject.name == name)
                    {
                        _UUIDRootChild[key] = child.gameObject;
                        return _UUIDRootChild[key];
                    }
                    else
                    {
                        GameObject recurseGameObject = GetChildByName(child.gameObject, name);
                        if (recurseGameObject != null)
                        {
                            _UUIDRootChild[key] = recurseGameObject;
                            return _UUIDRootChild[key];
                        }
                    }
                }
            }

            return null;
        }

        public static string GetObjectName(GameObject gameObject)
        {
            return GetObjectName(gameObject, null);
        }

        public static string GetObjectName(GameObject gameObject, Transform replayRoot)
        {
            GameObject currentGameObject = gameObject;
            string name = "";

            if (currentGameObject.name.Contains("[Rec]"))
                name = currentGameObject.name.Replace("[Rec]", "");
            else
                name = currentGameObject.name;

            Transform parent = currentGameObject.transform.parent;

            while (parent != null)
            {
                if (parent.name.Contains("[Rec]"))
                    name = parent.name.Replace("[Rec]", "") + "/" + name;
                else
                    name = parent.name + "/" + name;

                parent = parent.parent;
            }

            // When replaying under a configured replay root, the playback objects live beneath that
            // root (either pre-existing duplicates or prefabs instantiated under it by ScenePreparator).
            // Their derived hierarchy name therefore gains the root's full transform path as a prefix.
            // Strip it so the names match the recorded, anchor-relative names. This is a no-op for
            // objects that are not actually beneath the root.
            if (replayRoot != null)
            {
                string replayRootPrefix = GetTransformPath(replayRoot);
                if (!string.IsNullOrEmpty(replayRootPrefix))
                    name = Regex.Replace(name, "^" + Regex.Escape(replayRootPrefix), "");
            }

            return "/" + name;
        }

        /// <summary>
        /// Builds the full hierarchy path of a transform as "root/.../target/" (no leading slash,
        /// trailing slash), matching the prefix format used inside <see cref="GetObjectName"/>.
        /// </summary>
        public static string GetTransformPath(Transform targetTransform)
        {
            if (targetTransform == null)
                return "";

            string name = targetTransform.name.Contains("[Rec]")
                ? targetTransform.name.Replace("[Rec]", "")
                : targetTransform.name;

            string path = name + "/";
            if (targetTransform.parent != null)
                path = GetTransformPath(targetTransform.parent) + path;
            return path;
        }

        public static GameObject GetGameObjectByHierarchyName(GameObject root, string hierarchyGameObjectName)
        {
            string[] names = hierarchyGameObjectName.Split(new[] { '/' }, 2);
            string cleanName = root.name.Replace("[Rec]", "");
            if (cleanName == names[0])
            {
                if (names.Length > 1)
                {
                    foreach (Transform child in root.transform)
                    {
                        GameObject go = GetGameObjectByHierarchyName(child.gameObject, names[1]);
                        if (go != null)
                            return go;
                    }
                }
                else
                    return root;
            }

            return null;
        }

        public static GameObject GetObjectByHierarchyName(string hierarchyGameObjectName)
        {
            if (hierarchyGameObjectName.StartsWith('/'))
                hierarchyGameObjectName = hierarchyGameObjectName.Substring(hierarchyGameObjectName.IndexOf('/') + 1);
            
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            // clone scene for preview purpose
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i] != null)
                {
                    GameObject go = GetGameObjectByHierarchyName(rootObjects[i], hierarchyGameObjectName);
                    if (go != null)
                        return go;
                }
            }

            rootObjects = DontDestroySceneAccessor.Instance.GetAllRootsOfDontDestroyOnLoad();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i] != null)
                {
                    GameObject go = GetGameObjectByHierarchyName(rootObjects[i], hierarchyGameObjectName);
                    if (go != null)
                        return go;
                }
            }
            
            return null;
        }

        public static void ActivateChildren(GameObject gameObject)
        {
            foreach (Transform child in gameObject.transform)
                child.gameObject.SetActive(true);
        }

        public static void DeactivateChildren(GameObject gameObject)
        {
            foreach (Transform child in gameObject.transform)
                child.gameObject.SetActive(false);
        }

        public static void DestroyChildren(GameObject gameObject)
        {
            foreach (Transform child in gameObject.transform)
                DestroyGameObjectAndChildren(child.gameObject);
        }

        public static void DestroyNetworkAndCameraComponentsRecursive(GameObject root)
        {
            if (root.GetComponent<RecorderController>() != null)
            {
                GameObject.Destroy(root);
                return;
            }

            NetworkController networkController = root.GetComponent<NetworkController>();
            if (networkController != null)
                GameObject.Destroy(networkController);

            NetworkUser networkUser = root.GetComponent<NetworkUser>();
            if (networkUser != null)
                GameObject.Destroy(networkUser);

            Camera camera = root.GetComponent<Camera>();
            if (camera != null)
                GameObject.Destroy(camera);

            foreach (Transform child in root.transform)
                DestroyNetworkAndCameraComponentsRecursive(child.gameObject);
        }

        public static void RecursivelySetLayer(GameObject root, int layer)
        {
            foreach (Transform child in root.transform)
                RecursivelySetLayer(child.gameObject, layer);

            root.layer = layer;
        }

        public static void DestroyGameObjectAndChildren(GameObject gameObject)
        {
            foreach (Transform child in gameObject.transform)
                DestroyChildren(child.gameObject);

            GameObject.Destroy(gameObject);
        }

        public static GameObject GetGameObjectBySubstring(string substring)
        {
            if (_substringGo.ContainsKey(substring))
                return _substringGo[substring];

            GameObject result = null;
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootObject in rootObjects)
            {
                if (rootObject.name.Contains(substring))
                {
                    _substringGo[substring] = rootObject;
                    return rootObject;
                }

                if (!rootObject.name.Contains("__RECORDING__"))
                    result = GetChildBySubstring(rootObject, substring);
                if (result != null)
                {
                    _substringGo[substring] = result;
                    return result;
                }
            }

            if (DontDestroySceneAccessor.Instance != null)
            {
                rootObjects = DontDestroySceneAccessor.Instance.GetAllRootsOfDontDestroyOnLoad();
                foreach (var rootObject in rootObjects)
                {
                    if (rootObject.name.Contains(substring))
                    {
                        _substringGo[substring] = rootObject;
                        return rootObject;
                    }

                    if (!rootObject.name.Contains("__RECORDING__"))
                        result = GetChildBySubstring(rootObject, substring);
                    if (result != null)
                    {
                        _substringGo[substring] = result;
                        return result;
                    }
                }
            }


            return result;
        }

        public static GameObject GetChildBySubstring(GameObject root, string substring)
        {
            if (root == null)
                return null;

            Tuple<int, string> key = new Tuple<int, string>(root.GetInstanceID(), substring);
            if (_UUIDRootChild.ContainsKey(key))
                return _UUIDRootChild[key];

            foreach (Transform child in root.transform)
            {
                if (child.gameObject.name.Contains(substring))
                {
                    _UUIDRootChild[key] = child.gameObject;
                    return _UUIDRootChild[key];
                }

                GameObject recurseGameObject = GetChildBySubstring(child.gameObject, substring);
                if (recurseGameObject != null)
                    return recurseGameObject;
            }

            return null;
        }

        public static GameObject GetOrCreateHierarchy(string[] hierarchy, int index, GameObject parent)
        {
            List<GameObject> childObjects = new List<GameObject>();
            if (index == 0)
            {
                foreach (GameObject child in UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                             .GetRootGameObjects())
                    childObjects.Add(child);
            }
            else
            {
                foreach (Transform childTransform in parent.transform)
                    childObjects.Add(childTransform.gameObject);
            }


            foreach (var child in childObjects)
            {
                if (child.name == hierarchy[index])
                {
                    if (index + 1 == hierarchy.Length)
                        return child;
                    else
                        return GetOrCreateHierarchy(hierarchy, index + 1, child);
                }
            }

            GameObject ob = new GameObject(hierarchy[index]);
            if (parent != null)
                ob.transform.SetParent(parent.transform);

            if (index + 1 == hierarchy.Length)
                return ob;
            else
                return GetOrCreateHierarchy(hierarchy, index + 1, ob);
        }

        public static GameObject GetChildCamera(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                if (child.gameObject.GetComponent<Camera>() != null)
                {
                    return child.gameObject;
                }
                else
                {
                    GameObject recurseGameObject = GetChildCamera(child.gameObject);
                    if (recurseGameObject != null)
                    {
                        return recurseGameObject;
                    }
                }
            }

            return null;
        }

        public static void MakeChildrenVisible(GameObject root)
        {
            foreach (Transform child in root.transform)
                MakeChildrenVisible(child.gameObject);
            MakeVisible(root);
        }

        public static void MakeChildrenInvisible(GameObject root)
        {
            foreach (Transform child in root.transform)
                MakeChildrenInvisible(child.gameObject);
            MakeInvisible(root);
        }

        public static void SetColor(GameObject go, Color color)
        {
            foreach (Transform child in go.transform)
            {
                SetColor(child.gameObject, color);
            }
            
            if (go.GetComponent<Renderer>() != null)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if(!_UUIDMaterialColor.ContainsKey(go.GetInstanceID()))
                    _UUIDMaterialColor[go.GetInstanceID()] = renderer.material.color;
                renderer.material.color = color;
            }
        }
        
        public static void ResetColor(GameObject go)
        {
            foreach (Transform child in go.transform)
            {
                ResetColor(child.gameObject);
            }
            
            if (go.GetComponent<Renderer>() != null)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if (_UUIDMaterialColor.ContainsKey(go.GetInstanceID()))
                    renderer.material.color = _UUIDMaterialColor[go.GetInstanceID()];
            }
        }
        
        public static void MakeInvisible(GameObject go)
        {
            if (go.GetComponent<MeshRenderer>() != null)
            {
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.enabled = false;
            }
        }

        public static void DisableLight(GameObject go)
        {
            foreach (Transform child in go.transform)
                DisableLight(child.gameObject);

            if (go.GetComponent<Light>() != null)
                go.GetComponent<Light>().enabled = false;
        }

        public static void EnableLight(GameObject go)
        {
            foreach (Transform child in go.transform)
                EnableLight(child.gameObject);

            if (go.GetComponent<Light>() != null)
                go.GetComponent<Light>().enabled = true;
        }

        public static void MakeVisible(GameObject go)
        {
            if (go.GetComponent<MeshRenderer>() != null)
            {
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.enabled = true;
            }
        }

        public static void CopyLocalTransformations(Transform originRoot, Transform targetRoot)
        {
            foreach (Transform originChild in originRoot.transform)
            {
                foreach (Transform targetChild in targetRoot.transform)
                {
                    if (originChild.name == targetChild.name)
                    {
                        CopyLocalTransformations(originChild, targetChild);
                        break;
                    }
                }
            }

            //Debug.Log("Target: " + GetObjectName(targetRoot.gameObject) + ", Origin: " + GetObjectName(originRoot.gameObject));
            targetRoot.localPosition = originRoot.localPosition;
            targetRoot.localRotation = originRoot.localRotation;
            targetRoot.localScale = originRoot.localScale;
        }

        public static void RemoveCustomComponents(Transform root)
        {
            //ExtendedLogger.LogInfo("Utils", $"Trying to remove custom components on {root.name}", root);

            foreach (Transform child in root.transform)
            {
                RemoveCustomComponents(child);
            }

            bool finished = false;
            int it = 0;
            while (!finished && it < 100)
            {
                it++;
                finished = true;

                NetworkUser networkUser = root.GetComponent<NetworkUser>();
                
                if(networkUser != null)
                    UnityEngine.Object.DestroyImmediate(networkUser);

                // Looked up by type name so the package does not take a hard dependency on URP.
                Component cameraData = root.GetComponent("UniversalAdditionalCameraData");

                if(cameraData != null)
                    UnityEngine.Object.DestroyImmediate(cameraData);
                
                Component[] components = root.gameObject.GetComponents<Component>();
                
                Random r = new Random();
                List<int> accessIds = Enumerable.Range(0, components.Length).OrderBy(x => r.Next()).ToList();

                for (int i = 0; i < components.Length; ++i)
                {
                    Component comp = components[accessIds[i]];
                    
                    if (ReplayComponentPreserver.IsPreserved(comp) || comp is Transform || comp is Renderer || comp is MeshFilter || comp is TMP_Text || comp is Canvas || comp is CanvasScaler ||
                        comp is CanvasRenderer || comp is AvatarHMDAnatomy || comp is YawTowardsLocalHead || comp is RecordingPrefabInformation ||
                        comp is Image) continue;

                    UnityEngine.Object.DestroyImmediate(comp);
                    finished = false;
                }
            }
            //Debug.Log("Trying to remove custom finished.");
        }

        public static void ModifyComponents(Transform root)
        {
            foreach (Transform child in root.transform)
            {
                ModifyComponents(child);
            }
            
            root.gameObject.tag = "InstantiatedForPlayback";
        }

        public static Button CreateButton(GameObject buttonPrefab, string buttonText, Color color, Transform parent,
            float xOffset, float yOffset, float width, float height, Vector2 scale, float fontSize)
        {
            GameObject buttonGo = GameObject.Instantiate(buttonPrefab);
            buttonGo.name = buttonText;
            buttonGo.transform.SetParent(parent, false);
            RectTransform rectTransform = buttonGo.GetComponent<RectTransform>();

            float offset = 10.0f;
            if (width <= 0.0f)
                width = rectTransform.rect.width;
            if (height <= 0.0f)
                height = rectTransform.rect.height;

            float scaledWidth = scale.x * width;
            float scaledHeight = scale.y * height;

            if (scaledWidth <= 15)
            {
                scaledWidth = 15;
                xOffset = (float)(xOffset - 0.5f * width + 0.5 * scaledWidth);
            }

            rectTransform.sizeDelta = new Vector2(scaledWidth, scaledHeight);
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x + xOffset,
                rectTransform.localPosition.y + yOffset, rectTransform.localPosition.z);
            rectTransform.localPosition = new Vector3(xOffset, yOffset, 0.0f);

            Button button = buttonGo.GetComponent<Button>();
            Image image = buttonGo.GetComponent<Image>();
            image.color = color;
            button.targetGraphic = image;
            TextMeshProUGUI text = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            text.text = buttonText;
            text.color = Color.black;
            text.fontSize = fontSize;
            return button;
        }

        public static void CreateRectBox(RectTransform rect)
        {
            Vector3[] corner = new Vector3[4];
            rect.GetWorldCorners(corner);

            Vector3 max = Vector3.Max(Vector3.Max(Vector3.Max(corner[0], corner[1]), corner[2]), corner[3]);
            Vector3 min = Vector3.Min(Vector3.Min(Vector3.Min(corner[0], corner[1]), corner[2]), corner[3]);

            GameObject _containmentCube = null;

            if (_containmentCube == null)
            {
                _containmentCube = GameObject.Instantiate(Resources.Load<GameObject>("Models/ContainmentBox"));
                ;
            }

            max += Vector3.Cross(corner[0] - corner[1], corner[0] - corner[2]).normalized;
            Vector3 diff = max - min;
            _containmentCube.transform.localScale = diff;
            _containmentCube.transform.position = min + 0.5f * diff;
        }

        public static void DestroyButton(Button currentButton, Button invokingButton, UnityAction action)
        {
            if(currentButton != null)
                GameObject.Destroy(currentButton.gameObject);
            invokingButton.GetComponent<UnityEngine.UI.Outline>().enabled = false;
            invokingButton.onClick.RemoveAllListeners();
            invokingButton.onClick.AddListener(action);
        }

        public static void DestroyButtons(List<Button> currentButtons, Button invokingButton, Color originalColor, UnityAction action)
        {
            if (invokingButton != null)
            {
                Image image = invokingButton.GetComponent<Image>();
                image.color = originalColor;

                if (currentButtons != null)
                    for (int i = 0; i < currentButtons.Count; ++i)
                        DestroyButton(currentButtons[i], invokingButton, action);

                invokingButton.GetComponent<UnityEngine.UI.Outline>().enabled = false;
                invokingButton.onClick.RemoveAllListeners();
                invokingButton.onClick.AddListener(action);
            }
        }

        public static void DestroyButtons(List<Button> currentButtons)
        {
            for (int i = 0; i < currentButtons.Count; ++i)
                GameObject.Destroy(currentButtons[i].gameObject);
        }
    }

    [Serializable]
    public class FileNameInfo
    {
        public string[] fileNames;

        public FileNameInfo(string[] fileNames)
        {
            this.fileNames = fileNames;
        }
    }

    /// <summary>
    /// Source of microphone PCM for the <see cref="VRSYS.Scripts.Recording.MicrophoneRecorder"/>. The
    /// recorder pulls fixed-size chunks via <see cref="Read"/>; implementations decide where the samples
    /// come from (Unity <c>Microphone</c> clip, an external voice SDK, etc.). Implement
    /// <see cref="System.IDisposable"/> as well if the reader holds subscriptions that must be released
    /// when the recorder is destroyed.
    /// </summary>
    public interface IMicrophoneClipReader
    {
        int Channels { get; }
        int SamplingRate { get; }

        /// <summary>
        /// Fills <paramref name="buffer"/> with the next chunk of interleaved samples. Returns the number
        /// of buffer-fulls currently available (&gt;= 0) when a chunk was written, or a negative value when
        /// not enough data is buffered yet. The recorder loops on the positive return to drain backlog and
        /// uses it to back-date the first recorded sample so buffered audio stays time-aligned.
        /// </summary>
        float Read(float[] buffer);
    }

    // adapted from Photon.Voice.Unity.MicWrapper
    public class MicrophoneClipReader : IMicrophoneClipReader
    {
        private AudioClip _microphoneClip;
        private string _device;

        public MicrophoneClipReader(AudioClip mic, string device)
        {
            _microphoneClip = mic;
            _device = device;
        }

        public int Channels
        {
            get
            {
                if (_microphoneClip != null)
                    return _microphoneClip.channels;
                else
                    return 0;
            }
        }

        public int SamplingRate
        {
            get
            {
                if (_microphoneClip != null)
                    return _microphoneClip.frequency;
                else
                    return 0;
            }
        }

        private int _micPrevPos;
        private int _micLoopCnt;
        private int _readAbsPos;

        public float Read(float[] buffer)
        {
            int micPos = Microphone.GetPosition(_device);
            if (micPos < _micPrevPos)
            {
                _micLoopCnt++;
            }

            _micPrevPos = micPos;

            var micAbsPos = _micLoopCnt * _microphoneClip.samples + micPos;

            if (_microphoneClip.channels == 0)
            {
                return -1;
            }

            var bufferSamplesCount = buffer.Length / _microphoneClip.channels;

            var nextReadPos = _readAbsPos + bufferSamplesCount;
            if (nextReadPos < micAbsPos)
            {
                _microphoneClip.GetData(buffer, _readAbsPos % _microphoneClip.samples);
                var dataToRead = (micAbsPos - _readAbsPos) / (float)bufferSamplesCount;
                _readAbsPos = nextReadPos;
                return dataToRead;
            }
            else
            {
                return -1;
            }
        }
    }
}
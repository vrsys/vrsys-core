#if UNITY_EDITOR
using OdinNative.Unity;
using OdinNative.Unity.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public class OdinAssetContext : Editor
{
    internal const string MenuTag = "Assets/Create/4Players ODIN";
    private static GameObject NewOdinManager(string guid = "a421abe223e2dee45b89e686a84e5545")
    {
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (!assetPath.EndsWith("OdinManager.prefab"))
        {
            Debug.LogError($"No OdinManager prefab! {assetPath}");
            return null;
        }

        Object asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject));
        return PrefabUtility.InstantiatePrefab(asset) as GameObject;
    }

    [MenuItem(MenuTag + "/OdinManager/Prefab/Default")]
    public static GameObject CreateDefault()
    {
        GameObject odin = NewOdinManager();
        if(odin == null) return null;

        OdinEditorConfig config = odin.GetComponent<OdinEditorConfig>();
        if(config != null) config.DeviceSampleRate = OdinNative.Core.MediaSampleRate.Hz48000;

        return odin;
    }

    [MenuItem(MenuTag + "/OdinManager/Prefab/Advanced")]
    public static GameObject CreateAdvanced()
    {
        GameObject odin = CreateDefault();
        if (odin == null) return null;

        OdinHandler handler = odin.GetComponent<OdinHandler>();
        if (handler != null)
        {
            handler.Use3DAudio = true;
            handler.CreatePlayback = false;

            Object obj = Selection.activeObject;
            if (obj is AudioMixer)
            {
                AudioMixer mixer = obj as AudioMixer;
                handler.PlaybackAudioMixer = mixer;
                var groups = mixer.FindMatchingGroups("Odin");
                if (groups.Length <= 0) groups = mixer.FindMatchingGroups("Master");
                handler.PlaybackAudioMixerGroup =  groups.FirstOrDefault();
            }
            else if (obj is AudioMixerGroup)
            {
                AudioMixerGroup group = obj as AudioMixerGroup;
                handler.PlaybackAudioMixer = group.audioMixer;
                handler.PlaybackAudioMixerGroup = group;
            }
            else
                Debug.LogWarning("Selection has to be an AudioMixer or AudioMixerGroup! Skip playback mixer settings.");
        }

        return odin;
    }

    [MenuItem(MenuTag + "/OdinManager/Components/Basic")]
    public static GameObject CreateComponents()
    {
        GameObject obj = Selection.activeGameObject;
        if (obj == null || obj.transform.parent != null)
        {
            Debug.LogError("Selected object has to be a root GameObject!");
            return null;
        }

        OdinEditorConfig[] configs = FindObjectsOfType<OdinEditorConfig>();
        OdinEditorConfig config = configs.Length <= 0 ? obj.AddComponent<OdinEditorConfig>() : configs[0];
        if (config != null) config.DeviceSampleRate = OdinNative.Core.MediaSampleRate.Hz48000;

        OdinHandler handler = obj.GetComponent<OdinHandler>();
        if(handler == null) handler = obj.AddComponent<OdinHandler>();
        if (handler != null)
        {
            handler.Use3DAudio = true;
            handler.CreatePlayback = false;
        }

        return obj;
    }

    [MenuItem(MenuTag + "/OdinManager/Components/Extended")]
    public static GameObject CreateFullComponents()
    {
        GameObject obj = CreateComponents();

        OdinHandler handler = obj.GetComponent<OdinHandler>();
        MicrophoneReader[] micReaders = FindObjectsOfType<MicrophoneReader>();
        if (handler != null) handler.Microphone = micReaders.Length <= 0 ? obj.AddComponent<MicrophoneReader>() : micReaders[0];

        return obj;
    }

    [MenuItem(MenuTag + "/OdinManager/Link AudioMixerGroup")]
    public static void LinkAudioMixerGroup()
    {
        Object obj = Selection.activeObject;
        foreach (OdinHandler handler in FindObjectsOfType<OdinHandler>())
        {
            if (obj is AudioMixerGroup)
            {
                AudioMixerGroup group = obj as AudioMixerGroup;
                handler.PlaybackAudioMixer = group.audioMixer;
                handler.PlaybackAudioMixerGroup = group;
            }
            else
            {
                Debug.LogWarning("Selection has to be an AudioMixerGroup!");
                break;
            }
        }
    }
}
#endif
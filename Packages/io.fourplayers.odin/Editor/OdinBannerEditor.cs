#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace OdinNative.Unity.UIEditor
{
    [CustomEditor(typeof(OdinNative.Unity.OdinBanner))]
    public class OdinBannerEditor : Editor
    {
        public Texture2D OdinBannerTexture;
        public override void OnInspectorGUI()
        {
            if (OdinBannerTexture)
            {
                GUILayout.Box(OdinBannerTexture, GUILayout.ExpandWidth(true), GUILayout.Height(150));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Admin Panel"))
                OpenAdminPanel();
            if (GUILayout.Button("Web Documentation"))
                OpenDocumentation();
            GUILayout.EndHorizontal();
        }

        [MenuItem("Window/4Players ODIN/Admin Panel", false, 90)]
        static void OpenAdminPanel()
        {
            Application.OpenURL("https://app.netplay-config.4players.de/");
        }

        [MenuItem("Window/4Players ODIN/Documentation", false, 91)]
        static void OpenDocumentation()
        {
            Application.OpenURL("https://developers.4players.io/odin/");
        }
    }
}
#endif

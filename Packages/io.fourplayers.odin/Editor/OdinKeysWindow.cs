#if UNITY_EDITOR
using System;
using System.Linq;
using OdinNative.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OdinNative.Unity.UIEditor
{
    public class OdinKeysWindow : EditorWindow
    {
        public StyleSheet styleSheet;
        public string AccessKey;
        public string Gateway;
        private TextElement accessKeyTextElement;
        private OdinEditorConfig config;

        [MenuItem("Window/4Players ODIN/Manage Access", false, 0)]
        public static void ShowWindow()
        {
            OdinKeysWindow window = GetWindow<OdinKeysWindow>();
            window.minSize = new Vector2(440, 220);
            window.maxSize = window.minSize;
            window.titleContent = new GUIContent("Manage Access");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(styleSheet);

            // Headline
            Label headlineLabel = new Label("Manage Access to 4Players ODIN");
            headlineLabel.AddToClassList("headline");
            root.Add(headlineLabel);

            config = FindObjectsOfType<OdinEditorConfig>().FirstOrDefault();
            if (config == null)
            {
                // Displays no config found error
                VisualElement innerContainerNoConfig = new VisualElement();
                innerContainerNoConfig.AddToClassList("inner-container");
                root.Add(innerContainerNoConfig);
                Label noConfigFoundLabel = new Label("ODIN configuration is not available");
                noConfigFoundLabel.AddToClassList("row-label-error");
                innerContainerNoConfig.Add(noConfigFoundLabel);
                return;
            }

            AccessKey = config.AccessKey;
            Gateway = config.Server;

            // Renders the gateway box
            VisualElement innerContainerGateway = new VisualElement();
            innerContainerGateway.AddToClassList("inner-container");
            root.Add(innerContainerGateway);
            Label gatewayLabel = new Label("Using ODIN Gateway:");
            gatewayLabel.AddToClassList("row-label");
            TextElement gatewayText = new TextElement();
            gatewayText.AddToClassList("key");
            innerContainerGateway.Add(gatewayLabel);
            innerContainerGateway.Add(gatewayText);
            gatewayText.text = Gateway;

            // Renders the access key box
            VisualElement innerContainerKey = new VisualElement();
            innerContainerKey.AddToClassList("inner-container");
            root.Add(innerContainerKey);
            Label accessKeyLabel = new Label("Your Access Key:");
            accessKeyLabel.AddToClassList("row-label");
            accessKeyTextElement = new TextElement();
            accessKeyTextElement.AddToClassList("key");
            accessKeyTextElement.text = String.IsNullOrEmpty(AccessKey) ? "- none -" : AccessKey;
            innerContainerKey.Add(accessKeyLabel);
            innerContainerKey.Add(accessKeyTextElement);

            // Renders the generate access key button
            Button accessKeyBtn = new Button();
            accessKeyBtn.clicked += OnClickAccessBtn;
            accessKeyBtn.AddToClassList("access-key-btn");
            accessKeyBtn.text = "Generate Access Key";
            innerContainerKey.Add(accessKeyBtn);
        }

        public void OnClickAccessBtn()
        {
            config.GenerateUIAccessKey();
            accessKeyTextElement.text = config.AccessKey;
            EditorUtility.SetDirty(config);
        }
    }
}
#endif
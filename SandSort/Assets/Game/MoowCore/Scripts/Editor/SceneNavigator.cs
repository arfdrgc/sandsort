using System.Collections.Generic;
using UnityEngine;

namespace MoowCore.Editor
{
    using UnityEditor;
    using UnityEngine;
    using UnityEditor.SceneManagement;

    public class SceneNavigator : EditorWindow
    {
        [MenuItem("Window/Scene Navigator")]
        public static void ShowWindow()
        {
            GetWindow<SceneNavigator>("Scene Navigator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Scenes Used in Game", EditorStyles.boldLabel);

            // List all scenes in the build settings

            var root = "Assets/Scenes/";
            var scenesGUIDList = AssetDatabase.FindAssets("t:Scene", new[] { root });
            for (int i = 0; i < scenesGUIDList.Length; i++)
            {
                var GUID = scenesGUIDList[i];
                string scenePath = AssetDatabase.GUIDToAssetPath(GUID);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (GUILayout.Button(sceneName))
                {
                    // Load the scene when the button is clicked
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
        }
    }
}
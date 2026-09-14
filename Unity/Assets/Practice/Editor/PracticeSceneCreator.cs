using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Demo.MyPractice.Editor
{
    public static class PracticeSceneCreator
    {
        public static void CreateAndSetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var gameRoot = new GameObject("PracticeGameManager");
            gameRoot.AddComponent<MyPracticeGame>();

            var cameraObj = GameObject.Find("Main Camera");
            if (cameraObj != null)
            {
                cameraObj.transform.position = new Vector3(0, 1.5f, -3f);
                cameraObj.transform.LookAt(new Vector3(0, 1f, 3f));
            }

            string scenePath = "Assets/Practice/PracticeBattleScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Practice] 成功创建并设置练手场景: {scenePath}");
        }
    }
}

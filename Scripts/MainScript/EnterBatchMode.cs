using UnityEditor;
using UnityEngine;

public class EnterBatchMode : MonoBehaviour
{
    static void EnterPlayMode()
    {
        EditorApplication.EnterPlaymode();
    }

    static void ExitPlayMode()
    {
        EditorApplication.ExitPlaymode();
    }

    [MenuItem("Custom/Play Scene")]
    static void PlayScene()
    {
        EnterPlayMode();
    }
}

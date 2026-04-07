using UnityEditor;
using UnityEngine;

public class Builder
{
    public static void BuildMac()
    {
        string[] scenes = { "Assets/Scenes/MainScene.unity" };
        BuildPipeline.BuildPlayer(scenes, "Builds/Mac/YutNori.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        Debug.Log("Mac build complete.");
    }
}

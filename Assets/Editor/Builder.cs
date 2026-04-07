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

    public static void BuildWebGL()
    {
        // GitHub Pages doesn't send correct Content-Encoding headers for .gz files,
        // so enable decompression fallback to handle it in JavaScript instead.
        PlayerSettings.WebGL.decompressionFallback = true;

        string[] scenes = { "Assets/Scenes/MainScene.unity" };
        BuildPipeline.BuildPlayer(scenes, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log("WebGL build complete.");
    }
}

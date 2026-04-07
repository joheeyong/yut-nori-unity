using UnityEngine;

/// <summary>
/// Automatically creates the GameManager object at runtime without requiring scene setup.
/// Uses RuntimeInitializeOnLoadMethod so no scene editing is needed.
/// </summary>
public static class GameInit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("GameManager");
        go.AddComponent<BoardView>();
        Object.DontDestroyOnLoad(go);
    }
}

using UnityEngine;

public static class BoardData
{
    public static readonly Vector2[] NodePositions = new Vector2[]
    {
        new Vector2( 240, -240), // 0: Start/Finish
        new Vector2( 144, -240), // 1
        new Vector2(  48, -240), // 2
        new Vector2( -48, -240), // 3
        new Vector2(-144, -240), // 4
        new Vector2(-240, -240), // 5: BL corner (no shortcut)
        new Vector2(-240, -144), // 6
        new Vector2(-240,  -48), // 7
        new Vector2(-240,   48), // 8
        new Vector2(-240,  144), // 9
        new Vector2(-240,  240), // 10: TL corner → Route 1
        new Vector2(-144,  240), // 11
        new Vector2( -48,  240), // 12
        new Vector2(  48,  240), // 13
        new Vector2( 144,  240), // 14
        new Vector2( 240,  240), // 15: TR corner → Route 2
        new Vector2( 240,  144), // 16
        new Vector2( 240,   48), // 17
        new Vector2( 240,  -48), // 18
        new Vector2( 240, -144), // 19
        new Vector2(-160, -160), // 20 (visual only)
        new Vector2( -80,  -80), // 21 (visual only)
        new Vector2(   0,    0), // 22: CENTER
        new Vector2(  80,  -80), // 23
        new Vector2( 160, -160), // 24
        new Vector2(-160,  160), // 25: TL shortcut
        new Vector2( -80,   80), // 26
        new Vector2( 160,  160), // 27: TR shortcut
        new Vector2(  80,   80), // 28
    };

    public static readonly int[][] Routes = new int[][]
    {
        new int[]{ 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19 },
        new int[]{ 1,2,3,4,5,6,7,8,9,10,25,26,22,23,24 },
        new int[]{ 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,27,28,22,23,24 },
    };

    public static readonly (int, int)[] BoardLines = new (int, int)[]
    {
        (0,1),(1,2),(2,3),(3,4),(4,5),
        (5,6),(6,7),(7,8),(8,9),(9,10),
        (10,11),(11,12),(12,13),(13,14),(14,15),
        (15,16),(16,17),(17,18),(18,19),(19,0),
        (10,25),(25,26),(26,22),
        (15,27),(27,28),(28,22),
        (22,23),(23,24),(24,0),
    };

    public static readonly Color[] PlayerColors = new Color[]
    {
        new Color(0.9f, 0.2f, 0.2f, 1f),
        new Color(0.2f, 0.4f, 0.9f, 1f),
    };

    public static readonly string[] ThrowNames = new string[]
    {
        "모", "도", "개", "걸", "윷",
    };

    public static string StepsToName(int steps)
    {
        if (steps == -1) return "빽도";
        if (steps == 5)  return ThrowNames[0];
        if (steps >= 1 && steps <= 4) return ThrowNames[steps];
        return "?";
    }

    public static bool TryGetShortcutRoute(int route0StepIndex, out int newRouteId)
    {
        switch (route0StepIndex)
        {
            case 9:  newRouteId = 1; return true;
            case 14: newRouteId = 2; return true;
            default: newRouteId = 0; return false;
        }
    }
}

using UnityEngine;

public static class BoardData
{
    // 29 node positions in Canvas local space (board center = 0,0), y-up
    public static readonly Vector2[] NodePositions = new Vector2[]
    {
        // Outer ring
        new Vector2( 240, -240), // 0: Start/Finish (bottom-right)
        new Vector2( 144, -240), // 1
        new Vector2(  48, -240), // 2
        new Vector2( -48, -240), // 3
        new Vector2(-144, -240), // 4
        new Vector2(-240, -240), // 5: BL corner
        new Vector2(-240, -144), // 6
        new Vector2(-240,  -48), // 7
        new Vector2(-240,   48), // 8
        new Vector2(-240,  144), // 9
        new Vector2(-240,  240), // 10: TL corner
        new Vector2(-144,  240), // 11
        new Vector2( -48,  240), // 12
        new Vector2(  48,  240), // 13
        new Vector2( 144,  240), // 14
        new Vector2( 240,  240), // 15: TR corner
        new Vector2( 240,  144), // 16
        new Vector2( 240,   48), // 17
        new Vector2( 240,  -48), // 18
        new Vector2( 240, -144), // 19

        // Shortcut / diagonal nodes
        new Vector2(-160, -160), // 20: BL shortcut
        new Vector2( -80,  -80), // 21: BL shortcut inner
        new Vector2(   0,    0), // 22: CENTER
        new Vector2(  80,  -80), // 23: center-to-finish inner
        new Vector2( 160, -160), // 24: center-to-finish outer

        new Vector2(-160,  160), // 25: TL shortcut
        new Vector2( -80,   80), // 26: TL shortcut inner

        new Vector2( 160,  160), // 27: TR shortcut
        new Vector2(  80,   80), // 28: TR shortcut inner
    };

    // Routes: stepIndex -1 = HOME, >= route.Length = FINISHED
    // Node 0 (240,-240) is Start/Finish; pieces enter at node 1 on their first step.
    public static readonly int[][] Routes = new int[][]
    {
        // Route 0: Outer ring only (does NOT take any shortcut)
        new int[]{ 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19 },

        // Route 1: BL shortcut (land exactly on node 5 at step 4)
        new int[]{ 1,2,3,4,5,20,21,22,23,24 },

        // Route 2: TL shortcut (land exactly on node 10 at step 9 of route 0,
        //          i.e. step 9 of this route)
        new int[]{ 1,2,3,4,5,6,7,8,9,10,25,26,22,23,24 },

        // Route 3: TR shortcut (land exactly on node 15 at step 14 of route 0)
        new int[]{ 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,27,28,22,23,24 },
    };

    // Board lines: pairs of node indices to draw lines between
    public static readonly (int, int)[] BoardLines = new (int, int)[]
    {
        // Outer ring
        (0,1),(1,2),(2,3),(3,4),(4,5),
        (5,6),(6,7),(7,8),(8,9),(9,10),
        (10,11),(11,12),(12,13),(13,14),(14,15),
        (15,16),(16,17),(17,18),(18,19),(19,0),

        // Diagonal shortcuts
        (5,20),(20,21),(21,22),
        (10,25),(25,26),(26,22),
        (15,27),(27,28),(28,22),
        (22,23),(23,24),(24,0),
    };

    public static readonly Color[] PlayerColors = new Color[]
    {
        new Color(0.9f, 0.2f, 0.2f, 1f), // Player 0: Red
        new Color(0.2f, 0.4f, 0.9f, 1f), // Player 1: Blue (AI)
    };

    public static readonly string[] ThrowNames = new string[]
    {
        "모", // 0 face-up = 5 steps
        "도", // 1 face-up = 1 step
        "개", // 2 face-up = 2 steps
        "걸", // 3 face-up = 3 steps
        "윷", // 4 face-up = 4 steps
    };

    // Given steps (1-5), return the ThrowNames index
    // steps=1->도(1), steps=2->개(2), steps=3->걸(3), steps=4->윷(4), steps=5->모(0)
    public static string StepsToName(int steps)
    {
        if (steps == 5) return ThrowNames[0]; // 모
        if (steps >= 1 && steps <= 4) return ThrowNames[steps];
        return "?";
    }

    // Shortcut trigger rules:
    // If a piece is on Route 0 and lands exactly on one of these stepIndices,
    // switch it to the corresponding route at the same stepIndex.
    // (stepIndex 4 -> node 5 -> Route 1)
    // (stepIndex 9 -> node 10 -> Route 2)
    // (stepIndex 14 -> node 15 -> Route 3)
    public static bool TryGetShortcutRoute(int route0StepIndex, out int newRouteId)
    {
        switch (route0StepIndex)
        {
            case 4:  newRouteId = 1; return true;
            case 9:  newRouteId = 2; return true;
            case 14: newRouteId = 3; return true;
            default: newRouteId = 0; return false;
        }
    }
}

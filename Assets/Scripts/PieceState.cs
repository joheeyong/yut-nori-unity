using System.Collections.Generic;

/// <summary>
/// Represents the state of a single game piece (말).
/// stepIndex == -1  => piece is at HOME (not yet entered the board)
/// stepIndex >= route.Length => piece is FINISHED
/// </summary>
public class PieceState
{
    public int playerId;  // 0 = Human, 1 = AI
    public int pieceId;   // 0-3 within the player's set
    public int routeId;   // which route (0-3) this piece is currently following
    public int stepIndex; // position along the route (-1 = home)

    public PieceState(int playerId, int pieceId)
    {
        this.playerId  = playerId;
        this.pieceId   = pieceId;
        this.routeId   = 0;
        this.stepIndex = -1;
    }

    /// <summary>True when the piece has not yet entered the board.</summary>
    public bool IsHome => stepIndex < 0;

    /// <summary>True when the piece has completed its route and is finished.</summary>
    public bool IsFinished(int[][] routes) => stepIndex >= routes[routeId].Length;

    /// <summary>
    /// Returns the board node index (0-28) this piece occupies,
    /// or -1 if it is at home or finished.
    /// </summary>
    public int CurrentNode(int[][] routes)
    {
        if (IsHome || IsFinished(routes)) return -1;
        return routes[routeId][stepIndex];
    }

    /// <summary>
    /// Returns the number of steps remaining to finish from current position.
    /// Useful for AI heuristics.
    /// </summary>
    public int StepsToFinish(int[][] routes)
    {
        if (IsFinished(routes)) return 0;
        if (IsHome) return routes[routeId].Length + 1; // +1 for the entry step
        return routes[routeId].Length - stepIndex;
    }

    /// <summary>Deep copy of this piece state.</summary>
    public PieceState Clone()
    {
        return new PieceState(playerId, pieceId)
        {
            routeId   = this.routeId,
            stepIndex = this.stepIndex,
        };
    }

    public override string ToString()
    {
        return $"P{playerId}[{pieceId}] route={routeId} step={stepIndex}";
    }
}

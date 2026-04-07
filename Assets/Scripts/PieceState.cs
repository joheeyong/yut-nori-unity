using System.Collections.Generic;

public class PieceState
{
    public int playerId;
    public int pieceId;
    public int routeId;
    public int stepIndex;
    public int curseTimer = -1; // -1 = no curse, >0 = turns remaining

    public PieceState(int playerId, int pieceId)
    {
        this.playerId  = playerId;
        this.pieceId   = pieceId;
        this.routeId   = 0;
        this.stepIndex = -1;
    }

    public bool IsHome => stepIndex < 0;
    public bool IsFinished(int[][] routes) => stepIndex >= routes[routeId].Length;

    public int CurrentNode(int[][] routes)
    {
        if (IsHome || IsFinished(routes)) return -1;
        return routes[routeId][stepIndex];
    }

    public int StepsToFinish(int[][] routes)
    {
        if (IsFinished(routes)) return 0;
        if (IsHome) return routes[routeId].Length + 1;
        return routes[routeId].Length - stepIndex;
    }

    public PieceState Clone()
    {
        return new PieceState(playerId, pieceId)
        {
            routeId   = this.routeId,
            stepIndex = this.stepIndex,
            curseTimer = this.curseTimer,
        };
    }

    public override string ToString() =>
        $"P{playerId}[{pieceId}] route={routeId} step={stepIndex} curse={curseTimer}";
}

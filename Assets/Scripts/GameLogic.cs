using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result of a TryMove call.
/// </summary>
public struct MoveResult
{
    public bool isValid;
    public int  newRouteId;
    public int  newStepIndex;
    public bool isFinished;
    public List<PieceState> captures;    // opponent pieces that were captured
    public bool stacksOnFriend;          // landed on a friendly piece
}

/// <summary>
/// Pure game logic — no MonoBehaviour, no Unity scene objects.
/// All methods are static.
/// </summary>
public static class GameLogic
{
    // -----------------------------------------------------------------------
    //  Yut throwing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Simulates throwing 4 yut sticks.
    /// Each stick is 50/50 flat (0) or rounded (1).
    /// Count of flat sides facing up:
    ///   0 flat = 모 = 5 steps
    ///   1 flat = 도 = 1 step
    ///   2 flat = 개 = 2 steps
    ///   3 flat = 걸 = 3 steps
    ///   4 flat = 윷 = 4 steps
    /// </summary>
    public static int ThrowYut()
    {
        int flatCount = 0;
        for (int i = 0; i < 4; i++)
            flatCount += Random.Range(0, 2); // 0 or 1

        // flatCount 0 → 5 steps (모)
        return flatCount == 0 ? 5 : flatCount;
    }

    /// <summary>
    /// Returns true if the throw result grants an extra throw (윷=4 or 모=5).
    /// </summary>
    public static bool GivesBonus(int steps) => steps == 4 || steps == 5;

    // -----------------------------------------------------------------------
    //  Movement
    // -----------------------------------------------------------------------

    /// <summary>
    /// Attempts to move <paramref name="piece"/> by <paramref name="steps"/> spaces.
    /// Handles route switching at shortcut corners and captures/stacking.
    /// </summary>
    public static MoveResult TryMove(PieceState piece, int steps, PieceState[][] allPieces)
    {
        var result = new MoveResult
        {
            isValid      = false,
            newRouteId   = piece.routeId,
            newStepIndex = piece.stepIndex,
            isFinished   = false,
            captures     = new List<PieceState>(),
            stacksOnFriend = false,
        };

        if (piece.IsFinished(BoardData.Routes))
            return result; // cannot move a finished piece

        // ---- Compute new step index ----------------------------------------
        int newStep = piece.stepIndex + steps;

        // If piece was at home, moving 1 step puts it at stepIndex 0 (node 1).
        // Home is stepIndex -1, so +steps gives us (steps - 1) as the final index.
        // That is already correct: -1 + 1 = 0.

        int newRoute = piece.routeId;

        // ---- Check if the piece finishes ------------------------------------
        int routeLen = BoardData.Routes[newRoute].Length;
        if (newStep >= routeLen)
        {
            result.isValid      = true;
            result.newRouteId   = newRoute;
            result.newStepIndex = newStep;
            result.isFinished   = true;
            return result;
        }

        // ---- Shortcut detection (only on Route 0) ---------------------------
        // A piece on Route 0 switches route when it lands EXACTLY on a corner.
        if (newRoute == 0)
        {
            if (BoardData.TryGetShortcutRoute(newStep, out int shortcutRoute))
            {
                newRoute = shortcutRoute;
                // stepIndex stays the same — both routes share the same prefix up to
                // and including that corner node (node 5/10/15).
            }
        }

        // ---- Determine the landing node ------------------------------------
        int landingNode = BoardData.Routes[newRoute][newStep];

        // ---- Check for captures and stacking --------------------------------
        int opponentId = 1 - piece.playerId;

        // Collect opponent pieces at the landing node
        var opponentsAtNode = GetPiecesAtNode(landingNode, opponentId, allPieces);

        // Collect friendly pieces at the landing node (exclude current piece's group)
        var friendsAtNode = GetPiecesAtNode(landingNode, piece.playerId, allPieces);
        // Exclude the piece itself from friend check
        friendsAtNode.RemoveAll(p => p.pieceId == piece.pieceId);

        if (opponentsAtNode.Count > 0)
        {
            result.captures = opponentsAtNode;
        }
        else if (friendsAtNode.Count > 0)
        {
            result.stacksOnFriend = true;
        }

        result.isValid      = true;
        result.newRouteId   = newRoute;
        result.newStepIndex = newStep;
        return result;
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns all pieces belonging to <paramref name="player"/> that are
    /// currently on <paramref name="node"/> (node index 0-28).
    /// </summary>
    public static List<PieceState> GetPiecesAtNode(int node, int player, PieceState[][] allPieces)
    {
        var result = new List<PieceState>();
        if (node < 0) return result;

        foreach (var piece in allPieces[player])
        {
            if (piece.IsFinished(BoardData.Routes)) continue;
            if (piece.IsHome) continue;
            if (piece.CurrentNode(BoardData.Routes) == node)
                result.Add(piece);
        }
        return result;
    }

    /// <summary>
    /// Returns all non-finished pieces for <paramref name="player"/>.
    /// (Pieces that can potentially be selected for a move.)
    /// </summary>
    public static List<PieceState> GetMovablePieces(int player, PieceState[][] allPieces)
    {
        var result = new List<PieceState>();
        foreach (var piece in allPieces[player])
        {
            if (!piece.IsFinished(BoardData.Routes))
                result.Add(piece);
        }
        return result;
    }

    /// <summary>
    /// Checks whether all pieces of <paramref name="player"/> are finished.
    /// </summary>
    public static bool HasWon(int player, PieceState[][] allPieces)
    {
        foreach (var piece in allPieces[player])
        {
            if (!piece.IsFinished(BoardData.Routes))
                return false;
        }
        return true;
    }

    // -----------------------------------------------------------------------
    //  AI decision
    // -----------------------------------------------------------------------

    /// <summary>
    /// Simple AI: picks the best piece to move given <paramref name="steps"/>.
    /// Priority: capture > stack with friend > move furthest piece.
    /// Returns the pieceId to move, or -1 if no valid move exists.
    /// </summary>
    public static int AIPickPiece(int aiPlayer, int steps, PieceState[][] allPieces)
    {
        var movable = GetMovablePieces(aiPlayer, allPieces);
        if (movable.Count == 0) return -1;

        int bestPieceId    = -1;
        int bestPriority   = -1; // higher = better
        int bestProgress   = -1; // for tie-breaking: further along = better

        foreach (var piece in movable)
        {
            var res = TryMove(piece, steps, allPieces);
            if (!res.isValid) continue;

            int priority;
            if (res.captures.Count > 0)
                priority = 3; // capture
            else if (res.stacksOnFriend)
                priority = 2; // stack
            else if (res.isFinished)
                priority = 4; // finish a piece – best possible
            else
                priority = 1; // plain move

            // Progress = how far along the new position is (higher = closer to finish)
            int progress = res.isFinished ? 9999 : res.newStepIndex;

            if (priority > bestPriority ||
               (priority == bestPriority && progress > bestProgress))
            {
                bestPriority = priority;
                bestProgress = progress;
                bestPieceId  = piece.pieceId;
            }
        }

        return bestPieceId;
    }
}

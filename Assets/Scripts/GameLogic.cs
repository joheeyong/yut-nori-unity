using System.Collections.Generic;
using UnityEngine;

public struct MoveResult
{
    public bool isValid;
    public int  newRouteId;
    public int  newStepIndex;
    public bool isFinished;
    public List<PieceState> captures;
    public bool stacksOnFriend;
}

public static class GameLogic
{
    // Weighted probabilities: 빽도 3.84%, 도 11.52%, 개 34.56%, 걸 34.56%, 윷 12.96%, 모 2.56%
    public static int ThrowYut()
    {
        float r = Random.value;
        if (r < 0.0384f) return -1;
        r -= 0.0384f;
        if (r < 0.1152f) return 1;
        r -= 0.1152f;
        if (r < 0.3456f) return 2;
        r -= 0.3456f;
        if (r < 0.3456f) return 3;
        r -= 0.3456f;
        if (r < 0.1296f) return 4;
        return 5;
    }

    public static int ThrowTenSticks()
    {
        int front = 0;
        for (int i = 0; i < 10; i++)
            if (Random.value < 0.6f) front++;
        return Mathf.Max(1, front);
    }

    public static bool GivesBonus(int steps) => steps == 4 || steps == 5;

    public static MoveResult TryMove(PieceState piece, int steps, PieceState[][] allPieces)
    {
        var result = new MoveResult
        {
            isValid = false, newRouteId = piece.routeId,
            newStepIndex = piece.stepIndex, isFinished = false,
            captures = new List<PieceState>(), stacksOnFriend = false,
        };

        if (piece.IsFinished(BoardData.Routes)) return result;

        if (steps < 0)
            return TryMoveBackward(piece, Mathf.Abs(steps), allPieces);

        int newStep  = piece.stepIndex + steps;
        int newRoute = piece.routeId;

        if (newStep >= BoardData.Routes[newRoute].Length)
        {
            result.isValid = true; result.newRouteId = newRoute;
            result.newStepIndex = newStep; result.isFinished = true;
            return result;
        }

        if (newRoute == 0 && BoardData.TryGetShortcutRoute(newStep, out int sc))
            newRoute = sc;

        int landNode = BoardData.Routes[newRoute][newStep];

        int opp = 1 - piece.playerId;
        result.captures = GetPiecesAtNode(landNode, opp, allPieces);
        var friends = GetPiecesAtNode(landNode, piece.playerId, allPieces);
        friends.RemoveAll(p => p.pieceId == piece.pieceId);
        result.stacksOnFriend = friends.Count > 0;

        result.isValid = true; result.newRouteId = newRoute; result.newStepIndex = newStep;
        return result;
    }

    static MoveResult TryMoveBackward(PieceState piece, int steps, PieceState[][] allPieces)
    {
        var result = new MoveResult { captures = new List<PieceState>() };
        if (piece.IsHome) { result.isValid = false; return result; }

        int newStep = piece.stepIndex - steps;
        if (newStep < 0)
        {
            result.isValid = true; result.newRouteId = 0;
            result.newStepIndex = -1; return result;
        }
        result.isValid = true; result.newRouteId = piece.routeId; result.newStepIndex = newStep;
        return result;
    }

    // PUSH_BACK card: random on-board opponent piece goes back 2
    public static PieceState ApplyPushBack(int opponent, PieceState[][] pieces)
    {
        var cands = new List<PieceState>();
        foreach (var p in pieces[opponent])
            if (!p.IsHome && !p.IsFinished(BoardData.Routes)) cands.Add(p);
        if (cands.Count == 0) return null;

        var target = cands[Random.Range(0, cands.Count)];
        int ns = target.stepIndex - 2;
        if (ns < 0) { target.stepIndex = -1; target.routeId = 0; }
        else          target.stepIndex = ns;
        return target;
    }

    // DESTINY_DICE: 4 random effects
    public static (int effect, string desc, PieceState piece)
        ApplyDestinyDice(int player, PieceState[][] pieces)
    {
        int opp = 1 - player;
        int eff = Random.Range(0, 4);

        PieceState RandOnBoard(int p)
        {
            var list = new List<PieceState>();
            foreach (var pc in pieces[p])
                if (!pc.IsHome && !pc.IsFinished(BoardData.Routes)) list.Add(pc);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
        }

        switch (eff)
        {
            case 0:
            {
                var t = RandOnBoard(opp);
                if (t != null) { t.stepIndex = -1; t.routeId = 0; }
                return (0, "상대 말 1개 잡기!", t);
            }
            case 1:
            {
                var t = RandOnBoard(player);
                if (t != null) { t.stepIndex = -1; t.routeId = 0; }
                return (1, "내 말 1개 잡힘...", t);
            }
            case 2:
            {
                var t = RandOnBoard(player);
                if (t != null)
                {
                    var mv = TryMove(t, 3, pieces);
                    if (mv.isValid) { t.routeId = mv.newRouteId; t.stepIndex = mv.newStepIndex; }
                }
                return (2, "내 말 3칸 전진!", t);
            }
            default:
            {
                var t = RandOnBoard(opp);
                if (t != null)
                {
                    var mv = TryMove(t, 3, pieces);
                    if (mv.isValid) { t.routeId = mv.newRouteId; t.stepIndex = mv.newStepIndex; }
                }
                return (3, "상대 말 3칸 전진...", t);
            }
        }
    }

    public static void ApplySwap(PieceState a, PieceState b)
    {
        (a.routeId, b.routeId)     = (b.routeId, a.routeId);
        (a.stepIndex, b.stepIndex) = (b.stepIndex, a.stepIndex);
    }

    public static List<PieceState> GetPiecesAtNode(int node, int player, PieceState[][] all)
    {
        var res = new List<PieceState>();
        if (node < 0) return res;
        foreach (var p in all[player])
        {
            if (p.IsFinished(BoardData.Routes) || p.IsHome) continue;
            if (p.CurrentNode(BoardData.Routes) == node) res.Add(p);
        }
        return res;
    }

    public static List<PieceState> GetMovablePieces(int player, PieceState[][] all)
    {
        var res = new List<PieceState>();
        foreach (var p in all[player])
            if (!p.IsFinished(BoardData.Routes)) res.Add(p);
        return res;
    }

    public static bool HasWon(int player, PieceState[][] all)
    {
        foreach (var p in all[player])
            if (!p.IsFinished(BoardData.Routes)) return false;
        return true;
    }

    public static int AIPickPiece(int aiPlayer, int steps, PieceState[][] all)
    {
        int opp = 1 - aiPlayer;
        int best = int.MinValue, bestId = -1;

        foreach (var piece in all[aiPlayer])
        {
            if (piece.IsFinished(BoardData.Routes)) continue;
            if (steps < 0 && piece.IsHome) continue;

            var res = TryMove(piece, steps, all);
            if (!res.isValid) continue;

            int score = EvalMove(piece, res, all, aiPlayer, opp);
            if (score > best) { best = score; bestId = piece.pieceId; }
        }
        return bestId;
    }

    static int EvalMove(PieceState piece, MoveResult res,
                        PieceState[][] all, int player, int opp)
    {
        int score = 0;
        if (res.isFinished) return 1000;

        score += res.newStepIndex * 10;
        score += res.captures.Count * 500;
        if (res.stacksOnFriend) score += 30;
        if (piece.IsHome) score += 50;
        if (res.newRouteId != 0) score += 80;

        if (res.newStepIndex >= 0 && res.newStepIndex < BoardData.Routes[res.newRouteId].Length)
        {
            int node = BoardData.Routes[res.newRouteId][res.newStepIndex];
            foreach (var op in all[opp])
            {
                if (op.IsHome || op.IsFinished(BoardData.Routes)) continue;
                for (int s = 1; s <= 5; s++)
                {
                    var or2 = TryMove(op, s, all);
                    if (!or2.isValid || or2.isFinished) continue;
                    if (or2.newStepIndex < BoardData.Routes[or2.newRouteId].Length &&
                        BoardData.Routes[or2.newRouteId][or2.newStepIndex] == node)
                        score -= 50 * (6 - s);
                }
            }
        }

        score += Random.Range(-15, 16);
        return score;
    }
}

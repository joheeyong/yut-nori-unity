using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game state machine.
/// WAITING_THROW      – current player must press the throw button.
/// WAITING_PIECE_SELECT – human player selects which piece to move.
/// AI_TURN            – AI is deciding and animating its move.
/// GAME_OVER          – a player has won.
/// </summary>
public enum GameState
{
    WAITING_THROW,
    WAITING_PIECE_SELECT,
    AI_TURN,
    GAME_OVER,
}

public class GameController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    //  Public references (set by BoardView)
    // -----------------------------------------------------------------------
    [HideInInspector] public BoardView boardView;

    // -----------------------------------------------------------------------
    //  Game state
    // -----------------------------------------------------------------------
    public PieceState[][] Pieces { get; private set; }   // [2][4]
    public int CurrentPlayer { get; private set; } = 0;  // 0 = human, 1 = AI
    public GameState State { get; private set; } = GameState.WAITING_THROW;

    // Pending throws from 윷/모 bonuses (and the initial throw)
    public List<int> PendingThrows { get; private set; } = new List<int>();

    // The throw the player has selected to use for the next piece selection
    public int ActiveThrow { get; private set; } = 0;

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------
    void Awake()
    {
        InitPieces();
    }

    void Start()
    {
        // boardView will be set by BoardView.Start() before this runs,
        // but guard just in case.
        if (boardView != null)
            boardView.RefreshAll();
    }

    // -----------------------------------------------------------------------
    //  Initialisation
    // -----------------------------------------------------------------------
    void InitPieces()
    {
        Pieces = new PieceState[2][];
        for (int p = 0; p < 2; p++)
        {
            Pieces[p] = new PieceState[4];
            for (int i = 0; i < 4; i++)
                Pieces[p][i] = new PieceState(p, i);
        }
    }

    // -----------------------------------------------------------------------
    //  Button callbacks (called by BoardView UI)
    // -----------------------------------------------------------------------

    /// <summary>Called when the human player presses the 던지기 button.</summary>
    public void OnThrowClicked()
    {
        if (State != GameState.WAITING_THROW) return;
        if (CurrentPlayer != 0) return; // safety: human only

        PerformThrow(0);
    }

    /// <summary>Called by BoardView when the player clicks a piece token.</summary>
    public void OnPieceClicked(int playerId, int pieceId)
    {
        if (State != GameState.WAITING_PIECE_SELECT) return;
        if (playerId != CurrentPlayer) return;
        if (CurrentPlayer != 0) return; // AI does not use this path

        var piece = Pieces[playerId][pieceId];
        if (piece.IsFinished(BoardData.Routes)) return;

        ApplyMove(pieceId, ActiveThrow);
    }

    // -----------------------------------------------------------------------
    //  Core flow
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executes a yut throw for <paramref name="player"/>,
    /// accumulates the result in PendingThrows and advances state.
    /// </summary>
    void PerformThrow(int player)
    {
        int steps = GameLogic.ThrowYut();
        PendingThrows.Add(steps);

        boardView?.ShowThrowResult(steps, GameLogic.GivesBonus(steps));

        // If bonus throw, keep throwing automatically (show result briefly then throw again)
        if (GameLogic.GivesBonus(steps))
        {
            // Bonus: show result and allow another throw
            // For the human, remain in WAITING_THROW so they can see the result
            // and press the button again.
            // We do NOT advance to piece selection yet.
            State = GameState.WAITING_THROW;
            boardView?.UpdateUI();
            return;
        }

        // Move to piece selection
        AdvanceToPieceSelection(player);
    }

    /// <summary>
    /// Transitions to piece selection state.
    /// Picks the first pending throw as the active throw.
    /// </summary>
    void AdvanceToPieceSelection(int player)
    {
        if (PendingThrows.Count == 0) return;

        ActiveThrow = PendingThrows[0];
        // Do not remove yet; we remove after the move is applied.

        if (player == 0)
        {
            // Human: wait for click
            State = GameState.WAITING_PIECE_SELECT;
            boardView?.UpdateUI();
            boardView?.HighlightMovablePieces(player, ActiveThrow);
        }
        else
        {
            // AI: run coroutine
            State = GameState.AI_TURN;
            boardView?.UpdateUI();
            StartCoroutine(AITurn());
        }
    }

    /// <summary>
    /// Applies a move for the current player's piece identified by <paramref name="pieceId"/>
    /// using <paramref name="steps"/> steps.
    /// Handles captures, stacking, finish detection, and turn switching.
    /// </summary>
    public void ApplyMove(int pieceId, int steps)
    {
        var piece  = Pieces[CurrentPlayer][pieceId];
        var result = GameLogic.TryMove(piece, steps, Pieces);

        if (!result.isValid) return;

        // -- Remove used throw --
        PendingThrows.RemoveAt(0);

        // -- Move the piece (and its stack) --
        MoveStack(piece, result.newRouteId, result.newStepIndex);

        bool grantExtraThrow = false;

        // -- Captures --
        if (result.captures.Count > 0)
        {
            foreach (var captured in result.captures)
            {
                // Find all pieces stacked on the same node as the captured piece
                // and send them all home.
                var capturedNode = captured.CurrentNode(BoardData.Routes);
                SendGroupHome(1 - CurrentPlayer, capturedNode);
            }
            grantExtraThrow = true;
            boardView?.ShowMessage($"잡았습니다! 한 번 더!");
        }

        // -- Check win --
        if (GameLogic.HasWon(CurrentPlayer, Pieces))
        {
            State = GameState.GAME_OVER;
            boardView?.RefreshAll();
            boardView?.ShowMessage(CurrentPlayer == 0 ? "플레이어 승리!" : "AI 승리!");
            boardView?.UpdateUI();
            return;
        }

        // -- Extra throw from 윷/모 (already in PendingThrows) or capture --
        if (grantExtraThrow)
        {
            // Insert an extra throw for the current player
            // We'll trigger another throw immediately.
            boardView?.RefreshAll();
            boardView?.UpdateUI();
            StartCoroutine(DelayedExtraThrow());
            return;
        }

        // -- If there are still pending throws (윷/모 accumulated), use next --
        if (PendingThrows.Count > 0)
        {
            boardView?.RefreshAll();
            AdvanceToPieceSelection(CurrentPlayer);
            return;
        }

        // -- Switch player --
        CurrentPlayer = 1 - CurrentPlayer;
        boardView?.RefreshAll();

        if (CurrentPlayer == 0)
        {
            State = GameState.WAITING_THROW;
            boardView?.UpdateUI();
        }
        else
        {
            // AI's turn: it throws first
            State = GameState.AI_TURN;
            boardView?.UpdateUI();
            StartCoroutine(AIThrowCoroutine());
        }
    }

    // -----------------------------------------------------------------------
    //  Stack helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Moves <paramref name="lead"/> piece and all friendly pieces stacked on
    /// the same node to (newRoute, newStep).
    /// </summary>
    void MoveStack(PieceState lead, int newRoute, int newStep)
    {
        int oldNode = lead.CurrentNode(BoardData.Routes);

        // Collect stacked friends (same node, same player, not the lead itself)
        var stackedFriends = new List<PieceState>();
        if (oldNode >= 0)
        {
            foreach (var p in Pieces[lead.playerId])
            {
                if (p.pieceId == lead.pieceId) continue;
                if (p.CurrentNode(BoardData.Routes) == oldNode)
                    stackedFriends.Add(p);
            }
        }

        // Move everyone
        lead.routeId   = newRoute;
        lead.stepIndex = newStep;

        foreach (var friend in stackedFriends)
        {
            friend.routeId   = newRoute;
            friend.stepIndex = newStep;
        }
    }

    /// <summary>
    /// Sends all opponent pieces at <paramref name="node"/> back to home.
    /// </summary>
    void SendGroupHome(int opponentPlayer, int node)
    {
        foreach (var p in Pieces[opponentPlayer])
        {
            if (p.CurrentNode(BoardData.Routes) == node)
            {
                p.routeId   = 0;
                p.stepIndex = -1;
            }
        }
    }

    // -----------------------------------------------------------------------
    //  AI Coroutines
    // -----------------------------------------------------------------------

    IEnumerator AIThrowCoroutine()
    {
        yield return new WaitForSeconds(0.8f);
        PerformThrow(1);
        boardView?.UpdateUI();

        // If more bonus throws, keep throwing
        while (PendingThrows.Count > 1 || (PendingThrows.Count == 1 && GameLogic.GivesBonus(PendingThrows[0])))
        {
            if (PendingThrows.Count == 0) break;
            // The last throw in the list: if it gives bonus, throw again
            int last = PendingThrows[PendingThrows.Count - 1];
            if (!GameLogic.GivesBonus(last)) break;
            yield return new WaitForSeconds(0.6f);
            int extra = GameLogic.ThrowYut();
            PendingThrows.Add(extra);
            boardView?.ShowThrowResult(extra, GameLogic.GivesBonus(extra));
            boardView?.UpdateUI();
        }

        yield return new WaitForSeconds(0.6f);
        AdvanceToPieceSelection(1);
    }

    IEnumerator AITurn()
    {
        yield return new WaitForSeconds(0.7f);

        int chosen = GameLogic.AIPickPiece(1, ActiveThrow, Pieces);
        if (chosen < 0)
        {
            // No valid move (shouldn't happen normally); skip
            PendingThrows.RemoveAt(0);
            CurrentPlayer = 0;
            State = GameState.WAITING_THROW;
            boardView?.RefreshAll();
            boardView?.UpdateUI();
            yield break;
        }

        boardView?.HighlightSinglePiece(1, chosen);
        yield return new WaitForSeconds(0.5f);

        ApplyMove(chosen, ActiveThrow);
    }

    IEnumerator DelayedExtraThrow()
    {
        yield return new WaitForSeconds(0.5f);

        if (CurrentPlayer == 0)
        {
            State = GameState.WAITING_THROW;
            boardView?.UpdateUI();
        }
        else
        {
            StartCoroutine(AIThrowCoroutine());
        }
    }

    // -----------------------------------------------------------------------
    //  Public queries
    // -----------------------------------------------------------------------

    /// <summary>Returns the number of pieces a player has that are finished.</summary>
    public int GetFinishedCount(int player)
    {
        int count = 0;
        foreach (var p in Pieces[player])
            if (p.IsFinished(BoardData.Routes)) count++;
        return count;
    }

    /// <summary>Returns the number of home (not yet placed) pieces for a player.</summary>
    public int GetHomeCount(int player)
    {
        int count = 0;
        foreach (var p in Pieces[player])
            if (p.IsHome) count++;
        return count;
    }
}

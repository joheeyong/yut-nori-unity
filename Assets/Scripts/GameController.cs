using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game state machine for Yutnori.
/// BoardView subscribes to the public events and drives all visual reactions.
/// </summary>
public class GameController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    //  Singleton
    // -----------------------------------------------------------------------
    public static GameController Instance { get; private set; }

    // -----------------------------------------------------------------------
    //  References
    // -----------------------------------------------------------------------
    /// <summary>Set by BoardView during its Start() call.</summary>
    [HideInInspector] public BoardView boardView;

    // -----------------------------------------------------------------------
    //  State
    // -----------------------------------------------------------------------
    public enum GameState
    {
        StartScreen,
        WaitingThrow,
        WaitingPieceSelect,
        AITurn,
        GameOver
    }

    public GameState State { get; private set; } = GameState.WaitingThrow;

    /// <summary>All piece states: [player 0..1][piece 0..3].</summary>
    public PieceState[][] Pieces { get; private set; }

    /// <summary>0 = human, 1 = AI.</summary>
    public int CurrentPlayer { get; private set; } = 0;

    /// <summary>Throw results accumulated but not yet consumed (윷/모 bonuses stack here).</summary>
    public List<int> PendingThrows { get; private set; } = new List<int>();

    /// <summary>Most recent throw result (for display purposes).</summary>
    public int LastThrow { get; private set; } = 0;

    /// <summary>The throw currently being resolved (head of PendingThrows).</summary>
    public int ActiveThrow { get; private set; } = 0;

    // -----------------------------------------------------------------------
    //  Events  (BoardView subscribes to these)
    // -----------------------------------------------------------------------

    /// <summary>Fired after every throw. Param: steps (1-5).</summary>
    public event Action<int> OnThrowResult;

    /// <summary>Fired after a piece moves. Params: player, pieceId, fromNode, toNode. -1 = home/finished.</summary>
    public event Action<int, int, int, int> OnPieceMove;

    /// <summary>Fired when an opponent group is captured. Params: capturingPlayer, nodeWhereCapture happened.</summary>
    public event Action<int, int> OnCapture;

    /// <summary>Fired when a piece stacks on a friendly piece. Params: player, node.</summary>
    public event Action<int, int> OnStack;

    /// <summary>Fired when a player wins. Param: winning player index.</summary>
    public event Action<int> OnPlayerWin;

    /// <summary>Fired when the active player changes. Param: new current player.</summary>
    public event Action<int> OnTurnChange;

    /// <summary>Fired when the player earns a bonus throw (윷 or 모).</summary>
    public event Action OnBonusThrow;

    /// <summary>Fired when a piece finishes (crosses finish line). Params: player, pieceId.</summary>
    public event Action<int, int> OnPieceFinished;

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------

    void Awake()
    {
        Instance = this;
        InitPieces();
    }

    void Start()
    {
        boardView?.RefreshAll();
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

    /// <summary>Resets everything and starts a new game.</summary>
    public void StartGame()
    {
        StopAllCoroutines();
        PendingThrows.Clear();
        CurrentPlayer = 0;
        State = GameState.WaitingThrow;
        InitPieces();
        boardView?.RefreshAll();
        OnTurnChange?.Invoke(CurrentPlayer);
    }

    // -----------------------------------------------------------------------
    //  Human-facing callbacks (connected to UI buttons / piece clicks)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called when the human player presses the 던지기 (throw) button.
    /// Only acts during WaitingThrow when it is the human's turn.
    /// </summary>
    public void OnThrowClicked()
    {
        if (State != GameState.WaitingThrow) return;
        if (CurrentPlayer != 0) return;

        StartCoroutine(HumanThrowSequence());
    }

    /// <summary>
    /// Called when the human clicks a piece token on the board.
    /// Only acts during WaitingPieceSelect for the human player.
    /// </summary>
    public void OnPieceClicked(int playerId, int pieceId)
    {
        if (State != GameState.WaitingPieceSelect) return;
        if (playerId != CurrentPlayer) return;
        if (CurrentPlayer != 0) return; // AI never goes through here

        var piece = Pieces[playerId][pieceId];
        if (piece.IsFinished(BoardData.Routes)) return;

        // Verify the move is actually valid before accepting the click
        var test = GameLogic.TryMove(piece, ActiveThrow, Pieces);
        if (!test.isValid) return;

        ApplyMove(playerId, pieceId, ActiveThrow);
    }

    // -----------------------------------------------------------------------
    //  Core throw flow
    // -----------------------------------------------------------------------

    IEnumerator HumanThrowSequence()
    {
        // Animate throw visually (BoardView handles the actual animation via event)
        int steps = GameLogic.ThrowYut();
        LastThrow = steps;
        PendingThrows.Add(steps);

        OnThrowResult?.Invoke(steps);

        bool isBonus = GameLogic.GivesBonus(steps);
        if (isBonus)
        {
            OnBonusThrow?.Invoke();
            // Stay in WaitingThrow; human must press button again to collect more / proceed
            State = GameState.WaitingThrow;
            boardView?.UpdateUI();
            yield break;
        }

        // No bonus: advance to piece selection
        yield return new WaitForSeconds(0.4f);
        AdvanceToPieceSelection(CurrentPlayer);
    }

    void PerformAIThrow()
    {
        int steps = GameLogic.ThrowYut();
        LastThrow = steps;
        PendingThrows.Add(steps);
        OnThrowResult?.Invoke(steps);
    }

    // -----------------------------------------------------------------------
    //  Piece-selection advancement
    // -----------------------------------------------------------------------

    void AdvanceToPieceSelection(int player)
    {
        if (PendingThrows.Count == 0) return;

        ActiveThrow = PendingThrows[0];

        if (player == 0)
        {
            State = GameState.WaitingPieceSelect;
            boardView?.UpdateUI();
            boardView?.HighlightMovablePieces(player, ActiveThrow);
        }
        else
        {
            State = GameState.AITurn;
            boardView?.UpdateUI();
            StartCoroutine(AIPlayCoroutine());
        }
    }

    // -----------------------------------------------------------------------
    //  Move application
    // -----------------------------------------------------------------------

    /// <summary>
    /// Applies a move for the given player/piece using the given step count.
    /// Fires events, handles captures, stacking, finish, and turn switching.
    /// </summary>
    private void ApplyMove(int playerId, int pieceId, int steps)
    {
        var piece  = Pieces[playerId][pieceId];
        var result = GameLogic.TryMove(piece, steps, Pieces);
        if (!result.isValid) return;

        int fromNode = piece.CurrentNode(BoardData.Routes);

        // Consume this throw
        PendingThrows.RemoveAt(0);

        // Move the piece and any stacked friends
        MoveStack(piece, result.newRouteId, result.newStepIndex);

        int toNode = result.isFinished ? -1 : piece.CurrentNode(BoardData.Routes);
        OnPieceMove?.Invoke(playerId, pieceId, fromNode, toNode);

        bool grantExtraThrow = false;

        // Handle captures
        if (result.captures.Count > 0)
        {
            int captureNode = result.captures[0].CurrentNode(BoardData.Routes);
            SendGroupHome(1 - playerId, captureNode);
            OnCapture?.Invoke(playerId, captureNode);
            grantExtraThrow = true;
        }

        // Handle stacking on friend
        if (result.stacksOnFriend)
        {
            OnStack?.Invoke(playerId, toNode);
        }

        // Handle piece finishing
        if (result.isFinished)
        {
            OnPieceFinished?.Invoke(playerId, pieceId);
        }

        // Check win
        if (CheckWin(playerId))
        {
            State = GameState.GameOver;
            boardView?.RefreshAll();
            OnPlayerWin?.Invoke(playerId);
            boardView?.UpdateUI();
            return;
        }

        // Grant extra throw for capture (insert one more throw attempt)
        if (grantExtraThrow)
        {
            boardView?.RefreshAll();
            boardView?.UpdateUI();
            StartCoroutine(DelayedExtraThrow(playerId));
            return;
        }

        // If there are still queued pending throws (from accumulated 윷/모), use next
        if (PendingThrows.Count > 0)
        {
            boardView?.RefreshAll();
            AdvanceToPieceSelection(playerId);
            return;
        }

        EndTurn();
    }

    private void EndTurn()
    {
        boardView?.RefreshAll();
        CurrentPlayer = 1 - CurrentPlayer;
        OnTurnChange?.Invoke(CurrentPlayer);

        if (CurrentPlayer == 0)
        {
            State = GameState.WaitingThrow;
            boardView?.UpdateUI();
        }
        else
        {
            State = GameState.AITurn;
            boardView?.UpdateUI();
            StartCoroutine(AIThrowCoroutine());
        }
    }

    private bool CheckWin(int player)
    {
        return GameLogic.HasWon(player, Pieces);
    }

    // -----------------------------------------------------------------------
    //  Stack / home helpers
    // -----------------------------------------------------------------------

    void MoveStack(PieceState lead, int newRoute, int newStep)
    {
        int oldNode = lead.CurrentNode(BoardData.Routes);

        // Find all friends stacked on the same node
        var stackedFriends = new List<PieceState>();
        if (oldNode >= 0)
        {
            foreach (var p in Pieces[lead.playerId])
            {
                if (p.pieceId == lead.pieceId) continue;
                if (!p.IsFinished(BoardData.Routes) && p.CurrentNode(BoardData.Routes) == oldNode)
                    stackedFriends.Add(p);
            }
        }

        lead.routeId   = newRoute;
        lead.stepIndex = newStep;

        foreach (var friend in stackedFriends)
        {
            friend.routeId   = newRoute;
            friend.stepIndex = newStep;
        }
    }

    void SendGroupHome(int opponentPlayer, int node)
    {
        foreach (var p in Pieces[opponentPlayer])
        {
            if (!p.IsHome && !p.IsFinished(BoardData.Routes) && p.CurrentNode(BoardData.Routes) == node)
            {
                p.routeId   = 0;
                p.stepIndex = -1;
            }
        }
    }

    // -----------------------------------------------------------------------
    //  AI coroutines
    // -----------------------------------------------------------------------

    IEnumerator AIThrowCoroutine()
    {
        yield return new WaitForSeconds(0.8f);

        PerformAIThrow();
        boardView?.UpdateUI();

        // Keep throwing if last throw was bonus
        while (PendingThrows.Count > 0 && GameLogic.GivesBonus(PendingThrows[PendingThrows.Count - 1]))
        {
            OnBonusThrow?.Invoke();
            yield return new WaitForSeconds(0.6f);
            PerformAIThrow();
            boardView?.UpdateUI();
        }

        yield return new WaitForSeconds(0.5f);
        AdvanceToPieceSelection(1);
    }

    IEnumerator AIPlayCoroutine()
    {
        // Process all pending throws one at a time
        while (PendingThrows.Count > 0)
        {
            ActiveThrow = PendingThrows[0];

            yield return new WaitForSeconds(0.7f);

            int chosen = GameLogic.AIPickPiece(1, ActiveThrow, Pieces);
            if (chosen < 0)
            {
                // No valid move; consume throw and continue
                PendingThrows.RemoveAt(0);
                continue;
            }

            boardView?.HighlightSinglePiece(1, chosen);
            yield return new WaitForSeconds(0.5f);

            // ApplyMove may modify PendingThrows (capture bonus / turn switch),
            // so we call it and then break — ApplyMove will re-enter AIPlayCoroutine
            // via AdvanceToPieceSelection if needed.
            ApplyMove(1, chosen, ActiveThrow);
            yield break;
        }

        // No more throws and no action taken: end turn
        EndTurn();
    }

    IEnumerator DelayedExtraThrow(int player)
    {
        yield return new WaitForSeconds(0.5f);

        if (player == 0)
        {
            State = GameState.WaitingThrow;
            boardView?.UpdateUI();
        }
        else
        {
            State = GameState.AITurn;
            boardView?.UpdateUI();
            StartCoroutine(AIThrowCoroutine());
        }
    }

    // -----------------------------------------------------------------------
    //  Public queries (used by BoardView for display)
    // -----------------------------------------------------------------------

    public int GetFinishedCount(int player)
    {
        int count = 0;
        foreach (var p in Pieces[player])
            if (p.IsFinished(BoardData.Routes)) count++;
        return count;
    }

    public int GetHomeCount(int player)
    {
        int count = 0;
        foreach (var p in Pieces[player])
            if (p.IsHome) count++;
        return count;
    }
}

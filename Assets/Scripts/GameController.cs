using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CardType { None, TenSticks, Trap, PushBack, DestinyDice, Rewind, Curse, Swap }

public static class CardInfo
{
    public static string Emoji(CardType c) => c switch {
        CardType.TenSticks   => "🎋",
        CardType.Trap        => "💣",
        CardType.PushBack    => "⏪",
        CardType.DestinyDice => "🎲",
        CardType.Rewind      => "⏮",
        CardType.Curse       => "💀",
        CardType.Swap        => "🔄",
        _ => ""
    };
    public static string Name(CardType c) => c switch {
        CardType.TenSticks   => "열 윷",
        CardType.Trap        => "함정",
        CardType.PushBack    => "뒤로가기",
        CardType.DestinyDice => "운명의 주사위",
        CardType.Rewind      => "되돌리기",
        CardType.Curse       => "시한부",
        CardType.Swap        => "스왑",
        _ => ""
    };
    public static string Desc(CardType c) => c switch {
        CardType.TenSticks   => "윷 10개로 던지기",
        CardType.Trap        => "보드에 함정 설치 (3턴)",
        CardType.PushBack    => "상대 말 1개를 뒤로 2칸",
        CardType.DestinyDice => "랜덤 효과 발동!",
        CardType.Rewind      => "상대 마지막 이동 취소",
        CardType.Curse       => "각 말 1개에 시한부 (3턴)",
        CardType.Swap        => "내 말↔상대 말 위치 교환",
        _ => ""
    };
    public static readonly CardType[] All = {
        CardType.TenSticks, CardType.Trap, CardType.PushBack,
        CardType.DestinyDice, CardType.Rewind, CardType.Curse, CardType.Swap
    };
    public static CardType[] RandomOptions()
    {
        var pool = new List<CardType>(All);
        var res  = new CardType[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            res[i] = pool[idx]; pool.RemoveAt(idx);
        }
        return res;
    }
}

public class TrapData  { public int nodeIndex, owner, turnsLeft; }
public class CurseData { public int player, pieceId, turnsLeft; }

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    [HideInInspector] public BoardView boardView;

    const int PIECE_COUNT = 3;

    public enum GameState
    {
        StartScreen, CardPicking, WaitingThrow, WaitingPieceSelect,
        TrapPlacing, CursePickOwnPiece, CursePickOpponentPiece,
        SwapPickOwnPiece, SwapPickOpponentPiece,
        AITurn, GameOver
    }

    public GameState        State         { get; private set; } = GameState.WaitingThrow;
    public PieceState[][]   Pieces        { get; private set; }
    public int              CurrentPlayer { get; private set; } = 0;
    public List<int>        PendingThrows { get; private set; } = new List<int>();
    public int              LastThrow     { get; private set; }
    public int              ActiveThrow   { get; private set; }

    public CardType[]       PlayerCard    = { CardType.None, CardType.None };
    public CardType[][]     CardOptions;

    public List<TrapData>   Traps  { get; private set; } = new List<TrapData>();
    public List<CurseData>  Curses { get; private set; } = new List<CurseData>();

    public PieceState[][]   LastMoveSnapshot { get; private set; }

    public int  TurnCount         { get; private set; } = 0;
    int         _finishedCheck    = 0;

    public int  PickedOwnPiece    = -1;
    public bool IsCurseMode       = false;

    public float TurnTimer        { get; private set; } = 15f;
    const float  TIMER_MAX        = 15f;
    bool         _timerRunning    = false;

    // Events
    public event Action<int>            OnThrowResult;
    public event Action<int,int,int,int> OnPieceMove;
    public event Action<int,int>        OnCapture;
    public event Action<int,int>        OnStack;
    public event Action<int>            OnPlayerWin;
    public event Action<int>            OnTurnChange;
    public event Action                 OnBonusThrow;
    public event Action<int,int>        OnPieceFinished;
    public event Action<int,CardType[]> OnCardPickStart;
    public event Action<string>         OnCardEffect;

    void Awake()  { Instance = this; InitPieces(); }
    void Start()  { boardView?.RefreshAll(); }

    void Update()
    {
        if (!_timerRunning) return;
        if (State == GameState.WaitingThrow && CurrentPlayer == 0)
        {
            TurnTimer -= Time.deltaTime;
            boardView?.UpdateUI();
            if (TurnTimer <= 0f) { _timerRunning = false; OnThrowClicked(); }
        }
        else if (State == GameState.WaitingPieceSelect && CurrentPlayer == 0)
        {
            TurnTimer -= Time.deltaTime;
            boardView?.UpdateUI();
            if (TurnTimer <= 0f)
            {
                _timerRunning = false;
                int auto = GameLogic.AIPickPiece(0, ActiveThrow, Pieces);
                if (auto >= 0) ApplyMove(0, auto, ActiveThrow);
                else EndTurn();
            }
        }
        else
        {
            _timerRunning = false;
            TurnTimer = TIMER_MAX;
        }
    }

    void StartTimer() { TurnTimer = TIMER_MAX; _timerRunning = true; }

    void InitPieces()
    {
        Pieces = new PieceState[2][];
        for (int p = 0; p < 2; p++)
        {
            Pieces[p] = new PieceState[PIECE_COUNT];
            for (int i = 0; i < PIECE_COUNT; i++)
                Pieces[p][i] = new PieceState(p, i);
        }
    }

    public void StartGame()
    {
        StopAllCoroutines();
        PendingThrows.Clear(); Traps.Clear(); Curses.Clear();
        TurnCount = 0; _finishedCheck = 0;
        CurrentPlayer = 0;
        PlayerCard[0] = PlayerCard[1] = CardType.None;
        CardOptions = null; PickedOwnPiece = -1; LastMoveSnapshot = null;
        State = GameState.WaitingThrow;
        InitPieces();
        boardView?.RefreshAll();
        OnTurnChange?.Invoke(CurrentPlayer);
        StartTimer();
    }

    // ── Human callbacks ───────────────────────────────────────────────────────
    public void OnThrowClicked()
    {
        if (State != GameState.WaitingThrow || CurrentPlayer != 0) return;
        _timerRunning = false;
        StartCoroutine(HumanThrowSequence(false));
    }

    public void OnUseCardClicked()
    {
        if (CurrentPlayer != 0 || PlayerCard[0] == CardType.None) return;
        if (State != GameState.WaitingThrow && State != GameState.WaitingPieceSelect) return;
        _timerRunning = false;
        ApplyCardHuman(0, PlayerCard[0]);
    }

    public void OnCardOptionChosen(int player, int optionIndex)
    {
        if (State != GameState.CardPicking || CardOptions == null) return;
        if (CardOptions[player] == null || optionIndex >= CardOptions[player].Length) return;
        PlayerCard[player] = CardOptions[player][optionIndex];
        CardOptions[player] = null;
        boardView?.RefreshAll(); boardView?.UpdateUI();
        bool allDone = (CardOptions[0] == null) && (CardOptions[1] == null);
        if (allDone) { State = GameState.WaitingThrow; boardView?.UpdateUI(); StartTimer(); }
    }

    public void OnNodeClicked(int nodeIndex)
    {
        if (State != GameState.TrapPlacing) return;
        Traps.Add(new TrapData { nodeIndex = nodeIndex, owner = CurrentPlayer, turnsLeft = 6 });
        PlayerCard[CurrentPlayer] = CardType.None;
        OnCardEffect?.Invoke("💣 함정 설치!");
        State = PendingThrows.Count > 0 ? GameState.WaitingPieceSelect : GameState.WaitingThrow;
        boardView?.RefreshAll(); boardView?.UpdateUI();
        if (State == GameState.WaitingThrow && CurrentPlayer == 0) StartTimer();
    }

    public void OnPieceClicked(int playerId, int pieceId)
    {
        if (State == GameState.CursePickOwnPiece || State == GameState.SwapPickOwnPiece)
        {
            if (playerId != CurrentPlayer) return;
            var pc = Pieces[playerId][pieceId];
            if (pc.IsFinished(BoardData.Routes) || pc.IsHome) return;
            PickedOwnPiece = pieceId;
            State = (State == GameState.CursePickOwnPiece)
                ? GameState.CursePickOpponentPiece : GameState.SwapPickOpponentPiece;
            boardView?.UpdateUI();
            return;
        }
        if (State == GameState.CursePickOpponentPiece || State == GameState.SwapPickOpponentPiece)
        {
            if (playerId == CurrentPlayer) return;
            var opc = Pieces[playerId][pieceId];
            if (opc.IsFinished(BoardData.Routes) || opc.IsHome) return;
            if (State == GameState.CursePickOpponentPiece)
            {
                Curses.Add(new CurseData { player = CurrentPlayer,   pieceId = PickedOwnPiece, turnsLeft = 6 });
                Curses.Add(new CurseData { player = 1-CurrentPlayer, pieceId = pieceId,        turnsLeft = 6 });
                Pieces[CurrentPlayer][PickedOwnPiece].curseTimer = 6;
                Pieces[1-CurrentPlayer][pieceId].curseTimer      = 6;
                PlayerCard[CurrentPlayer] = CardType.None;
                OnCardEffect?.Invoke("💀 시한부 발동!");
            }
            else
            {
                GameLogic.ApplySwap(Pieces[CurrentPlayer][PickedOwnPiece], Pieces[1-CurrentPlayer][pieceId]);
                PlayerCard[CurrentPlayer] = CardType.None;
                OnCardEffect?.Invoke("🔄 스왑!");
            }
            PickedOwnPiece = -1;
            State = PendingThrows.Count > 0 ? GameState.WaitingPieceSelect : GameState.WaitingThrow;
            boardView?.RefreshAll(); boardView?.UpdateUI();
            if (State == GameState.WaitingThrow && CurrentPlayer == 0) StartTimer();
            return;
        }

        if (State != GameState.WaitingPieceSelect || playerId != CurrentPlayer || CurrentPlayer != 0) return;
        var p2 = Pieces[playerId][pieceId];
        if (p2.IsFinished(BoardData.Routes)) return;
        if (ActiveThrow < 0 && p2.IsHome) return;
        var test = GameLogic.TryMove(p2, ActiveThrow, Pieces);
        if (!test.isValid) return;
        ApplyMove(playerId, pieceId, ActiveThrow);
    }

    // ── Card application ─────────────────────────────────────────────────────
    void ApplyCardHuman(int player, CardType card)
    {
        switch (card)
        {
            case CardType.TenSticks:
                PlayerCard[player] = CardType.None;
                StartCoroutine(HumanThrowSequence(true));
                break;
            case CardType.Trap:
                State = GameState.TrapPlacing;
                boardView?.UpdateUI();
                break;
            case CardType.PushBack:
            {
                var moved = GameLogic.ApplyPushBack(1-player, Pieces);
                PlayerCard[player] = CardType.None;
                OnCardEffect?.Invoke(moved != null ? "⏪ 뒤로가기!" : "상대 말이 없습니다.");
                boardView?.RefreshAll(); boardView?.UpdateUI();
                break;
            }
            case CardType.DestinyDice:
            {
                var (_, desc, _) = GameLogic.ApplyDestinyDice(player, Pieces);
                PlayerCard[player] = CardType.None;
                OnCardEffect?.Invoke("🎲 " + desc);
                boardView?.RefreshAll(); boardView?.UpdateUI();
                break;
            }
            case CardType.Rewind:
            {
                int opp = 1 - player;
                if (LastMoveSnapshot != null && LastMoveSnapshot[opp] != null)
                {
                    for (int i = 0; i < LastMoveSnapshot[opp].Length && i < Pieces[opp].Length; i++)
                    {
                        Pieces[opp][i].routeId   = LastMoveSnapshot[opp][i].routeId;
                        Pieces[opp][i].stepIndex  = LastMoveSnapshot[opp][i].stepIndex;
                    }
                    OnCardEffect?.Invoke("⏮ 되돌리기!");
                }
                else OnCardEffect?.Invoke("되돌릴 이동이 없습니다.");
                PlayerCard[player] = CardType.None;
                boardView?.RefreshAll(); boardView?.UpdateUI();
                break;
            }
            case CardType.Curse:
                IsCurseMode = true; PickedOwnPiece = -1;
                State = GameState.CursePickOwnPiece;
                boardView?.UpdateUI();
                break;
            case CardType.Swap:
                IsCurseMode = false; PickedOwnPiece = -1;
                State = GameState.SwapPickOwnPiece;
                boardView?.UpdateUI();
                break;
        }
    }

    void ApplyCardAI(int player, CardType card)
    {
        int opp = 1 - player;
        switch (card)
        {
            case CardType.PushBack:
                GameLogic.ApplyPushBack(opp, Pieces);
                OnCardEffect?.Invoke("AI: ⏪ 뒤로가기!");
                break;
            case CardType.DestinyDice:
                var (_, desc, _) = GameLogic.ApplyDestinyDice(player, Pieces);
                OnCardEffect?.Invoke("AI 🎲 " + desc);
                break;
            case CardType.Rewind:
                if (LastMoveSnapshot != null)
                {
                    for (int i = 0; i < Pieces[opp].Length; i++)
                    {
                        Pieces[opp][i].routeId  = LastMoveSnapshot[opp][i].routeId;
                        Pieces[opp][i].stepIndex = LastMoveSnapshot[opp][i].stepIndex;
                    }
                    OnCardEffect?.Invoke("AI: ⏮ 되돌리기!");
                }
                break;
        }
        PlayerCard[player] = CardType.None;
    }

    // ── Throw flow ────────────────────────────────────────────────────────────
    IEnumerator HumanThrowSequence(bool tenSticks)
    {
        int steps = tenSticks ? GameLogic.ThrowTenSticks() : GameLogic.ThrowYut();
        LastThrow = steps;
        PendingThrows.Add(steps);
        OnThrowResult?.Invoke(steps);

        if (!tenSticks && GameLogic.GivesBonus(steps))
        {
            OnBonusThrow?.Invoke();
            State = GameState.WaitingThrow;
            boardView?.UpdateUI();
            StartTimer();
            yield break;
        }

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

    void AdvanceToPieceSelection(int player)
    {
        if (PendingThrows.Count == 0) return;
        ActiveThrow = PendingThrows[0];

        if (player == 0)
        {
            State = GameState.WaitingPieceSelect;
            boardView?.UpdateUI();
            boardView?.HighlightMovablePieces(player, ActiveThrow);
            StartTimer();
        }
        else
        {
            State = GameState.AITurn;
            boardView?.UpdateUI();
            StartCoroutine(AIPlayCoroutine());
        }
    }

    // ── Move application ──────────────────────────────────────────────────────
    void ApplyMove(int playerId, int pieceId, int steps)
    {
        var piece  = Pieces[playerId][pieceId];
        var result = GameLogic.TryMove(piece, steps, Pieces);
        if (!result.isValid) return;

        SaveSnapshot();
        int fromNode = piece.CurrentNode(BoardData.Routes);
        PendingThrows.RemoveAt(0);
        MoveStack(piece, result.newRouteId, result.newStepIndex);

        // Remove curse if finished
        if (result.isFinished)
        {
            for (int i = Curses.Count - 1; i >= 0; i--)
                if (Curses[i].player == playerId && Curses[i].pieceId == pieceId)
                {
                    Pieces[playerId][pieceId].curseTimer = -1;
                    Curses.RemoveAt(i);
                }
        }

        int toNode = result.isFinished ? -1 : piece.CurrentNode(BoardData.Routes);
        OnPieceMove?.Invoke(playerId, pieceId, fromNode, toNode);

        bool extraThrow = false;

        // Trap check
        if (!result.isFinished && toNode >= 0)
        {
            for (int t = Traps.Count - 1; t >= 0; t--)
            {
                if (Traps[t].nodeIndex == toNode && Traps[t].owner != playerId)
                {
                    SendGroupHome(playerId, toNode);
                    Traps.RemoveAt(t);
                    OnCardEffect?.Invoke("💣 함정! 집으로!");
                    boardView?.RefreshAll();
                    break;
                }
            }
        }

        // Captures
        if (result.captures.Count > 0)
        {
            int captureNode = result.captures[0].CurrentNode(BoardData.Routes);
            SendGroupHome(1 - playerId, captureNode);
            OnCapture?.Invoke(playerId, captureNode);
            extraThrow = true;
        }

        if (result.stacksOnFriend) OnStack?.Invoke(playerId, toNode);
        if (result.isFinished)     OnPieceFinished?.Invoke(playerId, pieceId);

        if (CheckWin(playerId))
        {
            State = GameState.GameOver;
            boardView?.RefreshAll();
            OnPlayerWin?.Invoke(playerId);
            boardView?.UpdateUI();
            return;
        }

        if (extraThrow)
        {
            boardView?.RefreshAll(); boardView?.UpdateUI();
            StartCoroutine(DelayedExtraThrow(playerId));
            return;
        }

        if (PendingThrows.Count > 0)
        {
            boardView?.RefreshAll();
            AdvanceToPieceSelection(playerId);
            return;
        }

        EndTurn();
    }

    void EndTurn()
    {
        TurnCount++;
        boardView?.RefreshAll();

        // Decrement traps
        for (int i = Traps.Count - 1; i >= 0; i--)
        {
            Traps[i].turnsLeft--;
            if (Traps[i].turnsLeft <= 0) Traps.RemoveAt(i);
        }

        // Decrement curses
        for (int i = Curses.Count - 1; i >= 0; i--)
        {
            Curses[i].turnsLeft--;
            Pieces[Curses[i].player][Curses[i].pieceId].curseTimer = Curses[i].turnsLeft;
            if (Curses[i].turnsLeft <= 0)
            {
                var c = Curses[i];
                if (!Pieces[c.player][c.pieceId].IsFinished(BoardData.Routes))
                {
                    Pieces[c.player][c.pieceId].stepIndex = -1;
                    Pieces[c.player][c.pieceId].routeId   = 0;
                    OnCardEffect?.Invoke($"💀 저주 만료! P{c.player} 말이 집으로!");
                }
                Pieces[c.player][c.pieceId].curseTimer = -1;
                Curses.RemoveAt(i);
            }
        }

        // Card offer: every 4 turns or every 2 pieces finished
        int totalFinished = GetFinishedCount(0) + GetFinishedCount(1);
        bool offerCard = (TurnCount == 4) ||
                         (totalFinished > 0 && totalFinished > _finishedCheck && totalFinished % 2 == 0);
        _finishedCheck = totalFinished;

        CurrentPlayer = 1 - CurrentPlayer;
        OnTurnChange?.Invoke(CurrentPlayer);

        if (offerCard)
        {
            OfferCard(CurrentPlayer);
            return;
        }

        if (CurrentPlayer == 0)
        {
            State = GameState.WaitingThrow;
            boardView?.UpdateUI();
            StartTimer();
        }
        else
        {
            State = GameState.AITurn;
            boardView?.UpdateUI();
            StartCoroutine(AIThrowCoroutine());
        }
    }

    void OfferCard(int player)
    {
        if (CardOptions == null) CardOptions = new CardType[2][];
        CardOptions[player] = CardInfo.RandomOptions();
        State = GameState.CardPicking;
        boardView?.UpdateUI();
        OnCardPickStart?.Invoke(player, CardOptions[player]);

        if (player == 1) StartCoroutine(AIPickCard());
    }

    IEnumerator AIPickCard()
    {
        yield return new WaitForSeconds(1.0f);
        if (CardOptions != null && CardOptions[1] != null && CardOptions[1].Length > 0)
        {
            PlayerCard[1] = CardOptions[1][UnityEngine.Random.Range(0, CardOptions[1].Length)];
            CardOptions[1] = null;
        }
        State = GameState.AITurn;
        boardView?.UpdateUI();
        StartCoroutine(AIThrowCoroutine());
    }

    // ── AI flow ───────────────────────────────────────────────────────────────
    IEnumerator AIThrowCoroutine()
    {
        yield return new WaitForSeconds(0.8f);

        if (PlayerCard[1] != CardType.None && UnityEngine.Random.value < 0.35f)
        {
            if (PlayerCard[1] == CardType.TenSticks)
            {
                int ts = GameLogic.ThrowTenSticks();
                LastThrow = ts; PendingThrows.Add(ts);
                PlayerCard[1] = CardType.None;
                OnThrowResult?.Invoke(ts);
                boardView?.UpdateUI();
                yield return new WaitForSeconds(0.5f);
                AdvanceToPieceSelection(1);
                yield break;
            }
            ApplyCardAI(1, PlayerCard[1]);
            boardView?.RefreshAll(); boardView?.UpdateUI();
            yield return new WaitForSeconds(0.4f);
        }

        PerformAIThrow();
        boardView?.UpdateUI();

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
        while (PendingThrows.Count > 0)
        {
            ActiveThrow = PendingThrows[0];
            yield return new WaitForSeconds(0.7f);

            int chosen = GameLogic.AIPickPiece(1, ActiveThrow, Pieces);
            if (chosen < 0) { PendingThrows.RemoveAt(0); continue; }

            boardView?.HighlightSinglePiece(1, chosen);
            yield return new WaitForSeconds(0.5f);
            ApplyMove(1, chosen, ActiveThrow);
            yield break;
        }
        EndTurn();
    }

    IEnumerator DelayedExtraThrow(int player)
    {
        yield return new WaitForSeconds(0.5f);
        if (player == 0) { State = GameState.WaitingThrow; boardView?.UpdateUI(); StartTimer(); }
        else { State = GameState.AITurn; boardView?.UpdateUI(); StartCoroutine(AIThrowCoroutine()); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void SaveSnapshot()
    {
        LastMoveSnapshot = new PieceState[2][];
        for (int p = 0; p < 2; p++)
        {
            LastMoveSnapshot[p] = new PieceState[Pieces[p].Length];
            for (int i = 0; i < Pieces[p].Length; i++)
                LastMoveSnapshot[p][i] = Pieces[p][i].Clone();
        }
    }

    void MoveStack(PieceState lead, int newRoute, int newStep)
    {
        int oldNode = lead.CurrentNode(BoardData.Routes);
        var stacked = new List<PieceState>();
        if (oldNode >= 0)
            foreach (var p in Pieces[lead.playerId])
                if (p.pieceId != lead.pieceId &&
                    !p.IsFinished(BoardData.Routes) &&
                    p.CurrentNode(BoardData.Routes) == oldNode)
                    stacked.Add(p);

        lead.routeId = newRoute; lead.stepIndex = newStep;
        foreach (var f in stacked) { f.routeId = newRoute; f.stepIndex = newStep; }
    }

    void SendGroupHome(int player, int node)
    {
        foreach (var p in Pieces[player])
            if (!p.IsHome && !p.IsFinished(BoardData.Routes) &&
                p.CurrentNode(BoardData.Routes) == node)
            { p.routeId = 0; p.stepIndex = -1; p.curseTimer = -1; }
    }

    bool CheckWin(int player) => GameLogic.HasWon(player, Pieces);

    public int GetFinishedCount(int player)
    {
        int c = 0;
        foreach (var p in Pieces[player]) if (p.IsFinished(BoardData.Routes)) c++;
        return c;
    }

    public int GetHomeCount(int player)
    {
        int c = 0;
        foreach (var p in Pieces[player]) if (p.IsHome) c++;
        return c;
    }
}
